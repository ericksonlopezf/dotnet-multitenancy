// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Common;
using Dapper;

namespace EricksonLopez.MultiTenancy.Oracle;

/// <summary>
/// Provides Oracle Virtual Private Database (VPD) and Fine-Grained Access Control (FGAC) integration for multi-tenant data access.
/// Sets the client identifier via <c>DBMS_SESSION.SET_IDENTIFIER</c> and <c>ClientId</c>, enabling
/// Oracle Row-Level Security policies (<c>DBMS_RLS.ADD_POLICY</c>) referencing <c>SYS_CONTEXT('USERENV', 'CLIENT_IDENTIFIER')</c>
/// to enforce data isolation at the database kernel level.
/// </summary>
/// <remarks>
/// <para>
/// Security model: Oracle VPD attaches a policy function to tables/views:
/// <code>
/// CREATE OR REPLACE FUNCTION get_tenant_predicate(p_schema VARCHAR2, p_obj VARCHAR2)
/// RETURN VARCHAR2 AS
/// BEGIN
///   RETURN 'tenant_id = SYS_CONTEXT(''USERENV'', ''CLIENT_IDENTIFIER'')';
/// END;
/// /
///
/// BEGIN
///   DBMS_RLS.ADD_POLICY(
///     object_name => 'INVOICES',
///     policy_name => 'TENANT_ISOLATION_POLICY',
///     policy_function => 'GET_TENANT_PREDICATE',
///     statement_types => 'SELECT,INSERT,UPDATE,DELETE'
///   );
/// END;
/// /
/// </code>
/// </para>
/// </remarks>
public static class OracleVpdExtensions
{
    /// <summary>
    /// Establishes the tenant context in Oracle using <c>DBMS_SESSION.SET_IDENTIFIER</c> and <c>ClientId</c>.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="transaction">The active Oracle transaction.</param>
    /// <param name="tenantContext">The resolved tenant context containing the tenant identifier.</param>
    /// <param name="setClientIdProperty">
    /// If <see langword="true"/>, also assigns <c>ClientId</c> on the connection object (default: <see langword="true"/>).
    /// </param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    /// <exception cref="InvalidOperationException"><paramref name="transaction"/> is <see langword="null"/></exception>
    /// <exception cref="TenantNotFoundException">No tenant has been resolved in the current context or the tenant identifier is empty</exception>
    public static Task SetTenantVpdContextAsync(
        this DbConnection connection,
        DbTransaction transaction,
        ITenantContext tenantContext,
        bool setClientIdProperty = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (transaction is null)
        {
            throw new InvalidOperationException(
                "Setting Oracle VPD context requires an active transaction to prevent context leakage across connection pool reuse. " +
                "Call BeginTenantTransactionAsync() or BeginTransactionAsync() before calling SetTenantVpdContextAsync().");
        }

        var tenant = tenantContext.RequiredTenant;
        var tenantIdValue = tenant.Id.Value;

        if (tenantIdValue == Guid.Empty)
        {
            throw new TenantNotFoundException("Tenant identifier is empty. Cannot establish Oracle VPD tenant context.");
        }

        var tenantIdStr = tenantIdValue.ToString();

        if (setClientIdProperty)
        {
            // Stryker disable once block : Concrete OracleConnection driver assignment
            if (connection is global::Oracle.ManagedDataAccess.Client.OracleConnection oracleConn)
            {
                oracleConn.ClientId = tenantIdStr;
            }
            else
            {
#pragma warning disable IL2075
                // Stryker disable once String
                var property = connection.GetType().GetProperty("ClientId");
                // Stryker disable once Logical
                if (property != null && property.CanWrite)
                {
                    property.SetValue(connection, tenantIdStr);
                }
#pragma warning restore IL2075
            }
        }

        const string sql = "BEGIN DBMS_SESSION.SET_IDENTIFIER(:tenantId); END;";

        var parameters = new DynamicParameters();
        parameters.Add("tenantId", tenantIdStr, DbType.String);

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }

    /// <summary>
    /// Clears the Oracle client identifier using <c>DBMS_SESSION.CLEAR_IDENTIFIER</c> and resets <c>ClientId</c>
    /// to prevent context leakage across connection pool reuse.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="transaction">The optional active Oracle transaction.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/></exception>
    public static Task ResetTenantVpdContextAsync(
        this DbConnection connection,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

#pragma warning disable IL2075
        // Stryker disable once String
        var property = connection.GetType().GetProperty("ClientId");
        // Stryker disable once Logical
        if (property != null && property.CanWrite)
        {
            property.SetValue(connection, string.Empty);
        }
#pragma warning restore IL2075

        const string sql = "BEGIN DBMS_SESSION.CLEAR_IDENTIFIER; END;";

        var command = new CommandDefinition(
            sql,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }

    /// <summary>
    /// Synchronously resets the tenant Oracle VPD context and clears the client identifier session state.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="transaction">The optional active Oracle transaction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/></exception>
    public static void ResetTenantVpdContext(
        this DbConnection connection,
        DbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

#pragma warning disable IL2075
        // Stryker disable once String
        var property = connection.GetType().GetProperty("ClientId");
        // Stryker disable once Logical
        if (property != null && property.CanWrite)
        {
            property.SetValue(connection, string.Empty);
        }
#pragma warning restore IL2075

        const string sql = "BEGIN DBMS_SESSION.CLEAR_IDENTIFIER; END;";

        var command = new CommandDefinition(
            sql,
            transaction: transaction);

        connection.Execute(command);
    }

    /// <summary>
    /// Opens a new transaction on the provided Oracle connection and sets the tenant VPD context atomically.
    /// </summary>
    /// <param name="connection">The database connection (opened automatically if not already open).</param>
    /// <param name="tenantContext">The resolved tenant context.</param>
    /// <param name="isolationLevel">The transaction isolation level (default: <see cref="IsolationLevel.ReadCommitted"/>).</param>
    /// <param name="setClientIdProperty">Whether to assign <c>ClientId</c> on the connection object.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the started transaction with tenant context applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    public static async Task<DbTransaction> BeginTenantTransactionAsync(
        this DbConnection connection,
        ITenantContext tenantContext,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        bool setClientIdProperty = true,
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
        var rawTransaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
        var transaction = new OracleTenantSessionTransaction(
            rawTransaction,
            connection,
            conn => conn.ResetTenantVpdContextAsync(cancellationToken: cancellationToken),
            conn => conn.ResetTenantVpdContext());

        try
        {
            // Stryker disable once boolean
            await connection.SetTenantVpdContextAsync(transaction, tenantContext, setClientIdProperty, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            try
            {
                // Stryker disable once boolean
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Ignore rollback exceptions if the connection is already dead
            }
            finally
            {
                // Stryker disable once boolean
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }

        return transaction;
    }
}
