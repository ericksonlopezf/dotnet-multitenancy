// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy;

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
