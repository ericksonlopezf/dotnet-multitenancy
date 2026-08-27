// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Creates isolated dependency injection scopes with pre-populated tenant context for background jobs and message consumers.
/// </summary>
public sealed class DefaultTenantScopeFactory : ITenantScopeFactory
{
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
    public ITenantScope CreateScope(ITenantInfo tenant, TenantResolutionSource source = TenantResolutionSource.ExplicitScope)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (tenant.Id.IsEmpty)
        {
            throw new ArgumentException("Tenant must have a valid, non-empty identifier.", nameof(tenant));
        }

        var scope = _scopeFactory.CreateScope();
        var context = new TenantContext<ITenantInfo>(tenant, source);

        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.TenantContext = context;

        return new DefaultTenantScope(scope, context);
    }
}

/// <summary>
/// Default implementation of <see cref="ITenantScope"/> wrapping a DI scope with a bound tenant context.
/// </summary>
internal sealed class DefaultTenantScope : ITenantScope
{
    private readonly IServiceScope _scope;
    private bool _disposed;

    /// <inheritdoc />
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;

    /// <inheritdoc />
    public ITenantContext TenantContext { get; }

    internal DefaultTenantScope(IServiceScope scope, ITenantContext tenantContext)
    {
        _scope = scope;
        TenantContext = tenantContext;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _scope.Dispose();
            _disposed = true;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_scope is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            _scope.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
