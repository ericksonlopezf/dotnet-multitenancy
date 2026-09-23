// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides an ambient context holder for background job scopes and decoupled workers where <c>HttpContext</c> is unavailable.
/// </summary>
public static class AmbientTenantContextHolder
{
    private static readonly AsyncLocal<ITenantContext?> _current = new();

    /// <summary>
    /// Gets the active ambient tenant context for the current asynchronous execution flow.
    /// Mutation is restricted to internal scope lifecycle managers.
    /// </summary>
    public static ITenantContext? Current
    {
        get => _current.Value;
        internal set => _current.Value = value;
    }

    /// <summary>
    /// Enters a safe ambient tenant scope that guarantees restoration of the previous context upon disposal.
    /// </summary>
    /// <param name="context">The tenant context to establish as ambient.</param>
    /// <returns>An <see cref="IDisposable"/> token that reverts to the previous ambient context upon disposal.</returns>
    public static IDisposable SetCurrentScoped(ITenantContext? context)
    {
        var previous = _current.Value;
        _current.Value = context;
        return new AmbientScope(previous);
    }

    private sealed class AmbientScope : IDisposable
    {
        private readonly ITenantContext? _previous;
        private int _disposed;

        public AmbientScope(ITenantContext? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _current.Value = _previous;
            }
        }
    }
}
