// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.PostgreSql;
using EricksonLopez.MultiTenancy.Testing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class PostgreSqlRlsExtensionsTests
{
    private static readonly Guid ExpectedGuid = Guid.Parse("dddddddd-0000-0000-0000-000000000004");
    private static readonly TenantId ExpectedTenantId = new TenantId(ExpectedGuid);

    // ─────────────────────────────────────────────────────────
    // SetTenantRlsContextAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetTenantRlsContextAsync_NullConnection_ThrowsArgumentNullException()
    {
        FakeDbConnection connection = null!;
        var transaction = new FakeDbTransaction(new FakeDbConnection(), IsolationLevel.ReadCommitted);
        var context = Substitute.For<ITenantContext>();

        var act = () => connection.SetTenantRlsContextAsync(transaction, context);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");
    }

    [Fact]
    public async Task SetTenantRlsContextAsync_NullContext_ThrowsArgumentNullException()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);

        var act = () => connection.SetTenantRlsContextAsync(transaction, null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("tenantContext");
    }

    [Fact]
    public async Task SetTenantRlsContextAsync_NullTransaction_ThrowsInvalidOperationException()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);

        var act = () => connection.SetTenantRlsContextAsync(null!, context);
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("SET LOCAL requires an active transaction. RLS context must be set within a transaction to guarantee that the variable is automatically cleared on transaction end. Call BeginTransactionAsync() before calling SetTenantRlsContextAsync().");
    }

    [Fact]
    public async Task SetTenantRlsContextAsync_EmptySessionVariable_ThrowsArgumentException()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var context = CreateContext(ExpectedTenantId);

        var act = () => connection.SetTenantRlsContextAsync(transaction, context, sessionVariable: "   ");
        (await act.Should().ThrowAsync<ArgumentException>().WithParameterName("sessionVariable"))
            .WithMessage("Session variable name must not be null or whitespace. (Parameter 'sessionVariable')");
    }

    [Fact]
    public async Task SetTenantRlsContextAsync_InvalidSessionVariable_ThrowsArgumentException()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var context = CreateContext(ExpectedTenantId);

        var act = () => connection.SetTenantRlsContextAsync(transaction, context, sessionVariable: "invalid var with space");
        (await act.Should().ThrowAsync<ArgumentException>().WithParameterName("sessionVariable"))
            .WithMessage("*Session variable name must be alphanumeric*");
    }

    [Fact]
    public async Task SetTenantRlsContextAsync_EmptyTenantId_ThrowsTenantNotFoundException()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var context = CreateContext(TenantId.Empty);

        var act = () => connection.SetTenantRlsContextAsync(transaction, context);
        (await act.Should().ThrowAsync<TenantNotFoundException>())
            .WithMessage("Tenant identifier is empty. Cannot establish RLS context.");
    }

    [Fact]
    public async Task SetTenantRlsContextAsync_Valid_ExecutesExpectedCommand()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var context = CreateContext(ExpectedTenantId);

        await connection.SetTenantRlsContextAsync(transaction, context, sessionVariable: "custom.tenant");

        var cmd = connection.Commands.Single();
        cmd.CommandText.Should().Be($"SET LOCAL \"custom.tenant\" = '{ExpectedGuid}'");
        cmd.Transaction.Should().BeSameAs(transaction);
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

        var transaction = await connection.BeginTenantTransactionAsync(context, IsolationLevel.RepeatableRead);

        connection.State.Should().Be(ConnectionState.Open);
        transaction.IsolationLevel.Should().Be(IsolationLevel.RepeatableRead);

        var cmd = connection.Commands.Single();
        cmd.Transaction.Should().BeSameAs(transaction);
        cmd.CommandText.Should().Be($"SET LOCAL \"app.current_tenant_id\" = '{ExpectedGuid}'");
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
    public async Task SetTenantRlsContextAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var connection = new FakeDbConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var context = CreateContext(ExpectedTenantId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => connection.SetTenantRlsContextAsync(tx, context, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BeginTenantTransactionAsync_WhenRollbackThrows_PreservesOriginalException()
    {
        var connection = new ThrowingRollbackConnection();
        var context = CreateContext(TenantId.Empty);

        var act = () => connection.BeginTenantTransactionAsync(context);
        await act.Should().ThrowAsync<TenantNotFoundException>();
    }

    private static ITenantContext CreateContext(TenantId id)
    {
        var tenantInfo = new TenantInfo(id, "PgTenant");
        var context = Substitute.For<ITenantContext>();
        context.RequiredTenant.Returns(tenantInfo);
        return context;
    }

    private sealed class ThrowingRollbackConnection : FakeDbConnection
    {
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            var tx = new ThrowingRollbackTransaction(this, isolationLevel);
            Transactions.Add(tx);
            return tx;
        }

        protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
        {
            var tx = new ThrowingRollbackTransaction(this, isolationLevel);
            Transactions.Add(tx);
            return new(tx);
        }
    }

    private sealed class ThrowingRollbackTransaction : FakeDbTransaction
    {
        public ThrowingRollbackTransaction(DbConnection connection, IsolationLevel isolationLevel)
            : base(connection, isolationLevel) { }

        public override Task RollbackAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated socket teardown error during rollback.");
    }
}
