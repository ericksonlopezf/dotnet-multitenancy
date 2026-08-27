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

/// <summary>
/// Represents an isolated execution scope bound to a specific tenant context.
/// </summary>
/// <remarks>
/// This instance must be disposed when the operation completes to release scoped resources.
/// </remarks>
public interface ITenantScope : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the scoped <see cref="IServiceProvider"/> configured for the tenant context.
    /// </summary>
    /// <remarks>
    /// All services resolved from this provider have access to the bound tenant context.
    /// </remarks>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the immutable tenant context associated with this scope.
    /// </summary>
    ITenantContext TenantContext { get; }
}
