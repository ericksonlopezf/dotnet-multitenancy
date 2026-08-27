// Copyright © Erickson Lopez. MIT License.
using System.Data.Common;

namespace EricksonLopez.MultiTenancy.Sqlite;

/// <summary>
/// Defines a factory for resolving and building tenant-specific SQLite connection strings and connections.
/// </summary>
public interface ISqliteTenantConnectionFactory
{
    /// <summary>
    /// Builds a tenant-specific connection string from a template format.
    /// </summary>
    /// <param name="tenant">The tenant metadata.</param>
    /// <returns>A formatted connection string for the specified tenant.</returns>
    string BuildConnectionString(ITenantInfo tenant);

    /// <summary>
    /// Creates a new <see cref="DbConnection"/> for the specified tenant, ensuring the target database directory exists.
    /// </summary>
    /// <param name="tenant">The tenant metadata.</param>
    /// <returns>An unopened <see cref="DbConnection"/> configured for the tenant.</returns>
    DbConnection CreateConnection(ITenantInfo tenant);
}
