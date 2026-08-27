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
}
