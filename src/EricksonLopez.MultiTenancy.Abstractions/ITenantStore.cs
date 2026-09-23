// Copyright © Erickson Lopez. MIT License.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy;

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

    /// <summary>
    /// Streams all tenants available in this store asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous stream of <see cref="ITenantInfo"/> metadata records.</returns>
    /// <remarks>
    /// The default interface implementation yields an empty sequence.
    /// Implementors that require non-empty streaming must override this method.
    /// </remarks>
    // Stryker disable block, statement : Default interface implementation empty stream
    async IAsyncEnumerable<ITenantInfo> GetAllStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
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

    /// <summary>
    /// Streams all strongly-typed tenants available in this store asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous stream of <typeparamref name="TTenant"/> metadata records.</returns>
    /// <remarks>
    /// The default interface implementation yields an empty sequence.
    /// Implementors that require non-empty streaming must override this method.
    /// </remarks>
    // Stryker disable block, statement : Default interface implementation empty stream
    new async IAsyncEnumerable<TTenant> GetAllStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}
