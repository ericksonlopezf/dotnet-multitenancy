// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides access to and single-assignment storage for the ambient tenant context within a scoped lifetime.
/// </summary>
public sealed class ScopedTenantContextAccessor : ITenantContextAccessor
{
    private ITenantContext? _context;
    private int _isSet;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The tenant context has already been set for this scope</exception>
    public ITenantContext? TenantContext
    {
        get => Volatile.Read(ref _context);
        set
        {
            if (Interlocked.CompareExchange(ref _isSet, 1, 0) != 0)
            {
                throw new InvalidOperationException(
                    "The tenant context has already been set for this scope. " +
                    "ITenantContext is immutable after assignment. " +
                    "Each scope may only have one tenant context.");
            }

            Volatile.Write(ref _context, value);
        }
    }
}
