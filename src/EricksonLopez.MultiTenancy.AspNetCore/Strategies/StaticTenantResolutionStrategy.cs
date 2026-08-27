// Copyright © Erickson Lopez. MIT License.
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy.AspNetCore.Strategies;

/// <summary>
/// Resolves a static, predefined tenant identifier.
/// </summary>
/// <remarks>
/// Useful for fallback scenarios, testing, or single-tenant deployments.
/// </remarks>
public sealed class StaticTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly TenantId _tenantId;

    /// <inheritdoc />
    public string StrategyName => "Static";

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticTenantResolutionStrategy"/> class with a predefined tenant identifier.
    /// </summary>
    /// <param name="tenantId">The static tenant identifier to resolve.</param>
    public StaticTenantResolutionStrategy(TenantId tenantId)
    {
        _tenantId = tenantId;
    }

    /// <inheritdoc />
    public ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        if (_tenantId.IsEmpty)
        {
            return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, "Static tenant ID is empty.")));
        }

        return ValueTask.FromResult(Result<TenantId>.Success(_tenantId));
    }
}
