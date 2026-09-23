// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.MultiTenancy.PostgreSql;

/// <summary>
/// Provides diagnostic and validation utilities for PostgreSQL Row Level Security (RLS) policies.
/// </summary>
public static class PostgreSqlRlsDiagnostics
{
    private const string _rlsVerificationSql = """
        SELECT 
            c.relname AS TableName,
            c.relrowsecurity AS HasRlsEnabled,
            c.relforcerowsecurity AS HasForceRlsEnabled
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = @SchemaParam AND c.relkind = 'r';
        """;

    /// <summary>
    /// Validates that all tables with Row Level Security enabled in the specified schema also have
    /// <c>FORCE ROW LEVEL SECURITY</c> enabled, preventing table owners and privileged roles from
    /// accidentally bypassing tenant isolation policies.
    /// </summary>
    /// <param name="connection">The database connection to inspect.</param>
    /// <param name="schemaName">The database schema to inspect (default is <c>"public"</c>).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous validation operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="schemaName"/> is <see langword="null"/>, empty, or consists only of white-space characters</exception>
    /// <exception cref="InvalidOperationException">One or more tables have RLS enabled without <c>FORCE ROW LEVEL SECURITY</c></exception>
    public static async Task ValidateRlsPoliciesAsync(
        DbConnection connection,
        string schemaName = "public",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name must not be null or whitespace.", nameof(schemaName));
        }

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = _rlsVerificationSql;

        var param = cmd.CreateParameter();
        if (param is not null)
        {
            param.ParameterName = "@SchemaParam";
            param.Value = schemaName;
            cmd.Parameters.Add(param);
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var tableName = reader.GetString(0);
            var hasRls = reader.GetBoolean(1);
            var hasForceRls = reader.GetBoolean(2);

            if (hasRls && !hasForceRls)
            {
                throw new InvalidOperationException(
                    $"Table '{tableName}' has Row Level Security enabled but lacks FORCE ROW LEVEL SECURITY. " +
                    $"Execute 'ALTER TABLE {tableName} FORCE ROW LEVEL SECURITY;' to prevent owner bypass.");
            }
        }
    }
}
