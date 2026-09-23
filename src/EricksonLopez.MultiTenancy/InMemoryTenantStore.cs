// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides a thread-safe in-memory store for retrieving and managing tenant metadata.
/// </summary>
/// <remarks>
/// Backed by a <see cref="ConcurrentDictionary{TKey, TValue}"/> for safe concurrent access.
/// This store is suitable for development, testing, and single-instance production deployments.
/// It is not suitable for distributed or multi-node deployments where tenants must be shared
/// across process boundaries.
/// </remarks>
/// <typeparam name="TTenant">The concrete tenant metadata type managed by this store.</typeparam>
public class InMemoryTenantStore<TTenant> : ITenantLookupStore<TTenant>
    where TTenant : class, ITenantInfo
{
    private readonly ConcurrentDictionary<TenantId, TTenant> _tenants = new();
    private readonly int? _maxCapacity;

    /// <summary>
    /// Initializes a new empty instance of the <see cref="InMemoryTenantStore{TTenant}"/> class.
    /// </summary>
    public InMemoryTenantStore() { }

    /// <summary>
    /// Initializes a new empty instance of the <see cref="InMemoryTenantStore{TTenant}"/> class with a maximum capacity limit.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity limit for stored tenants.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCapacity"/> is less than or equal to zero</exception>
    public InMemoryTenantStore(int maxCapacity)
    {
        if (maxCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Maximum capacity must be greater than zero.");
        }
        _maxCapacity = maxCapacity;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryTenantStore{TTenant}"/> class seeded with the specified tenants.
    /// </summary>
    /// <param name="tenants">The initial collection of tenants.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tenants"/> is <see langword="null"/></exception>
    public InMemoryTenantStore(IEnumerable<TTenant> tenants)
    {
        ArgumentNullException.ThrowIfNull(tenants);

        foreach (var tenant in tenants)
        {
            _tenants[tenant.Id] = tenant;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryTenantStore{TTenant}"/> class seeded with the specified tenants and a maximum capacity limit.
    /// </summary>
    /// <param name="tenants">The initial collection of tenants.</param>
    /// <param name="maxCapacity">The maximum capacity limit for stored tenants.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tenants"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCapacity"/> is less than or equal to zero</exception>
    public InMemoryTenantStore(IEnumerable<TTenant> tenants, int maxCapacity) : this(maxCapacity)
    {
        ArgumentNullException.ThrowIfNull(tenants);

        foreach (var tenant in tenants)
        {
            _tenants[tenant.Id] = tenant;
        }
    }

    /// <summary>
    /// Adds or updates tenant metadata in the in-memory store.
    /// </summary>
    /// <remarks>This operation is thread-safe.</remarks>
    /// <param name="tenant">The tenant metadata to add or update.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tenant"/> is <see langword="null"/></exception>
    /// <exception cref="InvalidOperationException">The configured maximum capacity has been exceeded</exception>
    public void AddOrUpdate(TTenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (_maxCapacity.HasValue && !_tenants.ContainsKey(tenant.Id) && _tenants.Count >= _maxCapacity.Value)
        {
            throw new InvalidOperationException($"Maximum tenant store capacity of {_maxCapacity.Value} exceeded.");
        }

        _tenants[tenant.Id] = tenant;
    }

    /// <summary>
    /// Attempts to add a tenant to the store if it does not already exist.
    /// </summary>
    /// <param name="tenant">The tenant to add.</param>
    /// <returns><see langword="true"/> if the tenant was added; <see langword="false"/> if it already exists or capacity is reached.</returns>
    public bool TryAdd(TTenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (_maxCapacity.HasValue && _tenants.Count >= _maxCapacity.Value)
        {
            return false;
        }

        return _tenants.TryAdd(tenant.Id, tenant);
    }

    /// <summary>
    /// Attempts to remove a tenant from the store.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant to remove.</param>
    /// <returns><see langword="true"/> if the tenant was removed; <see langword="false"/> otherwise.</returns>
    public bool TryRemove(TenantId tenantId)
    {
        return _tenants.TryRemove(tenantId, out _);
    }

    /// <summary>
    /// Clears all tenants from the store.
    /// </summary>
    public void Clear()
    {
        _tenants.Clear();
    }

    /// <inheritdoc />
    public Task<Result<TTenant>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_tenants.TryGetValue(tenantId, out var tenant))
        {
            return Task.FromResult(Result<TTenant>.Success(tenant));
        }

        return Task.FromResult(Result<TTenant>.Failure(TenantErrors.NotFound(tenantId)));
    }

    /// <inheritdoc />
    public Task<Result<TTenant>> GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var match = _tenants.Values.FirstOrDefault(t => string.Equals(t.Name, identifier, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return Task.FromResult(Result<TTenant>.Success(match));
        }

        // Check if the identifier is actually a valid TenantId string
        if (TenantId.TryCreate(identifier, out var parsedId) && _tenants.TryGetValue(parsedId, out var tenantById))
        {
            return Task.FromResult(Result<TTenant>.Success(tenantById));
        }

        return Task.FromResult(Result<TTenant>.Failure(Error.NotFound("Tenant.NotFound", $"Tenant with identifier '{identifier}' was not found.")));
    }

    /// <inheritdoc />
    async Task<Result<ITenantInfo>> ITenantStore.GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        // Stryker disable once boolean : ConfigureAwait is not verifiable
        var res = await GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (res.IsSuccess)
        {
            return Result<ITenantInfo>.Success(res.Value);
        }

        return Result<ITenantInfo>.Failure(res.Error);
    }

    /// <inheritdoc />
    async Task<Result<ITenantInfo>> ITenantLookupStore.GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        // Stryker disable once boolean : ConfigureAwait is not verifiable
        var res = await GetTenantByIdentifierAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (res.IsSuccess)
        {
            return Result<ITenantInfo>.Success(res.Value);
        }

        return Result<ITenantInfo>.Failure(res.Error);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TTenant> GetAllStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var tenant in _tenants.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return tenant;
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    async IAsyncEnumerable<ITenantInfo> ITenantStore.GetAllStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var tenant in GetAllStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return tenant;
        }
    }
}

/// <summary>
/// Provides a default in-memory store for retrieving and managing <see cref="TenantInfo"/> metadata.
/// </summary>
public sealed class InMemoryTenantStore : InMemoryTenantStore<TenantInfo>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryTenantStore"/> class.
    /// </summary>
    public InMemoryTenantStore() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryTenantStore"/> class with a maximum capacity limit.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity limit for stored tenants.</param>
    public InMemoryTenantStore(int maxCapacity) : base(maxCapacity) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryTenantStore"/> class seeded with the specified tenants.
    /// </summary>
    /// <param name="tenants">The initial collection of tenants.</param>
    public InMemoryTenantStore(IEnumerable<TenantInfo> tenants) : base(tenants) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryTenantStore"/> class seeded with the specified tenants and a maximum capacity limit.
    /// </summary>
    /// <param name="tenants">The initial collection of tenants.</param>
    /// <param name="maxCapacity">The maximum capacity limit for stored tenants.</param>
    public InMemoryTenantStore(IEnumerable<TenantInfo> tenants, int maxCapacity) : base(tenants, maxCapacity) { }
}
