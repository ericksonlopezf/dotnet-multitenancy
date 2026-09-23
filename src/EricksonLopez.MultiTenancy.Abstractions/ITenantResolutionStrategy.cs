// Copyright © Erickson Lopez. MIT License.
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Defines a strategy for resolving a tenant identifier from the ambient execution context.
/// </summary>
public interface ITenantResolutionStrategy
{
    /// <summary>
    /// Gets the human-readable name of this resolution strategy.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Gets the resolution source type associated with this strategy.
    /// </summary>
    TenantResolutionSource Source => TenantResolutionSource.None;

    /// <summary>
    /// Resolves the raw tenant identifier from the ambient context.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task representing the asynchronous operation. The task result contains a <see cref="Result{T}"/> with the resolved <see cref="TenantId"/> if successful, or an error result.</returns>
    ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default);
}
