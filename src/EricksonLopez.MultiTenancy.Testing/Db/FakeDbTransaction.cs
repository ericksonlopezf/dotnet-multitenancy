// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.MultiTenancy.Testing;

/// <summary>
/// Provides an in-memory fake implementation of <see cref="DbTransaction"/>.
/// </summary>
public class FakeDbTransaction : DbTransaction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FakeDbTransaction"/> class.
    /// </summary>
    /// <param name="connection">The database connection associated with this transaction.</param>
    /// <param name="isolationLevel">The transaction isolation level.</param>
    public FakeDbTransaction(DbConnection connection, IsolationLevel isolationLevel)
    {
        DbConnection = connection;
        IsolationLevel = isolationLevel;
    }

    /// <inheritdoc />
    protected override DbConnection DbConnection { get; }

    /// <inheritdoc />
    public override IsolationLevel IsolationLevel { get; }

    /// <summary>
    /// Gets a value indicating whether the transaction was committed.
    /// </summary>
    public bool IsCommitted { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the transaction was rolled back.
    /// </summary>
    public bool IsRolledBack { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the transaction was disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public override void Commit() => IsCommitted = true;

    /// <inheritdoc />
    public override void Rollback() => IsRolledBack = true;

    /// <inheritdoc />
    public override Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRolledBack = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task CommitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsCommitted = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
        return base.DisposeAsync();
    }
}
