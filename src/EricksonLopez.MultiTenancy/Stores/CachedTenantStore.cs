// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.Stores;

/// <summary>
/// Provides a caching decorator over an <see cref="ITenantStore{TTenant}"/> and <see cref="ITenantLookupStore{TTenant}"/> using <see cref="IMemoryCache"/>.
/// </summary>
/// <typeparam name="TTenant">The type of tenant metadata managed by this store.</typeparam>
public sealed class CachedTenantStore<TTenant> : ITenantLookupStore<TTenant>
    where TTenant : class, ITenantInfo
{
    private readonly ITenantStore<TTenant> _innerStore;
    private readonly IMemoryCache _cache;
    private readonly CachedTenantStoreOptions _options;
    private const int _stripeCount = 64;
    private readonly SemaphoreSlim[] _stripedLocks = InitializeStripedLocks();

    private static SemaphoreSlim[] InitializeStripedLocks()
    {
        var locks = new SemaphoreSlim[_stripeCount];
        for (var i = 0; i < _stripeCount; i++)
        {
            locks[i] = new SemaphoreSlim(1, 1);
        }
        return locks;
    }

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

        if (TryGetFromCache(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var stripeIndex = (tenantId.Value.GetHashCode() & 0x7FFFFFFF) % _stripeCount;
        var keyLock = _stripedLocks[stripeIndex];
        await keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetFromCache(cacheKey, out cachedResult))
            {
                return cachedResult;
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
            else if (_options.EnableNegativeCaching)
            {
                var negativeOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _options.NegativeCacheExpirationRelativeToNow
                };

                _cache.Set(cacheKey, result.Error, negativeOptions);
            }

            return result;
        }
        finally
        {
            keyLock.Release();
        }
    }

    private bool TryGetFromCache(string cacheKey, out Result<TTenant> result)
    {
        if (_cache.TryGetValue(cacheKey, out var cachedObj))
        {
            if (cachedObj is TTenant tenant)
            {
                result = Result<TTenant>.Success(tenant);
                return true;
            }

            if (cachedObj is Result<TTenant> cachedResult)
            {
                result = cachedResult;
                return true;
            }

            if (cachedObj is Error error)
            {
                result = Result<TTenant>.Failure(error);
                return true;
            }
        }

        result = default;
        return false;
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

    /// <inheritdoc />
    public async Task<Result<TTenant>> GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return TenantErrors.InvalidId(identifier);
        }

        var normalizedIdentifier = identifier.Trim().ToLowerInvariant();
        var cacheKey = $"TenantStore_Ident_{normalizedIdentifier}";

        if (TryGetFromCache(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var stripeIndex = (normalizedIdentifier.GetHashCode(StringComparison.Ordinal) & 0x7FFFFFFF) % _stripeCount;
        var keyLock = _stripedLocks[stripeIndex];
        await keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetFromCache(cacheKey, out cachedResult))
            {
                return cachedResult;
            }

            if (_innerStore is ITenantLookupStore<TTenant> lookupStore)
            {
                var result = await lookupStore.GetTenantByIdentifierAsync(identifier, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _options.AbsoluteExpirationRelativeToNow,
                        SlidingExpiration = _options.SlidingExpiration
                    };

                    _cache.Set(cacheKey, result.Value, cacheOptions);
                    _cache.Set($"TenantStore_Id_{result.Value.Id.Value:N}", result.Value, cacheOptions);
                }
                else if (_options.EnableNegativeCaching)
                {
                    var negativeOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _options.NegativeCacheExpirationRelativeToNow
                    };

                    _cache.Set(cacheKey, result.Error, negativeOptions);
                }

                return result;
            }

            return TenantErrors.StrategyFailed("CachedTenantStore", "Inner store does not implement ITenantLookupStore.");
        }
        finally
        {
            keyLock.Release();
        }
    }

    async Task<Result<ITenantInfo>> ITenantLookupStore.GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        var result = await GetTenantByIdentifierAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Result<ITenantInfo>.Success(result.Value);
        }

        return Result<ITenantInfo>.Failure(result.Error);
    }
}
