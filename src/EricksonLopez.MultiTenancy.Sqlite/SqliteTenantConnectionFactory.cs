// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.IO;
using System.Linq;

namespace EricksonLopez.MultiTenancy.Sqlite;

/// <summary>
/// Provides a factory that resolves per-tenant SQLite connection strings from a parameterized template
/// and ensures that the target database file directories exist before returning a connection.
/// </summary>
public sealed class SqliteTenantConnectionFactory : ISqliteTenantConnectionFactory
{
    private static readonly char[] _invalidFileNameChars =
    [
        .. Path.GetInvalidFileNameChars(),
        '*', '?', ':', '"', '<', '>', '|', '/', '\\'
    ];

    private readonly string _connectionStringTemplate;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteTenantConnectionFactory"/> class.
    /// </summary>
    /// <param name="connectionStringTemplate">
    /// The template connection string. Supported placeholders: <c>{TenantId}</c>, <c>{Name}</c>.
    /// Example: <c>"Data Source=data/{TenantId}.db;"</c>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="connectionStringTemplate"/> is <see langword="null"/>, empty, or consists only of white-space characters</exception>
    public SqliteTenantConnectionFactory(string connectionStringTemplate)
    {
        if (string.IsNullOrWhiteSpace(connectionStringTemplate))
        {
            throw new ArgumentException("Connection string template must not be null or whitespace.", nameof(connectionStringTemplate));
        }

        _connectionStringTemplate = connectionStringTemplate;
    }

    /// <inheritdoc />
    public string BuildConnectionString(ITenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (!string.IsNullOrWhiteSpace(tenant.ConnectionString))
        {
            return tenant.ConnectionString;
        }

        var tenantIdStr = tenant.Id.Value.ToString();
        var connectionString = _connectionStringTemplate
            .Replace("{TenantId}", tenantIdStr, StringComparison.OrdinalIgnoreCase);

        if (_connectionStringTemplate.Contains("{Name}", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(tenant.Name))
            {
                throw new ArgumentException("Tenant name must not be null or whitespace when {Name} placeholder is used in connection string template.", nameof(tenant));
            }

            if (tenant.Name.Contains("..", StringComparison.Ordinal) ||
                tenant.Name.IndexOfAny(_invalidFileNameChars) >= 0)
            {
                throw new ArgumentException(
                    $"Tenant name '{tenant.Name}' contains invalid path characters or directory traversal sequences.",
                    nameof(tenant));
            }

            connectionString = connectionString.Replace("{Name}", tenant.Name, StringComparison.OrdinalIgnoreCase);
        }

        return connectionString;
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Reflection fallback infrastructure for SQLite dynamic driver loading.")]
    public DbConnection CreateConnection(ITenantInfo tenant)
    {
        var connectionString = BuildConnectionString(tenant);

        // Stryker disable once String, equality : Reflection assembly lookup for SqliteConnectionStringBuilder
        var builderType = Type.GetType("Microsoft.Data.Sqlite.SqliteConnectionStringBuilder, Microsoft.Data.Sqlite");
        string? dataSource = null;

        if (builderType != null)
        {
            var builder = Activator.CreateInstance(builderType, connectionString);
            // Stryker disable once String, equality : Property access via reflection
            dataSource = (string?)builderType.GetProperty("DataSource")?.GetValue(builder);
        }
        else
        {
            // Fallback simplistic parsing if the assembly isn't loaded yet
            if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                var parts = connectionString.Split(';');
                var dsPart = parts.FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));
                if (dsPart != null)
                {
                    dataSource = dsPart.Substring("Data Source=".Length);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(dataSource) && dataSource != ":memory:")
        {
            var directory = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        // Stryker disable once String, equality : Reflection assembly lookup for SqliteConnection
        var sqlConnectionType = Type.GetType("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite");
        if (sqlConnectionType != null)
        {
            return (DbConnection)Activator.CreateInstance(sqlConnectionType, connectionString)!;
        }

        throw new InvalidOperationException("Microsoft.Data.Sqlite.SqliteConnection could not be loaded.");
    }
}
