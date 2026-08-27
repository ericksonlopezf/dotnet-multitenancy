// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Defines the fundamental contract for tenant metadata in a multi-tenant system.
/// </summary>
public interface ITenantInfo
{
    /// <summary>
    /// Gets the unique strongly-typed identifier of the tenant.
    /// </summary>
    TenantId Id { get; }

    /// <summary>
    /// Gets the human-readable display name of the tenant.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the optional tenant-specific database connection string.
    /// </summary>
    /// <remarks>
    /// Required only for database-per-tenant deployment models. In shared-database and row-level security deployments, this is typically <see langword="null"/>.
    /// </remarks>
    string? ConnectionString { get; }

    /// <summary>
    /// Gets a value indicating whether the tenant is currently active and permitted to process requests.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets custom tenant configuration properties.
    /// </summary>
    IReadOnlyDictionary<string, string> Properties { get; }
}

/// <summary>
/// Represents standard tenant metadata in a multi-tenant system.
/// </summary>
public record class TenantInfo : ITenantInfo
{
    /// <inheritdoc />
    public TenantId Id { get; init; }

    /// <inheritdoc />
    public string Name { get; init; } = string.Empty;

    /// <inheritdoc />
    public string? ConnectionString { get; init; }

    /// <inheritdoc />
    public bool IsActive { get; init; } = true;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantInfo"/> class with default values.
    /// </summary>
    public TenantInfo() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantInfo"/> class with the specified metadata.
    /// </summary>
    /// <param name="id">The unique tenant identifier.</param>
    /// <param name="name">The tenant display name.</param>
    /// <param name="connectionString">The optional database connection string for database-per-tenant deployments.</param>
    /// <param name="isActive">A value indicating whether the tenant is active.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/></exception>
    public TenantInfo(TenantId id, string name, string? connectionString = null, bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(name);
        Id = id;
        Name = name;
        ConnectionString = connectionString;
        IsActive = isActive;
    }
}
