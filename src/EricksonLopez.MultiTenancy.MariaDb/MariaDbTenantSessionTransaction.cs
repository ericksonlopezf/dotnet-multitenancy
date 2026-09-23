// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.MultiTenancy.MariaDb;

/// <summary>
/// Represents a <see cref="DbTransaction"/> decorator that automatically resets MariaDB tenant session variables upon transaction disposal,
/// ensuring that pooled connections returned to ADO.NET do not retain tenant context.
/// </summary>
internal sealed class MariaDbTenantSessionTransaction : DbTransaction
{
    private readonly DbTransaction _innerTransaction;
    private readonly DbConnection _connection;
    private readonly Func<DbConnection, Task> _resetAction;
    private readonly Action<DbConnection>? _syncResetAction;
    private bool _disposed;

    public MariaDbTenantSessionTransaction(
        DbTransaction innerTransaction,
        DbConnection connection,
        Func<DbConnection, Task> resetAction)
        : this(innerTransaction, connection, resetAction, null)
    {
    }

    public MariaDbTenantSessionTransaction(
        DbTransaction innerTransaction,
        DbConnection connection,
        Func<DbConnection, Task> resetAction,
        Action<DbConnection>? syncResetAction)
    {
        _innerTransaction = innerTransaction ?? throw new ArgumentNullException(nameof(innerTransaction));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _resetAction = resetAction ?? throw new ArgumentNullException(nameof(resetAction));
        _syncResetAction = syncResetAction;
    }

    public override IsolationLevel IsolationLevel => _innerTransaction.IsolationLevel;

    protected override DbConnection? DbConnection => _innerTransaction.Connection;

    public override void Commit() => _innerTransaction.Commit();

    public override Task CommitAsync(CancellationToken cancellationToken = default) =>
        _innerTransaction.CommitAsync(cancellationToken);

    public override void Rollback() => _innerTransaction.Rollback();

    public override Task RollbackAsync(CancellationToken cancellationToken = default) =>
        _innerTransaction.RollbackAsync(cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            if (disposing)
            {
                try
                {
                    if (_connection.State == ConnectionState.Open)
                    {
                        if (_syncResetAction != null)
                        {
                            _syncResetAction(_connection);
                        }
                        else
                        {
                            // Avoid sync-over-async deadlock when called from a single-threaded SynchronizationContext
                            Task.Run(async () => await _resetAction(_connection).ConfigureAwait(false)).GetAwaiter().GetResult();
                        }
                    }
                }
                catch
                {
                    // CRITICAL SECURITY MITIGATION:
                    // If session reset fails, sever and close the physical connection immediately
                    // so ADO.NET discards the socket rather than returning a tainted connection to the pool.
                    SeverConnection();
                }
                finally
                {
                    _innerTransaction.Dispose();
                }
            }
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            try
            {
                if (_connection.State == ConnectionState.Open)
                {
                    await _resetAction(_connection).ConfigureAwait(false);
                }
            }
            catch
            {
                // CRITICAL SECURITY MITIGATION:
                SeverConnection();
            }
            finally
            {
                await _innerTransaction.DisposeAsync().ConfigureAwait(false);
            }
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    private void SeverConnection()
    {
        try
        {
            _connection.Close();
            _connection.Dispose();
        }
        catch
        {
            // Socket already faulted or closed
        }
    }
}
