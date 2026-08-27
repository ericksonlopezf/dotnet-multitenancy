// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.MultiTenancy;

/// <summary>
/// Provides access to and single-assignment storage for the ambient tenant context within a scoped lifetime.
/// </summary>
public sealed class ScopedTenantContextAccessor : ITenantContextAccessor
{
    private ITenantContext? _context;
    private bool _isSet;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The tenant context has already been set for this scope</exception>
    public ITenantContext? TenantContext
    {
        get => _context;
        set
        {
            if (_isSet)
            {
                throw new InvalidOperationException(
                    "The tenant context has already been set for this scope. " +
                    "ITenantContext is immutable after assignment. " +
                    "Each scope may only have one tenant context.");
            }

            _context = value;
            _isSet = true;
        }
    }
}
