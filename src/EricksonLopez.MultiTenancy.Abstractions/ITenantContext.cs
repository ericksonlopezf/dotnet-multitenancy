// Copyright © Erickson Lopez. MIT License.
using System.Diagnostics.CodeAnalysis;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides access to the ambient tenant context for the current execution scope.
/// </summary>
/// <remarks>
/// The context is immutable after resolution. Reassignment within the same scope is not permitted.
/// </remarks>
public interface ITenantContext
{
    /// <summary>
    /// Gets the resolved tenant metadata for the current scope, or <see langword="null"/> if unassigned.
    /// </summary>
    ITenantInfo? Tenant { get; }

    /// <summary>
    /// Gets a value indicating whether a valid, active tenant has been resolved for the current scope.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="true"/> only when all three conditions are met:
    /// <list type="bullet">
    ///   <item><description><see cref="Tenant"/> is not <see langword="null"/>.</description></item>
    ///   <item><description>The tenant identifier is not <see cref="TenantId.Empty"/>.</description></item>
    ///   <item><description><see cref="ITenantInfo.IsActive"/> is <see langword="true"/>.</description></item>
    /// </list>
    /// An inactive tenant yields <see langword="false"/> even when the tenant record exists in the store.
    /// </remarks>
    [MemberNotNullWhen(true, nameof(Tenant))]
    bool IsResolved { get; }

    /// <summary>
    /// Gets the source mechanism through which this tenant was resolved.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="TenantResolutionSource.None"/> if no tenant is resolved.
    /// Used for audit trails and security event logging.
    /// </remarks>
    TenantResolutionSource Source { get; }

    /// <summary>
    /// Gets the required resolved tenant metadata.
    /// </summary>
    /// <exception cref="TenantNotFoundException">No tenant has been resolved in the current ambient execution context</exception>
    [SuppressMessage("Major Code Smell", "S2372:Exceptions should not be thrown from property getters", Justification = "RequiredTenant explicitly guarantees fail-fast invariant semantics when no tenant is resolved.")]
    ITenantInfo RequiredTenant
    {
        get
        {
            if (!IsResolved || Tenant is null)
            {
                throw new TenantNotFoundException("No tenant has been resolved in the current ambient execution context.");
            }

            return Tenant;
        }
    }
}

/// <summary>
/// Provides access to strongly-typed tenant metadata in a customized multi-tenant model.
/// </summary>
/// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
public interface ITenantContext<out TTenant> : ITenantContext
    where TTenant : class, ITenantInfo
{
    /// <summary>
    /// Gets the strongly-typed resolved tenant metadata.
    /// </summary>
    new TTenant? Tenant { get; }
}

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
