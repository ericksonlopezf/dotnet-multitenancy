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
    /// Resolves the raw tenant identifier from the ambient context.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task representing the asynchronous operation. The task result contains a <see cref="Result{T}"/> with the resolved <see cref="TenantId"/> if successful, or an error result.</returns>
    ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a store for retrieving tenant metadata by tenant identifier.
/// </summary>
public interface ITenantStore
{
    /// <summary>
    /// Retrieves tenant metadata for the specified identifier.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant to retrieve.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see cref="Result{T}"/> containing the matching <see cref="ITenantInfo"/> if found, or an error result.</returns>
    Task<Result<ITenantInfo>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a store for retrieving strongly-typed tenant metadata by tenant identifier.
/// </summary>
/// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
public interface ITenantStore<TTenant> : ITenantStore
    where TTenant : class, ITenantInfo
{
    /// <summary>
    /// Retrieves strongly-typed tenant metadata for the specified identifier.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant to retrieve.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see cref="Result{T}"/> containing the typed tenant metadata if found, or an error result.</returns>
    new Task<Result<TTenant>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}

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
