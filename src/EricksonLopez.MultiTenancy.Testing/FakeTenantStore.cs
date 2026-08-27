// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy.Testing;

/// <summary>
/// Provides an in-memory test double for <see cref="ITenantStore"/> designed for testing without database dependencies.
/// </summary>
public sealed class FakeTenantStore : ITenantStore
{
    private readonly ConcurrentDictionary<TenantId, ITenantInfo> _tenants = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTenantStore"/> class.
    /// </summary>
    public FakeTenantStore()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTenantStore"/> class pre-populated with the specified tenants.
    /// </summary>
    /// <param name="tenants">The initial tenants to seed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tenants"/> is <see langword="null"/></exception>
    public FakeTenantStore(IEnumerable<ITenantInfo> tenants)
    {
        ArgumentNullException.ThrowIfNull(tenants);
        foreach (var tenant in tenants)
        {
            _tenants[tenant.Id] = tenant;
        }
    }

    /// <summary>
    /// Adds or updates a tenant in the store.
    /// </summary>
    /// <param name="tenant">The tenant to register.</param>
    /// <returns>The current store instance for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tenant"/> is <see langword="null"/></exception>
    public FakeTenantStore WithTenant(ITenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        _tenants[tenant.Id] = tenant;
        return this;
    }

    /// <summary>
    /// Removes a tenant from the store.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to remove.</param>
    /// <returns><see langword="true"/> if the tenant was successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveTenant(TenantId tenantId)
    {
        return _tenants.TryRemove(tenantId, out _);
    }

    /// <inheritdoc />
    public Task<Result<ITenantInfo>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        if (_tenants.TryGetValue(tenantId, out var tenant))
        {
            return Task.FromResult(Result<ITenantInfo>.Success(tenant));
        }

        return Task.FromResult(Result<ITenantInfo>.Failure(TenantErrors.NotFound(tenantId)));
    }
}

/// <summary>
/// Provides a strongly-typed in-memory test double for <see cref="ITenantStore{TTenant}"/>.
/// </summary>
/// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
public sealed class FakeTenantStore<TTenant> : ITenantStore<TTenant>
    where TTenant : class, ITenantInfo
{
    private readonly ConcurrentDictionary<TenantId, TTenant> _tenants = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTenantStore{TTenant}"/> class.
    /// </summary>
    public FakeTenantStore()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTenantStore{TTenant}"/> class with pre-seeded typed tenants.
    /// </summary>
    /// <param name="tenants">The initial tenants to seed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tenants"/> is <see langword="null"/></exception>
    public FakeTenantStore(IEnumerable<TTenant> tenants)
    {
        ArgumentNullException.ThrowIfNull(tenants);
        foreach (var tenant in tenants)
        {
            _tenants[tenant.Id] = tenant;
        }
    }

    /// <summary>
    /// Adds or updates a typed tenant in the store.
    /// </summary>
    /// <param name="tenant">The typed tenant to register.</param>
    /// <returns>The current store instance for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tenant"/> is <see langword="null"/></exception>
    public FakeTenantStore<TTenant> WithTenant(TTenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        _tenants[tenant.Id] = tenant;
        return this;
    }

    /// <inheritdoc />
    public Task<Result<TTenant>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        if (_tenants.TryGetValue(tenantId, out var tenant))
        {
            return Task.FromResult(Result<TTenant>.Success(tenant));
        }

        return Task.FromResult(Result<TTenant>.Failure(TenantErrors.NotFound(tenantId)));
    }

    async Task<Result<ITenantInfo>> ITenantStore.GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        // Stryker disable once boolean, statement : ConfigureAwait optimization
        var result = await GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Result<ITenantInfo>.Success(result.Value);
        }

        return Result<ITenantInfo>.Failure(result.Error);
    }
}
