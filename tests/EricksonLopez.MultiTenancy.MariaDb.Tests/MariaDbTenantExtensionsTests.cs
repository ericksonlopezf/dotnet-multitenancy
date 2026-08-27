// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.MariaDb;
using EricksonLopez.MultiTenancy.Testing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class MariaDbTenantExtensionsTests
{
    private static readonly Guid ExpectedGuid = Guid.Parse("ffffffff-0000-0000-0000-000000000006");
    private static readonly TenantId ExpectedTenantId = new TenantId(ExpectedGuid);

    // ─────────────────────────────────────────────────────────
    // SetTenantSessionVariableAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetTenantSessionVariableAsync_NullConnection_ThrowsArgumentNullException()
    {
        FakeDbConnection connection = null!;
        var context = Substitute.For<ITenantContext>();

        var act = () => connection.SetTenantSessionVariableAsync(context);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");
    }

    [Fact]
    public async Task SetTenantSessionVariableAsync_NullContext_ThrowsArgumentNullException()
    {
        var connection = new FakeDbConnection();
        var act = () => connection.SetTenantSessionVariableAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("tenantContext");
    }

    [Fact]
    public async Task SetTenantSessionVariableAsync_EmptyVariableName_ThrowsArgumentException()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);

        var act = () => connection.SetTenantSessionVariableAsync(context, variableName: "   ");
        (await act.Should().ThrowAsync<ArgumentException>().WithParameterName("variableName"))
            .WithMessage("Variable name must not be null or whitespace. (Parameter 'variableName')");
    }

    [Fact]
    public async Task SetTenantSessionVariableAsync_EmptyTenantId_ThrowsTenantNotFoundException()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(TenantId.Empty);

        var act = () => connection.SetTenantSessionVariableAsync(context);
        (await act.Should().ThrowAsync<TenantNotFoundException>())
            .WithMessage("Tenant identifier is empty. Cannot establish MariaDB tenant session variable.");
    }

    [Fact]
    public async Task SetTenantSessionVariableAsync_Valid_ExecutesExpectedCommand()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);

        await connection.SetTenantSessionVariableAsync(context, variableName: "@custom_tenant_maria");

        var cmd = connection.Commands.Single();
        cmd.CommandText.Should().Be("SET @custom_tenant_maria = @Value;");

        cmd.Parameters["Value"].Value.Should().Be(ExpectedGuid.ToString());
        cmd.Parameters["Value"].DbType.Should().Be(DbType.String);
    }

    // ─────────────────────────────────────────────────────────
    // ResetTenantSessionVariableAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetTenantSessionVariableAsync_NullConnection_ThrowsArgumentNullException()
    {
        FakeDbConnection connection = null!;
        var act = () => connection.ResetTenantSessionVariableAsync();
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");
    }

    [Fact]
    public async Task ResetTenantSessionVariableAsync_EmptyVariableName_ThrowsArgumentException()
    {
        var connection = new FakeDbConnection();
        var act = () => connection.ResetTenantSessionVariableAsync(variableName: "");
        (await act.Should().ThrowAsync<ArgumentException>().WithParameterName("variableName"))
            .WithMessage("Variable name must not be null or whitespace. (Parameter 'variableName')");
    }

    [Fact]
    public async Task ResetTenantSessionVariableAsync_Valid_ExecutesExpectedCommand()
    {
        var connection = new FakeDbConnection();

        await connection.ResetTenantSessionVariableAsync(variableName: "reset_var");

        var cmd = connection.Commands.Single();
        cmd.CommandText.Should().Be("SET @reset_var = NULL;");
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

        var transaction = await connection.BeginTenantTransactionAsync(context, IsolationLevel.Serializable);

        connection.State.Should().Be(ConnectionState.Open);
        transaction.IsolationLevel.Should().Be(IsolationLevel.Serializable);

        var cmd = connection.Commands.Single();
        cmd.Transaction.Should().BeSameAs(transaction);
        cmd.CommandText.Should().Be("SET @app_tenant_id = @Value;");
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
    public async Task SetTenantSessionVariableAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => connection.SetTenantSessionVariableAsync(context, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ResetTenantSessionVariableAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var connection = new FakeDbConnection();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => connection.ResetTenantSessionVariableAsync(cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ResetTenantSessionVariableAsync_WithLeadingAtSymbol_TrimsAtCorrectly()
    {
        var connection = new FakeDbConnection();
        await connection.ResetTenantSessionVariableAsync(variableName: "@custom_tenant_id");

        connection.Commands.Should().ContainSingle();
        connection.Commands.Single().CommandText.Should().Be("SET @custom_tenant_id = NULL;");
    }

    private static ITenantContext CreateContext(TenantId id)
    {
        var tenantInfo = new TenantInfo(id, "MariaDbTenant");
        var context = Substitute.For<ITenantContext>();
        context.RequiredTenant.Returns(tenantInfo);
        return context;
    }
}
