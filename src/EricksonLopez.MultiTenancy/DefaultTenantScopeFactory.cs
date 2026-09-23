// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides a factory that creates isolated dependency injection scopes with pre-populated tenant context for background jobs and message consumers.
/// </summary>
public sealed class DefaultTenantScopeFactory : ITenantScopeFactory
{
    private static readonly AsyncLocal<DefaultTenantScope?> _currentScope = new();
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTenantScopeFactory"/> class.
    /// </summary>
    /// <param name="scopeFactory">The root service scope factory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scopeFactory"/> is <see langword="null"/></exception>
    public DefaultTenantScopeFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="tenant"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="tenant"/> has an empty identifier</exception>
    /// <exception cref="TenantInactiveException"><paramref name="tenant"/> is inactive</exception>
    public ITenantScope CreateScope(ITenantInfo tenant, TenantResolutionSource source = TenantResolutionSource.ExplicitScope)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (tenant.Id.IsEmpty)
        {
            throw new ArgumentException("Tenant must have a valid, non-empty identifier.", nameof(tenant));
        }

        if (!tenant.IsActive)
        {
            throw new TenantInactiveException(tenant.Id);
        }

        var scope = _scopeFactory.CreateScope();
        var context = new TenantContext<ITenantInfo>(tenant, source);

        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.TenantContext = context;

        var previousScope = _currentScope.Value;
        var previousContext = AmbientTenantContextHolder.Current;
        AmbientTenantContextHolder.Current = context;

        var tenantScope = new DefaultTenantScope(scope, context, previousContext, previousScope, s => _currentScope.Value = s);
        _currentScope.Value = tenantScope;
        return tenantScope;
    }
}
