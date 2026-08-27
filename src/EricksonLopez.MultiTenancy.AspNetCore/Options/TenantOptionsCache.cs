// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
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
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OptionsCache<TOptions> _innerCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantOptionsCache{TOptions, TTenant}"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current tenant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/></exception>
    public TenantOptionsCache(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _innerCache = new OptionsCache<TOptions>();
    }

    private string GetTenantKey(string? name)
    {
        name ??= Microsoft.Extensions.Options.Options.DefaultName;

        var context = _httpContextAccessor.HttpContext;
        if (context is not null)
        {
            var tenantAccessor = context.RequestServices.GetService<ITenantContextAccessor>();
            if (tenantAccessor?.TenantContext is not null && tenantAccessor.TenantContext.IsResolved)
            {
                return $"{tenantAccessor.TenantContext.RequiredTenant.Id.Value:N}_{name}";
            }
        }

        return name;
    }

    /// <inheritdoc />
    public void Clear() => _innerCache.Clear();

    /// <inheritdoc />
    public TOptions GetOrAdd(string? name, Func<TOptions> createOptions)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner OptionsCache
        ArgumentNullException.ThrowIfNull(createOptions);
        return _innerCache.GetOrAdd(GetTenantKey(name), createOptions);
    }

    /// <inheritdoc />
    public bool TryAdd(string? name, TOptions options)
    {
        // Stryker disable once statement : Guard clause defensively duplicated by inner OptionsCache
        ArgumentNullException.ThrowIfNull(options);
        return _innerCache.TryAdd(GetTenantKey(name), options);
    }

    /// <inheritdoc />
    public bool TryRemove(string? name)
    {
        return _innerCache.TryRemove(GetTenantKey(name));
    }
}
