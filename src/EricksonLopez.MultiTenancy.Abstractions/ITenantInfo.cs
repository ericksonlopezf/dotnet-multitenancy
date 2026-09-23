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
