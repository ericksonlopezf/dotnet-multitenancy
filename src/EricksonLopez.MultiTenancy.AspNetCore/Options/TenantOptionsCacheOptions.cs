// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.MultiTenancy.AspNetCore.Options;

/// <summary>
/// Provides configuration options for <see cref="TenantOptionsCache{TOptions, TTenant}"/>.
/// </summary>
public sealed class TenantOptionsCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum number of cached tenant options instances before LRU eviction triggers.
    /// Default is 10,000.
    /// </summary>
    public int MaxCapacity { get; set; } = 10000;

    /// <summary>
    /// Gets or sets a value indicating whether to throw <see cref="TenantNotFoundException"/>
    /// when resolving options for an un-scoped or un-resolved tenant context, instead of
    /// falling back to a shared global options cache key. Default is <see langword="false"/>.
    /// </summary>
    public bool ThrowOnMissingTenant { get; set; }
}
