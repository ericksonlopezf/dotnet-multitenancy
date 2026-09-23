// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.Oracle;
using EricksonLopez.MultiTenancy.Testing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class FakeDbConnectionWithoutClientId : DbConnection
{
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FakeDbTransaction(this, isolationLevel);
    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    protected override DbCommand CreateDbCommand() => new FakeDbCommand { Connection = this };
    public override void Open() { }
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString { get; set; } = "";
    public override string Database => "";
    public override ConnectionState State => ConnectionState.Open;
    public override string DataSource => "";
    public override string ServerVersion => "";
}

public class FakeDbConnectionWithReadOnlyClientId : FakeDbConnectionWithoutClientId
{
    public string ClientId { get; } = "readonly";
}

public class OracleVpdExtensionsTests
{
    private static readonly Guid ExpectedGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000007");
    private static readonly TenantId ExpectedTenantId = new TenantId(ExpectedGuid);

    // ─────────────────────────────────────────────────────────
    // SetTenantVpdContextAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetTenantVpdContextAsync_NullConnection_ThrowsArgumentNullException()
    {
        FakeDbConnection connection = null!;
        var transaction = Substitute.For<DbTransaction>();
        var context = Substitute.For<ITenantContext>();

        var act = () => connection.SetTenantVpdContextAsync(transaction, context);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");
    }

    [Fact]
    public async Task SetTenantVpdContextAsync_NullContext_ThrowsArgumentNullException()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var act = () => connection.SetTenantVpdContextAsync(transaction, null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("tenantContext");
    }

    [Fact]
    public async Task SetTenantVpdContextAsync_NullTransaction_ThrowsInvalidOperationException()
    {
        var connection = new FakeDbConnection();
        var context = CreateContext(ExpectedTenantId);

        var act = () => connection.SetTenantVpdContextAsync(null!, context);
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Setting Oracle VPD context requires an active transaction to prevent context leakage across connection pool reuse. Call BeginTenantTransactionAsync() or BeginTransactionAsync() before calling SetTenantVpdContextAsync().");
    }

    [Fact]
    public async Task SetTenantVpdContextAsync_EmptyTenantId_ThrowsTenantNotFoundException()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var context = CreateContext(TenantId.Empty);

        var act = () => connection.SetTenantVpdContextAsync(transaction, context);
        (await act.Should().ThrowAsync<TenantNotFoundException>())
            .WithMessage("Tenant identifier is empty. Cannot establish Oracle VPD tenant context.");
    }

    [Fact]
    public async Task SetTenantVpdContextAsync_Valid_ExecutesExpectedCommandAndSetsClientId()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var context = CreateContext(ExpectedTenantId);

        await connection.SetTenantVpdContextAsync(transaction, context);

        var cmd = connection.Commands.Single();
        cmd.CommandText.Should().Be("BEGIN DBMS_SESSION.SET_IDENTIFIER(:tenantId); END;");
        cmd.Transaction.Should().BeSameAs(transaction);

        cmd.Parameters["tenantId"].Value.Should().Be(ExpectedGuid.ToString());
        cmd.Parameters["tenantId"].DbType.Should().Be(DbType.String);

        connection.ClientId.Should().Be(ExpectedGuid.ToString());
    }

    [Fact]
    public async Task SetTenantVpdContextAsync_MissingClientIdProperty_DoesNotThrow()
    {
        var connection = new FakeDbConnectionWithoutClientId();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var context = CreateContext(ExpectedTenantId);

        var act = () => connection.SetTenantVpdContextAsync(transaction, context);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetTenantVpdContextAsync_ReadOnlyClientIdProperty_DoesNotThrow()
    {
        var connection = new FakeDbConnectionWithReadOnlyClientId();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var context = CreateContext(ExpectedTenantId);

        var act = () => connection.SetTenantVpdContextAsync(transaction, context);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetTenantVpdContextAsync_ValidWithoutClientId_ExecutesCommandButDoesNotSetClientId()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var context = CreateContext(ExpectedTenantId);

        await connection.SetTenantVpdContextAsync(transaction, context, setClientIdProperty: false);

        var cmd = connection.Commands.Single();
        cmd.CommandText.Should().Be("BEGIN DBMS_SESSION.SET_IDENTIFIER(:tenantId); END;");
        cmd.Transaction.Should().BeSameAs(transaction);
        connection.ClientId.Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────
    // ResetTenantVpdContextAsync
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetTenantVpdContextAsync_NullConnection_ThrowsArgumentNullException()
    {
        FakeDbConnection connection = null!;
        var act = () => connection.ResetTenantVpdContextAsync();
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("connection");
    }

    [Fact]
    public async Task ResetTenantVpdContextAsync_Valid_ExecutesExpectedCommandAndClearsClientId()
    {
        var connection = new FakeDbConnection { ClientId = "existing" };

        await connection.ResetTenantVpdContextAsync();

        var cmd = connection.Commands.Single();
        cmd.CommandText.Should().Be("BEGIN DBMS_SESSION.CLEAR_IDENTIFIER; END;");

        connection.ClientId.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetTenantVpdContextAsync_MissingClientIdProperty_DoesNotThrow()
    {
        var connection = new FakeDbConnectionWithoutClientId();
        var act = () => connection.ResetTenantVpdContextAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetTenantVpdContextAsync_ReadOnlyClientIdProperty_DoesNotThrow()
    {
        var connection = new FakeDbConnectionWithReadOnlyClientId();
        var act = () => connection.ResetTenantVpdContextAsync();
        await act.Should().NotThrowAsync();
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
        cmd.CommandText.Should().Be("BEGIN DBMS_SESSION.SET_IDENTIFIER(:tenantId); END;");
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
    public async Task SetTenantVpdContextAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection, IsolationLevel.ReadCommitted);
        var context = CreateContext(ExpectedTenantId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => connection.SetTenantVpdContextAsync(transaction, context, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ResetTenantVpdContextAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var connection = new FakeDbConnection();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => connection.ResetTenantVpdContextAsync(cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ResetTenantVpdContext_NullConnection_ThrowsArgumentNullException()
    {
        DbConnection connection = null!;
        var act = () => connection.ResetTenantVpdContext();
        act.Should().Throw<ArgumentNullException>().WithParameterName("connection");
    }

    [Fact]
    public void ResetTenantVpdContext_Valid_ClearsClientIdAndExecutesCommand()
    {
        var connection = new FakeDbConnection
        {
            ClientId = "OLD_TENANT"
        };
        connection.Open();

        connection.ResetTenantVpdContext();

        connection.ClientId.Should().BeEmpty();
        connection.Commands.Should().ContainSingle();
        connection.Commands.Single().CommandText.Should().Be("BEGIN DBMS_SESSION.CLEAR_IDENTIFIER; END;");
    }

    private static ITenantContext CreateContext(TenantId id)
    {
        var tenantInfo = new TenantInfo(id, "OracleTenant");
        var context = Substitute.For<ITenantContext>();
        context.RequiredTenant.Returns(tenantInfo);
        return context;
    }
}
