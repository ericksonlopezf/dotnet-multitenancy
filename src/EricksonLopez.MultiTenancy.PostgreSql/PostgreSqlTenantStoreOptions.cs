// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy.PostgreSql;

/// <summary>
/// Provides configuration options for <see cref="PostgreSqlTenantStore{TTenant}"/>.
/// </summary>
public sealed class PostgreSqlTenantStoreOptions
{
    /// <summary>
    /// Gets or sets the PostgreSQL database connection string used to connect to the tenant store database.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the database schema name containing the tenants table.
    /// Default is <c>public</c>.
    /// </summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// Gets or sets the database table name storing tenant records.
    /// Default is <c>tenants</c>.
    /// </summary>
    public string TableName { get; set; } = "tenants";

    /// <summary>
    /// Gets or sets the column name for the primary tenant identifier (UUID/Guid).
    /// Default is <c>id</c>.
    /// </summary>
    public string IdColumn { get; set; } = "id";

    /// <summary>
    /// Gets or sets the column name for the tenant human-readable name or lookup identifier.
    /// Default is <c>name</c>.
    /// </summary>
    public string NameColumn { get; set; } = "name";

    /// <summary>
    /// Gets or sets the column name for the tenant-specific connection string.
    /// Default is <c>connection_string</c>.
    /// </summary>
    public string ConnectionStringColumn { get; set; } = "connection_string";

    /// <summary>
    /// Gets or sets the column name indicating whether the tenant is active.
    /// Default is <c>is_active</c>.
    /// </summary>
    public string IsActiveColumn { get; set; } = "is_active";

    /// <summary>
    /// Gets or sets the column name storing additional properties (JSON/JSONB or key-value dictionary).
    /// Default is <c>properties</c>.
    /// </summary>
    public string PropertiesColumn { get; set; } = "properties";

    /// <summary>
    /// Gets or sets an optional custom SQL query for retrieving a tenant by ID.
    /// If null, the query is generated dynamically from the schema, table, and column names.
    /// Must accept an <c>@Id</c> parameter.
    /// </summary>
    public string? CustomSelectByIdQuery { get; set; }

    /// <summary>
    /// Gets or sets an optional custom SQL query for retrieving a tenant by string identifier/name.
    /// If null, the query is generated dynamically from the schema, table, and column names.
    /// Must accept an <c>@Identifier</c> parameter and optional <c>@ParsedId</c> parameter.
    /// </summary>
    public string? CustomSelectByIdentifierQuery { get; set; }
}
