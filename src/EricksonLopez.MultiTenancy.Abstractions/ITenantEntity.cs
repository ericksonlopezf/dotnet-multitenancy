// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Defines an entity that is partitioned by a tenant.
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// Gets the strongly-typed identifier of the owning tenant.
    /// </summary>
    TenantId TenantId { get; }
}
