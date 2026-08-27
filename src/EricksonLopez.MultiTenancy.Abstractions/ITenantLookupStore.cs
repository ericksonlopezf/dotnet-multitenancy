// Copyright © Erickson Lopez. MIT License.
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Defines a store for retrieving tenant metadata by alternative identifiers, such as names or subdomains.
/// </summary>
public interface ITenantLookupStore : ITenantStore
{
    /// <summary>
    /// Retrieves tenant metadata for the specified string identifier.
    /// </summary>
    /// <param name="identifier">The string identifier, such as a subdomain or unique name, of the tenant to retrieve.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see cref="Result{T}"/> containing the matching <see cref="ITenantInfo"/> if found, or an error result.</returns>
    Task<Result<ITenantInfo>> GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a store for retrieving strongly-typed tenant metadata by alternative identifiers.
/// </summary>
/// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
public interface ITenantLookupStore<TTenant> : ITenantLookupStore, ITenantStore<TTenant>
    where TTenant : class, ITenantInfo
{
    /// <summary>
    /// Retrieves strongly-typed tenant metadata for the specified string identifier.
    /// </summary>
    /// <param name="identifier">The string identifier, such as a subdomain or unique name, of the tenant to retrieve.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see cref="Result{T}"/> containing the typed tenant metadata if found, or an error result.</returns>
    new Task<Result<TTenant>> GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
}
