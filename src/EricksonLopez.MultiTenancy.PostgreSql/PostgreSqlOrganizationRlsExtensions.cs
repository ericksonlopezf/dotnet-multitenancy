// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.MultiTenancy.PostgreSql;

/// <summary>
/// Provides advanced PostgreSQL Row Level Security (RLS) integration for compound organization hierarchies (Tenant + Company + Branch).
/// Enforces transaction-local GUC variables (<c>is_local = true</c>) to guarantee total connection pool isolation.
/// </summary>
public static partial class PostgreSqlOrganizationRlsExtensions
{
    [GeneratedRegex(@"^[a-zA-Z0-9_.]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SafeIdentifierRegex();
    private static readonly OrganizationRlsOptions _defaultOptions = new();

    private static void ValidateIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !SafeIdentifierRegex().IsMatch(name))
        {
            throw new ArgumentException($"Invalid session variable name: '{name}'. Only alphanumeric characters, underscores, and dots are permitted.", nameof(name));
        }
    }

    /// <summary>
    /// Establishes the full compound organization context (Tenant, Company, Branches) for Row Level Security on an active transaction.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="transaction">The active transaction within which <c>set_config(..., true)</c> is executed.</param>
    /// <param name="context">The organization context containing hierarchy identifiers.</param>
    /// <param name="options">Optional custom RLS session variable options.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="context"/> is <see langword="null"/></exception>
    /// <exception cref="InvalidOperationException"><paramref name="transaction"/> is <see langword="null"/></exception>
    /// <exception cref="TenantNotFoundException"><paramref name="context"/> is unresolved or has an empty tenant identifier</exception>
    [SuppressMessage("Security", "S2077:Formatting SQL queries is an anti-pattern", Justification = "Session variable names come from server configuration, values are strictly parameterized.")]
    public static async Task SetOrganizationRlsContextAsync(
        this DbConnection connection,
        DbTransaction transaction,
        IOrganizationContext context,
        OrganizationRlsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        if (transaction is null)
        {
            throw new InvalidOperationException(
                "PostgreSQL RLS context must be established inside an active transaction (is_local = true) to prevent connection pool contamination.");
        }

        if (!context.IsResolved || context.Tenant is null || context.Tenant.Id.Value == Guid.Empty)
        {
            throw new TenantNotFoundException("Cannot establish PostgreSQL Organization RLS context because the tenant context is unresolved or empty.");
        }

        var opts = options ?? _defaultOptions;
        ValidateIdentifier(opts.TenantSessionVariable);
        ValidateIdentifier(opts.CompanySessionVariable);
        ValidateIdentifier(opts.AllBranchesSessionVariable);
        ValidateIdentifier(opts.BranchIdsSessionVariable);

        // 1. Tenant Isolation
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"SELECT set_config('{opts.TenantSessionVariable}', @TenantId, true)",
                new { TenantId = context.Tenant.Id.Value.ToString() },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        // 2. Company Isolation
        if (context.HasCompanyContext && context.CompanyId.HasValue)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    $"SELECT set_config('{opts.CompanySessionVariable}', @CompanyId, true)",
                    new { CompanyId = context.CompanyId.Value.ToString() },
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        // 3. Branch Isolation (1 or N branches)
        var allBranches = context.AllBranchesAllowed;
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"SELECT set_config('{opts.AllBranchesSessionVariable}', @AllBranches, true)",
                new { AllBranches = allBranches ? "true" : "false" },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (!allBranches && context.AllowedBranchIds is not null && context.AllowedBranchIds.Count > 0)
        {
            var branchList = string.Join(",", context.AllowedBranchIds.Select(b => b.ToString()));
            await connection.ExecuteAsync(
                new CommandDefinition(
                    $"SELECT set_config('{opts.BranchIdsSessionVariable}', @BranchIds, true)",
                    new { BranchIds = branchList },
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        // 4. Statement Timeout
        if (opts.StatementTimeout > TimeSpan.Zero)
        {
            var timeoutSeconds = Convert.ToInt32(opts.StatementTimeout.TotalSeconds);
            await connection.ExecuteAsync(
                new CommandDefinition(
                    $"SET LOCAL statement_timeout = '{timeoutSeconds}s'",
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens a new transaction on the PostgreSQL connection and establishes full organization RLS context atomically.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="context">The resolved organization context.</param>
    /// <param name="isolationLevel">The transaction isolation level.</param>
    /// <param name="options">Optional custom RLS session variable options.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The opened transaction with RLS context applied.</returns>
    public static async Task<DbTransaction> BeginEnterpriseTransactionAsync(
        this DbConnection connection,
        IOrganizationContext context,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        OrganizationRlsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);

        try
        {
            await connection.SetOrganizationRlsContextAsync(transaction, context, options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return transaction;
    }
}
