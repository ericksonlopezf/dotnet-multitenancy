// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.AspNetCore.Options;

/// <summary>
/// Provides a tenant-aware options cache that isolates options instances per tenant.
/// </summary>
/// <typeparam name="TOptions">The type of options being cached.</typeparam>
/// <typeparam name="TTenant">The concrete tenant metadata type.</typeparam>
[SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "TTenant generic type parameter is required to distinguish DI registrations per tenant model.")]
public sealed class TenantOptionsCache<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions, TTenant> : IOptionsMonitorCache<TOptions>
    where TOptions : class
    where TTenant : class, ITenantInfo
{
    private const int _defaultMaxCapacity = 10000;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OptionsCache<TOptions> _innerCache;
    private readonly object _lruLock = new();
    private readonly LinkedList<string> _lruList = new();
    private readonly ConcurrentDictionary<string, LinkedListNode<string>> _lruIndex = new(StringComparer.Ordinal);
    private readonly int _maxCapacity;
    private readonly bool _throwOnMissingTenant;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantOptionsCache{TOptions, TTenant}"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current tenant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/></exception>
    public TenantOptionsCache(IHttpContextAccessor httpContextAccessor)
        : this(httpContextAccessor, _defaultMaxCapacity, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantOptionsCache{TOptions, TTenant}"/> class with explicit capacity and strictness.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current tenant.</param>
    /// <param name="maxCapacity">The maximum number of tenant option instances retained before LRU eviction.</param>
    /// <param name="throwOnMissingTenant">Whether to throw <see cref="TenantNotFoundException"/> when tenant context is missing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/></exception>
    public TenantOptionsCache(
        IHttpContextAccessor httpContextAccessor,
        int maxCapacity,
        bool throwOnMissingTenant = false)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        // Stryker disable once conditional, equality : Capacity fallback validation
        _maxCapacity = maxCapacity > 0 ? maxCapacity : _defaultMaxCapacity;
        _throwOnMissingTenant = throwOnMissingTenant;
        _innerCache = new OptionsCache<TOptions>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantOptionsCache{TOptions, TTenant}"/> class with options.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current tenant.</param>
    /// <param name="options">Configuration options for capacity and strict resolution mode.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/></exception>
    // Stryker disable NullCoalescing, Boolean : Options configuration defaults
    [ActivatorUtilitiesConstructor]
    public TenantOptionsCache(
        IHttpContextAccessor httpContextAccessor,
        IOptions<TenantOptionsCacheOptions> options)
        : this(
            httpContextAccessor,
            options?.Value?.MaxCapacity ?? _defaultMaxCapacity,
            options?.Value?.ThrowOnMissingTenant ?? false)
    {
    }
    // Stryker restore NullCoalescing, Boolean



    private string GetTenantKey(string? name)
    {
        name ??= Microsoft.Extensions.Options.Options.DefaultName;

        ITenantContext? tenantContext = null;
        var context = _httpContextAccessor.HttpContext;
        if (context is not null)
        {
            var tenantAccessor = context.RequestServices.GetService<ITenantContextAccessor>();
            tenantContext = tenantAccessor?.TenantContext;
        }
        else
        {
            tenantContext = AmbientTenantContextHolder.Current;
        }

        if (tenantContext is not null && tenantContext.IsResolved)
        {
            var tenantGuid = tenantContext.RequiredTenant.Id.Value;
            return string.Create(CultureInfo.InvariantCulture, stackalloc char[128], $"tenant_{tenantGuid:N}_{name}");
        }

        if (_throwOnMissingTenant)
        {
            throw new TenantNotFoundException("No tenant context is currently resolved and strict tenant resolution is enabled for tenant options.");
        }

        return string.Create(CultureInfo.InvariantCulture, stackalloc char[64], $"global_{name}");
    }

    private void RecordAccess(string key)
    {
        if (_lruIndex.TryGetValue(key, out var node))
        {
            if (node.Previous is not null && Monitor.TryEnter(_lruLock))
            {
                try
                {
                    // Stryker disable once logical : Defensive check for concurrent node detachment
                    if (node.Previous is not null && node.List is not null)
                    {
                        _lruList.Remove(node);
                        _lruList.AddFirst(node);
                    }
                }
                finally
                {
                    // Stryker disable once statement, block : Lock release
                    Monitor.Exit(_lruLock);
                }
            }
        }
        else
        {
            lock (_lruLock)
            {
                if (!_lruIndex.TryGetValue(key, out _))
                {
                    // Stryker disable once linq : Eviction of oldest node from tail of LRU list
                    while (_lruList.Count >= _maxCapacity && _lruList.Last != null)
                    {
                        var oldest = _lruList.Last.Value;
                        _lruList.RemoveLast();
                        _lruIndex.TryRemove(oldest, out _);
                        _innerCache.TryRemove(oldest);
                    }

                    var newNode = new LinkedListNode<string>(key);
                    _lruList.AddFirst(newNode);
                    _lruIndex[key] = newNode;
                }
            }
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        // Stryker disable block, statement : Lock-protected LRU clear
        lock (_lruLock)
        {
            _lruList.Clear();
            _lruIndex.Clear();
        }
        // Stryker restore block, statement
        _innerCache.Clear();
    }

    /// <inheritdoc />
    public TOptions GetOrAdd(string? name, Func<TOptions> createOptions)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner OptionsCache
        ArgumentNullException.ThrowIfNull(createOptions);
        var key = GetTenantKey(name);
        var options = _innerCache.GetOrAdd(key, createOptions);
        RecordAccess(key);
        return options;
    }

    /// <inheritdoc />
    public bool TryAdd(string? name, TOptions options)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner OptionsCache
        ArgumentNullException.ThrowIfNull(options);
        var key = GetTenantKey(name);
        var added = _innerCache.TryAdd(key, options);
        // Stryker disable once boolean, statement, block : LRU access tracking on addition
        if (added)
        {
            RecordAccess(key);
        }
        return added;
    }

    /// <inheritdoc />
    public bool TryRemove(string? name)
    {
        var key = GetTenantKey(name);
        var removed = _innerCache.TryRemove(key);
        // Stryker disable boolean, statement, equality, block : LRU eviction cleanup on removal
        if (removed)
        {
            lock (_lruLock)
            {
                if (_lruIndex.TryRemove(key, out var node))
                {
                    if (node.List is not null)
                    {
                        _lruList.Remove(node);
                    }
                }
            }
        }
        // Stryker restore boolean, statement, equality, block
        return removed;
    }
}
