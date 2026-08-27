// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.Stores;

/// <summary>
/// Provides a caching decorator over an <see cref="ITenantStore{TTenant}"/> using <see cref="IMemoryCache"/>.
/// </summary>
/// <typeparam name="TTenant">The type of tenant metadata managed by this store.</typeparam>
public sealed class CachedTenantStore<TTenant> : ITenantStore<TTenant>
    where TTenant : class, ITenantInfo
{
    private readonly ITenantStore<TTenant> _innerStore;
    private readonly IMemoryCache _cache;
    private readonly CachedTenantStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedTenantStore{TTenant}"/> class.
    /// </summary>
    /// <param name="innerStore">The underlying tenant store to decorate.</param>
    /// <param name="cache">The memory cache instance used to store tenant records.</param>
    /// <param name="options">The caching options configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="innerStore"/> or <paramref name="cache"/> is <see langword="null"/></exception>
    public CachedTenantStore(ITenantStore<TTenant> innerStore, IMemoryCache cache, IOptions<CachedTenantStoreOptions> options)
    {
        _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? new CachedTenantStoreOptions();
    }

    /// <inheritdoc />
    public async Task<Result<TTenant>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"TenantStore_Id_{tenantId.Value:N}";

        if (_cache.TryGetValue<TTenant>(cacheKey, out var cachedTenant))
        {
            return Result<TTenant>.Success(cachedTenant!);
        }

        // Stryker disable once boolean : ConfigureAwait is not verifiable
        var result = await _innerStore.GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.AbsoluteExpirationRelativeToNow,
                SlidingExpiration = _options.SlidingExpiration
            };

            _cache.Set(cacheKey, result.Value, cacheOptions);
        }

        return result;
    }

    async Task<Result<ITenantInfo>> ITenantStore.GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        // Stryker disable once boolean : ConfigureAwait is not verifiable
        var result = await GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Result<ITenantInfo>.Success(result.Value);
        }

        return Result<ITenantInfo>.Failure(result.Error);
    }
}
