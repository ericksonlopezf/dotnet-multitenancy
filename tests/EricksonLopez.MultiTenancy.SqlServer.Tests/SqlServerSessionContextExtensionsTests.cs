// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.SqlServer;
using EricksonLopez.MultiTenancy.Testing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class SqlServerSessionContextExtensionsTests
{
    private static readonly Guid ExpectedGuid = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly TenantId ExpectedTenantId = new TenantId(ExpectedGuid);

    // ─────────────────────────────────────────────────────────
    // SetTenantSessionContextAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetTenantSessionContextAsync_NullConnection_ThrowsArgumentNullException()
    {
        FakeDbConnection connection = null!;
        var context = Substitute.For<ITenantContext>();

        var act = () => connection.SetTenantSessionContextAsync(context);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");
    }

    [Fact]
    public async Task SetTenantSessionContextAsync_NullContext_ThrowsArgumentNullException()
    {
        var connection = new FakeDbConnection();
        var act = () => connection.SetTenantSessionContextAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("tenantContext");
    }

    [Fact]
    public async Task SetTenantSessionContextAsync_EmptySessionKey_ThrowsArgumentException()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);

        var act = () => connection.SetTenantSessionContextAsync(context, sessionKey: "   ");
        (await act.Should().ThrowAsync<ArgumentException>().WithParameterName("sessionKey"))
            .WithMessage("Session key name must not be null or whitespace. (Parameter 'sessionKey')");
    }

    [Fact]
    public async Task SetTenantSessionContextAsync_EmptyTenantId_ThrowsTenantNotFoundException()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(TenantId.Empty);

        var act = () => connection.SetTenantSessionContextAsync(context);
        (await act.Should().ThrowAsync<TenantNotFoundException>())
            .WithMessage("Tenant identifier is empty. Cannot establish SQL Server session context.");
    }

    [Fact]
    public async Task SetTenantSessionContextAsync_Valid_ExecutesExpectedCommand()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);

        await connection.SetTenantSessionContextAsync(context, readOnly: true, sessionKey: "CustomKey");

        var cmd = connection.Commands.Single();
        cmd.CommandText.Should().Be("EXEC sp_set_session_context @key = @Key, @value = @Value, @read_only = @ReadOnly;");

        cmd.Parameters["Key"].Value.Should().Be("CustomKey");
        cmd.Parameters["Key"].DbType.Should().Be(DbType.String);

        cmd.Parameters["Value"].Value.Should().Be(ExpectedGuid);
        cmd.Parameters["Value"].DbType.Should().Be(DbType.Guid);

        cmd.Parameters["ReadOnly"].Value.Should().Be(1);
        cmd.Parameters["ReadOnly"].DbType.Should().Be(DbType.Int32);
    }

    [Fact]
    public async Task SetTenantSessionContextAsync_NotReadOnly_ExecutesExpectedCommand()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);

        await connection.SetTenantSessionContextAsync(context, readOnly: false);

        var cmd = connection.Commands.Single();
        cmd.Parameters["ReadOnly"].Value.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────
    // ResetTenantSessionContextAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetTenantSessionContextAsync_NullConnection_ThrowsArgumentNullException()
    {
        FakeDbConnection connection = null!;
        var act = () => connection.ResetTenantSessionContextAsync();
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");
    }

    [Fact]
    public async Task ResetTenantSessionContextAsync_EmptySessionKey_ThrowsArgumentException()
    {
        var connection = new FakeDbConnection();
        var act = () => connection.ResetTenantSessionContextAsync(sessionKey: "");
        (await act.Should().ThrowAsync<ArgumentException>().WithParameterName("sessionKey"))
            .WithMessage("Session key name must not be null or whitespace. (Parameter 'sessionKey')");
    }

    [Fact]
    public async Task ResetTenantSessionContextAsync_Valid_ExecutesExpectedCommand()
    {
        var connection = new FakeDbConnection();

        await connection.ResetTenantSessionContextAsync(sessionKey: "ResetKey");

        var cmd = connection.Commands.Single();
        cmd.CommandText.Should().Be("EXEC sp_set_session_context @key = @Key, @value = NULL, @read_only = 0;");
        cmd.Parameters["Key"].Value.Should().Be("ResetKey");
    }

    // ─────────────────────────────────────────────────────────
    // BeginTenantTransactionAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task BeginTenantTransactionAsync_NullConnection_ThrowsArgumentNullException()
    {
        FakeDbConnection connection = null!;
        var context = CreateContext(ExpectedTenantId);

        var act = () => connection.BeginTenantTransactionAsync(context);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");
    }

    [Fact]
    public async Task BeginTenantTransactionAsync_NullContext_ThrowsArgumentNullException()
    {
        var connection = new FakeDbConnection();
        var act = () => connection.BeginTenantTransactionAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("tenantContext");
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task BeginTenantTransactionAsync_ClosedConnection_OpensConnectionAndStartsTransaction()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);

        connection.State.Should().Be(ConnectionState.Closed);

        var transaction = await connection.BeginTenantTransactionAsync(context, IsolationLevel.Serializable);

        connection.State.Should().Be(ConnectionState.Open);
        transaction.IsolationLevel.Should().Be(IsolationLevel.Serializable);

        var cmd = connection.Commands.Single();
        cmd.Transaction.Should().BeSameAs(transaction);
        cmd.CommandText.Should().Contain("sp_set_session_context");
    }

    [Fact]
    public async Task BeginTenantTransactionAsync_OpenConnection_StartsTransactionWithoutReopening()
    {
        var connection = new FakeDbConnection();
        connection.Open();
        var context = CreateContext(ExpectedTenantId);

        var transaction = await connection.BeginTenantTransactionAsync(context);

        connection.State.Should().Be(ConnectionState.Open);
        transaction.Should().NotBeNull();
    }

    [Fact]
    public async Task BeginTenantTransactionAsync_SetContextFails_RollbacksAndDisposesTransaction()
    {
        var connection = new FakeDbConnection();
        // Passing Empty Tenant ID will make SetTenantSessionContextAsync throw TenantNotFoundException
        var context = CreateContext(TenantId.Empty);

        var act = () => connection.BeginTenantTransactionAsync(context);
        await act.Should().ThrowAsync<TenantNotFoundException>();

        var transaction = connection.Transactions.Single();
        transaction.IsRolledBack.Should().BeTrue();
        transaction.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task BeginTenantTransactionAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => connection.BeginTenantTransactionAsync(context, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SetTenantSessionContextAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => connection.SetTenantSessionContextAsync(context, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static ITenantContext CreateContext(TenantId id)
    {
        var tenantInfo = new TenantInfo(id, "SqlTenant");
        var context = Substitute.For<ITenantContext>();
        context.RequiredTenant.Returns(tenantInfo);
        return context;
    }
}
