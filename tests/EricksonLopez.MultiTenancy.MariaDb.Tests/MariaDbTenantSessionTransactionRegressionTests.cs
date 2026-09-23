// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.MariaDb;
using EricksonLopez.MultiTenancy.Testing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class MariaDbTenantSessionTransactionRegressionTests
{
    [Fact]
    public void Dispose_WhenResetActionFails_SeversConnectionToPreventPoolContamination()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        connection.Open();

        Func<DbConnection, Task> failingReset = _ => throw new TimeoutException("Simulated MariaDB timeout during session reset!");

        var sessionTx = new MariaDbTenantSessionTransaction(innerTx, connection, failingReset);

        sessionTx.Dispose();

        connection.State.Should().Be(ConnectionState.Closed);
        innerTx.Received(1).Dispose();
    }

    [Fact]
    public async Task DisposeAsync_WhenResetActionFails_SeversConnectionToPreventPoolContamination()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        connection.Open();

        Func<DbConnection, Task> failingReset = _ => Task.FromException(new InvalidOperationException("Simulated network drop during session reset!"));

        var sessionTx = new MariaDbTenantSessionTransaction(innerTx, connection, failingReset);

        await sessionTx.DisposeAsync();

        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public void Dispose_MultipleCalls_DisposesInnerOnlyOnce()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        connection.Open();
        int resetCount = 0;

        var sessionTx = new MariaDbTenantSessionTransaction(innerTx, connection, _ => { resetCount++; return Task.CompletedTask; });

        sessionTx.Dispose();
        sessionTx.Dispose();

        resetCount.Should().Be(1);
        innerTx.Received(1).Dispose();
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DisposesInnerOnlyOnce()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        connection.Open();
        int resetCount = 0;

        var sessionTx = new MariaDbTenantSessionTransaction(innerTx, connection, _ => { resetCount++; return Task.CompletedTask; });

        await sessionTx.DisposeAsync();
        await sessionTx.DisposeAsync();

        resetCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_WhenResetActionSucceeds_InnerTransactionDisposedAndConnectionNotSevered()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        connection.Open();
        bool resetExecuted = false;

        Func<DbConnection, Task> successfulReset = _ =>
        {
            resetExecuted = true;
            return Task.CompletedTask;
        };

        var sessionTx = new MariaDbTenantSessionTransaction(innerTx, connection, successfulReset);

        sessionTx.Dispose();

        resetExecuted.Should().BeTrue();
        connection.State.Should().Be(ConnectionState.Open);
        innerTx.Received(1).Dispose();
    }

    [Fact]
    public void Constructor_NullGuards_ThrowArgumentNullException()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        Func<DbConnection, Task> reset = _ => Task.CompletedTask;

        var actNullTx = () => new MariaDbTenantSessionTransaction(null!, connection, reset);
        actNullTx.Should().Throw<ArgumentNullException>().WithParameterName("innerTransaction");

        var actNullConn = () => new MariaDbTenantSessionTransaction(innerTx, null!, reset);
        actNullConn.Should().Throw<ArgumentNullException>().WithParameterName("connection");

        var actNullReset = () => new MariaDbTenantSessionTransaction(innerTx, connection, null!);
        actNullReset.Should().Throw<ArgumentNullException>().WithParameterName("resetAction");
    }

    [Fact]
    public async Task MethodsAndProperties_DelegateToInnerTransaction()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        innerTx.IsolationLevel.Returns(IsolationLevel.Serializable);
        innerTx.Connection.Returns(connection);

        var sessionTx = new MariaDbTenantSessionTransaction(innerTx, connection, _ => Task.CompletedTask);

        sessionTx.IsolationLevel.Should().Be(IsolationLevel.Serializable);
        sessionTx.Connection.Should().BeSameAs(connection);

        sessionTx.Commit();
        innerTx.Received(1).Commit();

        await sessionTx.CommitAsync(CancellationToken.None);
        await innerTx.Received(1).CommitAsync(Arg.Any<CancellationToken>());

        sessionTx.Rollback();
        innerTx.Received(1).Rollback();

        await sessionTx.RollbackAsync(CancellationToken.None);
        await innerTx.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Dispose_WithSyncResetAction_ExecutesSyncReset()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        connection.Open();
        bool syncExecuted = false;

        var sessionTx = new MariaDbTenantSessionTransaction(
            innerTx,
            connection,
            _ => Task.CompletedTask,
            _ => { syncExecuted = true; });

        sessionTx.Dispose();

        syncExecuted.Should().BeTrue();
        innerTx.Received(1).Dispose();
    }

    [Fact]
    public void Dispose_WhenSeverConnectionThrows_SwallowsException()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FailingCloseDbConnection();
        connection.Open();

        Func<DbConnection, Task> failingReset = _ => throw new TimeoutException("Reset failed");
        var sessionTx = new MariaDbTenantSessionTransaction(innerTx, connection, failingReset);

        var act = () => sessionTx.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_WhenConnectionClosed_DoesNotCallResetAction()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        bool resetCalled = false;
        Func<DbConnection, Task> reset = _ =>
        {
            resetCalled = true;
            return Task.CompletedTask;
        };

        var sessionTx = new MariaDbTenantSessionTransaction(innerTx, connection, reset);
        await sessionTx.DisposeAsync();

        resetCalled.Should().BeFalse();
    }

    [Fact]
    public async Task BeginTenantTransactionAsync_WhenRollbackThrows_PreservesOriginalException()
    {
        var connection = new ThrowingRollbackConnection();
        var context = Substitute.For<ITenantContext>();
        context.RequiredTenant.Returns(new TenantInfo(TenantId.Empty, "MariaDbTenant"));

        var act = () => connection.BeginTenantTransactionAsync(context);
        await act.Should().ThrowAsync<TenantNotFoundException>();
    }

    private sealed class FailingCloseDbConnection : FakeDbConnection
    {
        public override void Close() => throw new InvalidOperationException("Failed to close socket.");
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
