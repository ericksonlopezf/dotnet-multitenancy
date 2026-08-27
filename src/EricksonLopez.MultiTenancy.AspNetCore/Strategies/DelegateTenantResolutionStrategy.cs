// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy.AspNetCore.Strategies;

/// <summary>
/// Resolves the tenant identifier using a custom delegate function.
/// </summary>
public sealed class DelegateTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly Func<CancellationToken, ValueTask<Result<TenantId>>> _resolver;

    /// <inheritdoc />
    public string StrategyName => "Delegate";

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="resolver">The custom delegate function used to resolve the tenant identifier.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is <see langword="null"/></exception>
    public DelegateTenantResolutionStrategy(Func<CancellationToken, ValueTask<Result<TenantId>>> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <inheritdoc />
    public ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        return _resolver(cancellationToken);
    }
}
