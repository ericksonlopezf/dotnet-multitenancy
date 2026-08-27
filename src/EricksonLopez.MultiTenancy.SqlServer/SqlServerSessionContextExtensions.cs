// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace EricksonLopez.MultiTenancy.SqlServer;

/// <summary>
/// Provides Microsoft SQL Server <c>SESSION_CONTEXT</c> and Row-Level Security (RLS) integration for multi-tenant data access.
/// Sets the tenant identifier in SQL Server's session context using <c>sp_set_session_context</c>, allowing Security Policies
/// (<c>FILTER PREDICATE</c> and <c>BLOCK PREDICATE</c>) and functions using <c>SESSION_CONTEXT(N'TenantId')</c> to enforce data isolation.
/// </summary>
/// <remarks>
/// <para>
/// Security model: SQL Server stores key-value pairs in session memory accessible via <c>SESSION_CONTEXT(N'key')</c>.
/// When combined with SQL Server Row-Level Security predicates, queries are automatically filtered at the database engine level.
/// Example security predicate:
/// <code>
/// CREATE FUNCTION dbo.fn_tenant_security_predicate(@TenantId UNIQUEIDENTIFIER)
/// RETURNS TABLE
/// WITH SCHEMABINDING
/// AS
/// RETURN SELECT 1 AS fn_securitypredicate_result
/// WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER);
///
/// CREATE SECURITY POLICY dbo.TenantSecurityPolicy
/// ADD FILTER PREDICATE dbo.fn_tenant_security_predicate(TenantId) ON dbo.Invoices,
/// ADD BLOCK PREDICATE dbo.fn_tenant_security_predicate(TenantId) ON dbo.Invoices AFTER INSERT;
/// </code>
/// </para>
/// </remarks>
public static class SqlServerSessionContextExtensions
{
    /// <summary>
    /// The default SQL Server session context key used to store the tenant identifier.
    /// </summary>
    public const string DefaultTenantSessionKey = "TenantId";

    /// <summary>
    /// Establishes the tenant context in SQL Server's session context by executing <c>sp_set_session_context</c>.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="tenantContext">The resolved tenant context containing the tenant identifier.</param>
    /// <param name="transaction">The optional active SQL Server transaction.</param>
    /// <param name="sessionKey">The session context key name. Defaults to <see cref="DefaultTenantSessionKey"/>.</param>
    /// <param name="readOnly">
    /// If <see langword="true"/>, the value cannot be modified again until the connection is closed.
    /// Use <see langword="false"/> when working with connection pools where the context must be reset upon completion.
    /// </param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="sessionKey"/> is <see langword="null"/>, empty, or consists only of white-space characters</exception>
    /// <exception cref="TenantNotFoundException">No tenant has been resolved in the current context or the tenant identifier is empty</exception>
    public static Task SetTenantSessionContextAsync(
        this DbConnection connection,
        ITenantContext tenantContext,
        DbTransaction? transaction = null,
        string sessionKey = DefaultTenantSessionKey,
        bool readOnly = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            throw new ArgumentException("Session key name must not be null or whitespace.", nameof(sessionKey));
        }

        var tenant = tenantContext.RequiredTenant;
        var tenantIdValue = tenant.Id.Value;

        if (tenantIdValue == Guid.Empty)
        {
            throw new TenantNotFoundException("Tenant identifier is empty. Cannot establish SQL Server session context.");
        }

        const string sql = "EXEC sp_set_session_context @key = @Key, @value = @Value, @read_only = @ReadOnly;";

        var parameters = new DynamicParameters();
        parameters.Add("Key", sessionKey, DbType.String);
        parameters.Add("Value", tenantIdValue, DbType.Guid);
        parameters.Add("ReadOnly", readOnly ? 1 : 0, DbType.Int32);

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }

    /// <summary>
    /// Resets the tenant session context key to <see langword="null"/> to prevent context leakage across connection pool reuse.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="transaction">The optional active SQL Server transaction.</param>
    /// <param name="sessionKey">The session context key name. Defaults to <see cref="DefaultTenantSessionKey"/>.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="sessionKey"/> is <see langword="null"/>, empty, or consists only of white-space characters</exception>
    public static Task ResetTenantSessionContextAsync(
        this DbConnection connection,
        DbTransaction? transaction = null,
        string sessionKey = DefaultTenantSessionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            throw new ArgumentException("Session key name must not be null or whitespace.", nameof(sessionKey));
        }

        const string sql = "EXEC sp_set_session_context @key = @Key, @value = NULL, @read_only = 0;";

        var parameters = new DynamicParameters();
        parameters.Add("Key", sessionKey, DbType.String);

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }

    /// <summary>
    /// Opens a new transaction on the provided SQL Server connection and sets the tenant session context atomically.
    /// </summary>
    /// <param name="connection">The database connection (opened automatically if not already open).</param>
    /// <param name="tenantContext">The resolved tenant context.</param>
    /// <param name="isolationLevel">The transaction isolation level (default: <see cref="IsolationLevel.ReadCommitted"/>).</param>
    /// <param name="sessionKey">The session context key name.</param>
    /// <param name="readOnly">Whether the session context key should be marked read-only for the remainder of the connection.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the started transaction with tenant context applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    public static async Task<DbTransaction> BeginTenantTransactionAsync(
        this DbConnection connection,
        ITenantContext tenantContext,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        string sessionKey = DefaultTenantSessionKey,
        bool readOnly = false,
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
            await connection.SetTenantSessionContextAsync(tenantContext, transaction, sessionKey, readOnly, cancellationToken)
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
