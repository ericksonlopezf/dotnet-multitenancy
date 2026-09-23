// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.MariaDb;
using EricksonLopez.MultiTenancy.MySql;
using EricksonLopez.MultiTenancy.Oracle;
using EricksonLopez.MultiTenancy.PostgreSql;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using EricksonLopez.MultiTenancy.Sqlite;
using EricksonLopez.MultiTenancy.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level04_AdvancedIntegration;

/// <summary>
/// Level 4 — Advanced Integration: Database dialect transaction boundaries and kernel-level isolation.
/// </summary>
public static class AdvancedIntegrationDemo
{
    /// <summary>
    /// Demonstrates configuring a persistent PostgreSQL-backed tenant catalog store.
    /// </summary>
    public static void ConfigurePostgreSqlTenantStore(IServiceCollection services, string connectionString)
    {
        services.AddPostgreSqlTenantStore(options =>
        {
            options.ConnectionString = connectionString;
            options.Schema = "public";
            options.TableName = "tenants";
            options.IdColumn = "id";
            options.NameColumn = "name";
            options.ConnectionStringColumn = "connection_string";
            options.IsActiveColumn = "is_active";
            options.PropertiesColumn = "properties";
        });
    }
    /// <summary>
    /// Demonstrates PostgreSQL Row Level Security (RLS) via transaction-scoped SET LOCAL.
    /// </summary>
    public static async Task ExecutePostgreSqlRlsWorkflowAsync(DbConnection connection, ITenantContext tenantContext, CancellationToken ct = default)
    {
        Console.WriteLine("--- Executing PostgreSQL RLS Transaction Workflow ---");

        // Open transaction AND set RLS context atomically using SET LOCAL
        await using var transaction = await PostgreSqlRlsExtensions.BeginTenantTransactionAsync(
            connection,
            tenantContext,
            IsolationLevel.ReadCommitted,
            PostgreSqlRlsExtensions.DefaultTenantSessionVariable,
            ct);

        // Within this transaction, PostgreSQL RLS policies enforce isolation automatically
        var command = new CommandDefinition("SELECT * FROM invoices", transaction: transaction, cancellationToken: ct);
        _ = await connection.QueryAsync<Invoice>(command);

        await transaction.CommitAsync(ct);
        // Upon commit or rollback, SET LOCAL is automatically cleared by PostgreSQL — zero context leakage!

        // NOTE: If you already have an open transaction from a Unit of Work, use the lower-level API directly:
        // await connection.SetTenantRlsContextAsync(existingTransaction, tenantContext, sessionVariable, ct);
    }

    /// <summary>
    /// Demonstrates Microsoft SQL Server SESSION_CONTEXT and RLS security policies.
    /// </summary>
    public static async Task ExecuteSqlServerSessionContextWorkflowAsync(DbConnection connection, ITenantContext tenantContext, CancellationToken ct = default)
    {
        Console.WriteLine("--- Executing SQL Server SESSION_CONTEXT Workflow ---");

        // Begin transaction with sp_set_session_context
        await using var transaction = await SqlServerSessionContextExtensions.BeginTenantTransactionAsync(
            connection,
            tenantContext,
            IsolationLevel.ReadCommitted,
            SqlServerSessionContextExtensions.DefaultTenantSessionKey,
            readOnly: false,
            cancellationToken: ct);

        var command = new CommandDefinition("SELECT * FROM invoices", transaction: transaction, cancellationToken: ct);
        _ = await connection.QueryAsync<Invoice>(command);

        await transaction.CommitAsync(ct);

        // Reset session context when returning connection to the pool
        await SqlServerSessionContextExtensions.ResetTenantSessionContextAsync(connection, sessionKey: SqlServerSessionContextExtensions.DefaultTenantSessionKey, cancellationToken: ct);

        // NOTE: If you already have an open transaction from a Unit of Work, use the lower-level API directly:
        // await connection.SetTenantSessionContextAsync(tenantContext, existingTx, sessionKey, readOnly: false, ct);
    }

    /// <summary>
    /// Demonstrates MySQL user session variable isolation.
    /// </summary>
    public static async Task ExecuteMySqlSessionVariableWorkflowAsync(DbConnection connection, ITenantContext tenantContext, CancellationToken ct = default)
    {
        Console.WriteLine("--- Executing MySQL Session Variable Workflow ---");

        await using var transaction = await MySqlTenantExtensions.BeginTenantTransactionAsync(
            connection,
            tenantContext,
            IsolationLevel.ReadCommitted,
            MySqlTenantExtensions.DefaultTenantVariableName,
            ct);

        var command = new CommandDefinition("SELECT * FROM invoices", transaction: transaction, cancellationToken: ct);
        _ = await connection.QueryAsync<Invoice>(command);

        await transaction.CommitAsync(ct);

        await MySqlTenantExtensions.ResetTenantSessionVariableAsync(connection, variableName: MySqlTenantExtensions.DefaultTenantVariableName, cancellationToken: ct);

        // NOTE: If you already have an open transaction, use the lower-level API directly:
        // await connection.SetTenantSessionVariableAsync(tenantContext, existingTx, variableName, ct);
    }

    /// <summary>
    /// Demonstrates MariaDB user session variable isolation.
    /// </summary>
    public static async Task ExecuteMariaDbSessionVariableWorkflowAsync(DbConnection connection, ITenantContext tenantContext, CancellationToken ct = default)
    {
        Console.WriteLine("--- Executing MariaDB Session Variable Workflow ---");

        await using var transaction = await MariaDbTenantExtensions.BeginTenantTransactionAsync(
            connection,
            tenantContext,
            IsolationLevel.ReadCommitted,
            MariaDbTenantExtensions.DefaultTenantVariableName,
            ct);

        var command = new CommandDefinition("SELECT * FROM invoices", transaction: transaction, cancellationToken: ct);
        _ = await connection.QueryAsync<Invoice>(command);

        await transaction.CommitAsync(ct);

        await MariaDbTenantExtensions.ResetTenantSessionVariableAsync(connection, variableName: MariaDbTenantExtensions.DefaultTenantVariableName, cancellationToken: ct);

        // NOTE: If you already have an open transaction, use the lower-level API directly:
        // await connection.SetTenantSessionVariableAsync(tenantContext, existingTx, variableName, ct);
    }

    /// <summary>
    /// Demonstrates Oracle Virtual Private Database (VPD) with DBMS_SESSION.SET_IDENTIFIER.
    /// </summary>
    public static async Task ExecuteOracleVpdWorkflowAsync(DbConnection connection, ITenantContext tenantContext, CancellationToken ct = default)
    {
        Console.WriteLine("--- Executing Oracle VPD Workflow ---");

        await using var transaction = await OracleVpdExtensions.BeginTenantTransactionAsync(
            connection,
            tenantContext,
            IsolationLevel.ReadCommitted,
            setClientIdProperty: true,
            cancellationToken: ct);

        var command = new CommandDefinition("SELECT * FROM invoices", transaction: transaction, cancellationToken: ct);
        _ = await connection.QueryAsync<Invoice>(command);

        await transaction.CommitAsync(ct);

        await OracleVpdExtensions.ResetTenantVpdContextAsync(connection, cancellationToken: ct);

        // NOTE: If you already have an open transaction, use the lower-level API directly:
        // await connection.SetTenantVpdContextAsync(existingTx, tenantContext, setClientIdProperty: true, ct);
    }

    /// <summary>
    /// Demonstrates SQLite temporary table session isolation.
    /// </summary>
    public static async Task ExecuteSqliteWorkflowAsync(DbConnection connection, ITenantContext tenantContext, CancellationToken ct = default)
    {
        Console.WriteLine("--- Executing SQLite Temporary Table Workflow ---");

        await using var transaction = await SqliteTenantExtensions.BeginTenantTransactionAsync(
            connection,
            tenantContext,
            IsolationLevel.ReadCommitted,
            ct);

        var command = new CommandDefinition("SELECT * FROM invoices", transaction: transaction, cancellationToken: ct);
        _ = await connection.QueryAsync<Invoice>(command);

        await transaction.CommitAsync(ct);

        await SqliteTenantExtensions.ResetTenantContextAsync(connection, cancellationToken: ct);

        // NOTE: If you already have an open transaction, use the lower-level API directly:
        // await connection.SetTenantContextAsync(tenantContext, existingTx, ct);
    }
}
