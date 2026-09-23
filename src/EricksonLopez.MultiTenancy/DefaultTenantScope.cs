// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Represents the default implementation of <see cref="ITenantScope"/> wrapping a DI scope with a bound tenant context.
/// </summary>
internal sealed class DefaultTenantScope : ITenantScope
{
    private readonly IServiceScope _scope;
    private readonly ITenantContext? _previousContext;
    private readonly DefaultTenantScope? _parentScope;
    private readonly Action<DefaultTenantScope?> _setCurrentScope;
    private bool _disposed;

    /// <inheritdoc />
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;

    /// <inheritdoc />
    public ITenantContext TenantContext { get; }

    internal DefaultTenantScope(
        IServiceScope scope,
        ITenantContext tenantContext,
        ITenantContext? previousContext,
        DefaultTenantScope? parentScope,
        Action<DefaultTenantScope?> setCurrentScope)
    {
        _scope = scope;
        TenantContext = tenantContext;
        _previousContext = previousContext;
        _parentScope = parentScope;
        _setCurrentScope = setCurrentScope;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            RestoreAmbientContext();
            _scope.Dispose();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            RestoreAmbientContext();

            if (_scope is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            _scope.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private void RestoreAmbientContext()
    {
        if (ReferenceEquals(AmbientTenantContextHolder.Current, TenantContext))
        {
            var ancestor = _parentScope;
            while (ancestor is not null && ancestor._disposed)
            {
                ancestor = ancestor._parentScope;
            }

            if (ancestor is not null)
            {
                AmbientTenantContextHolder.Current = ancestor.TenantContext;
                // Stryker disable once statement : AsyncLocal scope tracking
                _setCurrentScope(ancestor);
            }
            else
            {
                var root = this;
                while (root._parentScope is not null)
                {
                    root = root._parentScope;
                }

                AmbientTenantContextHolder.Current = (root._previousContext is { IsResolved: true }) ? root._previousContext : null;
                // Stryker disable once statement : AsyncLocal scope tracking
                _setCurrentScope(null);
            }
        }
    }
}
