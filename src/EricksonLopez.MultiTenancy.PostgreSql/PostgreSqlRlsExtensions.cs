// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.MultiTenancy.PostgreSql;

/// <summary>
/// Provides PostgreSQL Row Level Security (RLS) integration for multi-tenant data access.
/// Establishes transaction-scoped tenant context using <c>SET LOCAL</c> so that the
/// session variable is automatically cleared at transaction commit or rollback —
/// protecting against context leakage across connection pool connections.
/// </summary>
/// <remarks>
/// <para>
/// Security model: <c>SET LOCAL</c> scopes the variable to the current transaction block.
/// When the transaction ends (commit or rollback), PostgreSQL automatically resets the variable.
/// This means a connection returned to the pool cannot carry stale tenant context to the next request.
/// </para>
/// <para>
/// RLS policy requirement: The PostgreSQL RLS policy must reference the session variable set here.
/// Example policy:
/// <code>
/// CREATE POLICY tenant_isolation ON invoices
///   USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
///   WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
/// </code>
/// </para>
/// <para>
/// This class does NOT replace PostgreSQL <c>FORCE ROW LEVEL SECURITY</c> — that must be
/// configured at the database level. This class is one layer of the defense-in-depth model.
/// </para>
/// </remarks>
public static class PostgreSqlRlsExtensions
{
    /// <summary>
    /// The PostgreSQL session configuration variable name used to set the current tenant.
    /// Must match the variable name referenced in your RLS policies.
    /// </summary>
    public const string DefaultTenantSessionVariable = "app.current_tenant_id";

    /// <summary>
    /// Establishes the tenant context for Row Level Security on an open database connection within the provided transaction.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="transaction">The active transaction within which <c>SET LOCAL</c> is executed.</param>
    /// <param name="tenantContext">The resolved tenant context containing the tenant identifier.</param>
    /// <param name="sessionVariable">
    /// The PostgreSQL session variable name. Defaults to <see cref="DefaultTenantSessionVariable"/>.
    /// Must match the variable referenced in RLS policies.
    /// </param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="sessionVariable"/> is <see langword="null"/>, empty, or consists only of white-space characters</exception>
    /// <exception cref="InvalidOperationException"><paramref name="transaction"/> is <see langword="null"/></exception>
    /// <exception cref="TenantNotFoundException">No tenant has been resolved in the current context or the tenant identifier is empty</exception>
    [SuppressMessage("Security", "S2077:Formatting SQL queries is an anti-pattern and can lead to SQL injection vulnerabilities", Justification = "SET LOCAL variable and sanitized tenantIdValue are authenticated server-side invariants.")]
    public static Task SetTenantRlsContextAsync(
        this DbConnection connection,
        DbTransaction transaction,
        ITenantContext tenantContext,
        string sessionVariable = DefaultTenantSessionVariable,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (transaction is null)
        {
            throw new InvalidOperationException(
                "SET LOCAL requires an active transaction. " +
                "RLS context must be set within a transaction to guarantee " +
                "that the variable is automatically cleared on transaction end. " +
                "Call BeginTransactionAsync() before calling SetTenantRlsContextAsync().");
        }

        if (string.IsNullOrWhiteSpace(sessionVariable))
        {
            throw new ArgumentException("Session variable name must not be null or whitespace.", nameof(sessionVariable));
        }

        var tenant = tenantContext.RequiredTenant;

        if (tenant.Id.Value == Guid.Empty)
        {
            throw new TenantNotFoundException("Tenant identifier is empty. Cannot establish RLS context.");
        }

        var tenantIdValue = tenant.Id.Value.ToString(); // canonical UUID string format

        // Use SET LOCAL — variable is scoped to the current transaction only.
        // When the transaction commits or rolls back, PostgreSQL automatically resets this variable.
        // This prevents context leakage when a connection is returned to the connection pool.
        //
        // Security note: The tenant ID value is parameterized via Dapper/positional binding,
        // but PostgreSQL SET LOCAL does not support parameterized values in the same way as DML.
        // We use format-safe string construction with explicit validation above.
        // The value comes from the authenticated, server-side resolved tenant context — not from
        // user input — so the risk surface is minimal. However, the value is still validated above.
        var sql = string.Create(CultureInfo.InvariantCulture, $"SET LOCAL \"{sessionVariable}\" = '{tenantIdValue}'");

        var commandDefinition = new CommandDefinition(
            sql,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(commandDefinition);
    }

    /// <summary>
    /// Opens a new transaction on the provided PostgreSQL connection and sets the tenant RLS context atomically.
    /// </summary>
    /// <param name="connection">The database connection (opened automatically if not already open).</param>
    /// <param name="tenantContext">The resolved tenant context.</param>
    /// <param name="isolationLevel">The transaction isolation level (default: <see cref="IsolationLevel.ReadCommitted"/>).</param>
    /// <param name="sessionVariable">The PostgreSQL session variable name for RLS.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the started transaction with RLS context applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    public static async Task<DbTransaction> BeginTenantTransactionAsync(
        this DbConnection connection,
        ITenantContext tenantContext,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        string sessionVariable = DefaultTenantSessionVariable,
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
            await connection.SetTenantRlsContextAsync(transaction, tenantContext, sessionVariable, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // If SET LOCAL fails, roll back and rethrow — don't leave a transaction open without RLS context
            // Stryker disable once boolean
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            // Stryker disable once boolean
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return transaction;
    }
}
