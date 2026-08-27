// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.MultiTenancy.Sqlite;

/// <summary>
/// Provides SQLite-specific multi-tenant connection and transaction utilities.
/// Supports both database-per-tenant file isolation and connection-scoped temporary table isolation.
/// </summary>
public static class SqliteTenantExtensions
{
    /// <summary>
    /// Establishes the tenant context in a shared SQLite connection by populating a temporary table (<c>_current_tenant</c>).
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="tenantContext">The resolved tenant context containing the tenant identifier.</param>
    /// <param name="transaction">The optional active SQLite transaction.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    /// <exception cref="TenantNotFoundException">No tenant has been resolved in the current context or the tenant identifier is empty</exception>
    public static Task SetTenantContextAsync(
        this DbConnection connection,
        ITenantContext tenantContext,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(tenantContext);

        var tenant = tenantContext.RequiredTenant;
        var tenantIdValue = tenant.Id.Value;

        if (tenantIdValue == Guid.Empty)
        {
            throw new TenantNotFoundException("Tenant identifier is empty. Cannot establish SQLite tenant context.");
        }

        const string sql =
            "CREATE TEMP TABLE IF NOT EXISTS _current_tenant (tenant_id TEXT PRIMARY KEY); " +
            "DELETE FROM _current_tenant; " +
            "INSERT INTO _current_tenant (tenant_id) VALUES (@TenantId);";

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantIdValue.ToString(), DbType.String);

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }

    /// <summary>
    /// Clears the temporary tenant table (<c>_current_tenant</c>) on a shared SQLite connection.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="transaction">The optional active SQLite transaction.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/></exception>
    public static Task ResetTenantContextAsync(
        this DbConnection connection,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        const string sql = "DELETE FROM _current_tenant;";

        var command = new CommandDefinition(
            sql,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }

    /// <summary>
    /// Opens a new transaction on the provided SQLite connection and sets the tenant context atomically.
    /// </summary>
    /// <param name="connection">The database connection (opened automatically if not already open).</param>
    /// <param name="tenantContext">The resolved tenant context.</param>
    /// <param name="isolationLevel">The transaction isolation level (default: <see cref="IsolationLevel.ReadCommitted"/>).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the started transaction with tenant context applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    public static async Task<DbTransaction> BeginTenantTransactionAsync(
        this DbConnection connection,
        ITenantContext tenantContext,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (connection.State != ConnectionState.Open)
        {
            // Stryker disable once boolean
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        // Stryker disable once boolean
        var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);

        try
        {
            // Stryker disable once boolean
            await connection.SetTenantContextAsync(tenantContext, transaction, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Stryker disable once boolean
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            // Stryker disable once boolean
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return transaction;
    }
}
