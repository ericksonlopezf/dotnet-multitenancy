// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.MultiTenancy.MySql;

/// <summary>
/// Provides MySQL session variable and transaction-scoped context integration for multi-tenant data access.
/// Sets a connection/session user variable (e.g. <c>@app_tenant_id</c>) to allow multi-tenant views, stored routines,
/// triggers, and queries to enforce tenant boundaries.
/// </summary>
/// <remarks>
/// <para>
/// Security model: MySQL supports user-defined session variables prefixed with <c>@</c>.
/// Views and queries can filter against the variable:
/// <code>
/// CREATE VIEW v_invoices AS
/// SELECT * FROM invoices
/// WHERE tenant_id = @app_tenant_id;
/// </code>
/// </para>
/// <para>
/// Connection pooling notice: Because MySQL user variables are connection-scoped, <see cref="ResetTenantSessionVariableAsync"/>
/// or proper transaction boundaries should be used when returning connections to the pool.
/// </para>
/// </remarks>
public static partial class MySqlTenantExtensions
{
    [GeneratedRegex(@"^[a-zA-Z0-9_]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SafeIdentifierRegex();

    /// <summary>
    /// Specifies the default MySQL session variable name used to store the tenant identifier.
    /// </summary>
    public const string DefaultTenantVariableName = "app_tenant_id";

    /// <summary>
    /// Establishes the tenant context in MySQL by setting a session user variable.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="transaction">The active MySQL transaction.</param>
    /// <param name="tenantContext">The resolved tenant context containing the tenant identifier.</param>
    /// <param name="variableName">The user variable name (without the '@' prefix). Defaults to <see cref="DefaultTenantVariableName"/>.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="variableName"/> is <see langword="null"/>, empty, or contains invalid characters</exception>
    /// <exception cref="InvalidOperationException"><paramref name="transaction"/> is <see langword="null"/></exception>
    /// <exception cref="TenantNotFoundException">No tenant has been resolved in the current context or the tenant identifier is empty</exception>
    [SuppressMessage("Security", "S2077:Formatting SQL queries is an anti-pattern and can lead to SQL injection vulnerabilities", Justification = "Variable name is validated and cannot be parameterized in MySQL SET statement syntax; the value is safely parameterized via @Value.")]
    public static Task SetTenantSessionVariableAsync(
        this DbConnection connection,
        DbTransaction transaction,
        ITenantContext tenantContext,
        string variableName = DefaultTenantVariableName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (transaction is null)
        {
            throw new InvalidOperationException(
                "Setting MySQL session variable requires an active transaction to prevent context leakage across connection pool reuse. " +
                "Call BeginTenantTransactionAsync() or BeginTransactionAsync() before calling SetTenantSessionVariableAsync().");
        }

        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException("Variable name must not be null or whitespace.", nameof(variableName));
        }

        var cleanVarName = variableName.TrimStart('@');
        if (string.IsNullOrWhiteSpace(cleanVarName) || !SafeIdentifierRegex().IsMatch(cleanVarName))
        {
            throw new ArgumentException($"Invalid MySQL variable name: '{variableName}'. Only alphanumeric characters and underscores are permitted.", nameof(variableName));
        }

        var tenant = tenantContext.RequiredTenant;
        var tenantIdValue = tenant.Id.Value;

        if (tenantIdValue == Guid.Empty)
        {
            throw new TenantNotFoundException("Tenant identifier is empty. Cannot establish MySQL tenant session variable.");
        }

        var sql = $"SET @{cleanVarName} = @Value;";

        var parameters = new DynamicParameters();
        parameters.Add("Value", tenantIdValue.ToString(), DbType.String);

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }

    /// <summary>
    /// Resets the MySQL tenant user variable to <see langword="null"/> to prevent context leakage across connection pool reuse.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="transaction">The optional active MySQL transaction.</param>
    /// <param name="variableName">The user variable name (without the '@' prefix). Defaults to <see cref="DefaultTenantVariableName"/>.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="variableName"/> is <see langword="null"/>, empty, or consists only of white-space characters</exception>
    [SuppressMessage("Security", "S2077:Formatting SQL queries is an anti-pattern and can lead to SQL injection vulnerabilities", Justification = "Variable name is validated and cannot be parameterized in MySQL SET statement syntax; the value is set to literal NULL.")]
    public static Task ResetTenantSessionVariableAsync(
        this DbConnection connection,
        DbTransaction? transaction = null,
        string variableName = DefaultTenantVariableName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException("Variable name must not be null or whitespace.", nameof(variableName));
        }

        var cleanVarName = variableName.TrimStart('@');
        if (string.IsNullOrWhiteSpace(cleanVarName) || !SafeIdentifierRegex().IsMatch(cleanVarName))
        {
            throw new ArgumentException($"Invalid MySQL variable name: '{variableName}'. Only alphanumeric characters and underscores are permitted.", nameof(variableName));
        }

        var sql = $"SET @{cleanVarName} = NULL;";

        var command = new CommandDefinition(
            sql,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }

    /// <summary>
    /// Synchronously resets the MySQL user variable holding the tenant identifier to <c>NULL</c> to prevent context leakage across connection pool reuse.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="transaction">The optional active MySQL transaction.</param>
    /// <param name="variableName">The user variable name (without the '@' prefix). Defaults to <see cref="DefaultTenantVariableName"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="variableName"/> is <see langword="null"/>, empty, or consists only of white-space characters</exception>
    [SuppressMessage("Security", "S2077:Formatting SQL queries is an anti-pattern and can lead to SQL injection vulnerabilities", Justification = "Variable name is validated and cannot be parameterized in MySQL SET statement syntax; the value is set to literal NULL.")]
    public static void ResetTenantSessionVariable(
        this DbConnection connection,
        DbTransaction? transaction = null,
        string variableName = DefaultTenantVariableName)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException("Variable name must not be null or whitespace.", nameof(variableName));
        }

        var cleanVarName = variableName.TrimStart('@');
        if (string.IsNullOrWhiteSpace(cleanVarName) || !SafeIdentifierRegex().IsMatch(cleanVarName))
        {
            throw new ArgumentException($"Invalid MySQL variable name: '{variableName}'. Only alphanumeric characters and underscores are permitted.", nameof(variableName));
        }

        var sql = $"SET @{cleanVarName} = NULL;";

        var command = new CommandDefinition(
            sql,
            transaction: transaction);

        connection.Execute(command);
    }

    /// <summary>
    /// Opens a new transaction on the provided MySQL connection and sets the tenant session variable atomically.
    /// </summary>
    /// <param name="connection">The database connection (opened automatically if not already open).</param>
    /// <param name="tenantContext">The resolved tenant context.</param>
    /// <param name="isolationLevel">The transaction isolation level (default: <see cref="IsolationLevel.ReadCommitted"/>).</param>
    /// <param name="variableName">The user variable name (without the '@' prefix).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the started transaction with tenant context applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="tenantContext"/> is <see langword="null"/></exception>
    public static async Task<DbTransaction> BeginTenantTransactionAsync(
        this DbConnection connection,
        ITenantContext tenantContext,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        string variableName = DefaultTenantVariableName,
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
        var transaction = new MySqlTenantSessionTransaction(
            rawTransaction,
            connection,
            conn => conn.ResetTenantSessionVariableAsync(variableName: variableName, cancellationToken: cancellationToken),
            conn => conn.ResetTenantSessionVariable(variableName: variableName));

        try
        {
            // Stryker disable once boolean
            await connection.SetTenantSessionVariableAsync(transaction, tenantContext, variableName, cancellationToken)
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
