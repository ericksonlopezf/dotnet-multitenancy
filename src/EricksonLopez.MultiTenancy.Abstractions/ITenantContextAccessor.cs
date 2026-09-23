// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides a mechanism to set the ambient tenant context exactly once per scope.
/// </summary>
public interface ITenantContextAccessor
{
    /// <summary>
    /// Gets or sets the current ambient tenant context.
    /// </summary>
    /// <remarks>
    /// The setter may only be called once per scope lifetime.
    /// Subsequent set attempts will throw a <see cref="System.InvalidOperationException"/>.
    /// </remarks>
    ITenantContext? TenantContext { get; set; }
}
