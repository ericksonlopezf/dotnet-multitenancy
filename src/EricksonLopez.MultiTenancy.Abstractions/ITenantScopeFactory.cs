// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Defines a factory for creating isolated tenant execution scopes.
/// </summary>
public interface ITenantScopeFactory
{
    /// <summary>
    /// Creates an isolated dependency injection scope bound to the specified tenant metadata.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for disposing the scope when the operation completes.
    /// Each scope is fully isolated; no tenant state is shared between scopes.
    /// </remarks>
    /// <param name="tenant">The tenant metadata to bind to the new scope.</param>
    /// <param name="source">The resolution source identifying how the tenant was determined.</param>
    /// <returns>An isolated <see cref="ITenantScope"/> instance bound to the specified tenant.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tenant"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="tenant"/> has an empty identifier (<see cref="TenantId.Empty"/>)</exception>
    ITenantScope CreateScope(ITenantInfo tenant, TenantResolutionSource source = TenantResolutionSource.ExplicitScope);
}
