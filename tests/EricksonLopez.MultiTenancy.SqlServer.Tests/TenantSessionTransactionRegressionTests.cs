// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.SqlServer;
using EricksonLopez.MultiTenancy.Testing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class TenantSessionTransactionRegressionTests
{
    [Fact]
    public void Dispose_WhenResetActionFails_SeversConnectionToPreventPoolContamination()
    {
        // Arrange
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        connection.Open();

        Func<DbConnection, Task> failingReset = _ => throw new TimeoutException("Simulated SQL Server timeout during session reset!");

        var sessionTx = new TenantSessionTransaction(innerTx, connection, failingReset);

        // Act - Dispose encounters reset exception
        sessionTx.Dispose();

        // Assert - Connection MUST be severed / closed to protect ADO.NET connection pool
        connection.State.Should().Be(ConnectionState.Closed);
        innerTx.Received(1).Dispose();
    }

    [Fact]
    public async Task DisposeAsync_WhenResetActionFails_SeversConnectionToPreventPoolContamination()
    {
        // Arrange
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        connection.Open();

        Func<DbConnection, Task> failingReset = _ => Task.FromException(new InvalidOperationException("Simulated network drop during session reset!"));

        var sessionTx = new TenantSessionTransaction(innerTx, connection, failingReset);

        // Act - DisposeAsync encounters reset exception
        await sessionTx.DisposeAsync();

        // Assert - Connection MUST be severed / closed to protect ADO.NET connection pool
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public void Dispose_MultipleCalls_DisposesInnerOnlyOnce()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        connection.Open();
        int resetCount = 0;

        var sessionTx = new TenantSessionTransaction(innerTx, connection, _ => { resetCount++; return Task.CompletedTask; });

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

        var sessionTx = new TenantSessionTransaction(innerTx, connection, _ => { resetCount++; return Task.CompletedTask; });

        await sessionTx.DisposeAsync();
        await sessionTx.DisposeAsync();

        resetCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_WhenResetActionSucceeds_InnerTransactionDisposedAndConnectionNotSevered()
    {
        // Arrange
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        connection.Open();
        bool resetExecuted = false;

        Func<DbConnection, Task> successfulReset = _ =>
        {
            resetExecuted = true;
            return Task.CompletedTask;
        };

        var sessionTx = new TenantSessionTransaction(innerTx, connection, successfulReset);

        // Act
        sessionTx.Dispose();

        // Assert
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

        var actNullTx = () => new TenantSessionTransaction(null!, connection, reset);
        actNullTx.Should().Throw<ArgumentNullException>().WithParameterName("innerTransaction");

        var actNullConn = () => new TenantSessionTransaction(innerTx, null!, reset);
        actNullConn.Should().Throw<ArgumentNullException>().WithParameterName("connection");

        var actNullReset = () => new TenantSessionTransaction(innerTx, connection, null!);
        actNullReset.Should().Throw<ArgumentNullException>().WithParameterName("resetAction");
    }

    [Fact]
    public async Task MethodsAndProperties_DelegateToInnerTransaction()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        innerTx.IsolationLevel.Returns(IsolationLevel.Serializable);
        innerTx.Connection.Returns(connection);

        var sessionTx = new TenantSessionTransaction(innerTx, connection, _ => Task.CompletedTask);

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

        var sessionTx = new TenantSessionTransaction(
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
        var sessionTx = new TenantSessionTransaction(innerTx, connection, failingReset);

        var act = () => sessionTx.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_WhenConnectionClosed_DoesNotCallResetAction()
    {
        var innerTx = Substitute.For<DbTransaction>();
        var connection = new FakeDbConnection();
        // Connection is closed (not open)
        bool resetCalled = false;
        Func<DbConnection, Task> reset = _ =>
        {
            resetCalled = true;
            return Task.CompletedTask;
        };

        var sessionTx = new TenantSessionTransaction(innerTx, connection, reset);
        await sessionTx.DisposeAsync();

        resetCalled.Should().BeFalse();
    }

    private sealed class FailingCloseDbConnection : FakeDbConnection
    {
        public override void Close() => throw new InvalidOperationException("Failed to close socket.");
    }
}
