// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy.Stores;

/// <summary>
/// Provides configuration options for <see cref="CachedTenantStore{TTenant}"/>.
/// </summary>
public sealed class CachedTenantStoreOptions
{
    /// <summary>
    /// Gets or sets the maximum duration a tenant cache entry remains valid relative to its insertion time.
    /// Defaults to five minutes.
    /// </summary>
    public TimeSpan AbsoluteExpirationRelativeToNow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the duration a tenant cache entry remains valid after its last access.
    /// When <see langword="null"/>, sliding expiration is disabled.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration a failed/missing tenant lookup is retained in cache to prevent cache stampedes and DoS attacks.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan NegativeCacheExpirationRelativeToNow { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether negative lookup results (tenant not found or inactive) are cached.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool EnableNegativeCaching { get; set; } = true;
}
