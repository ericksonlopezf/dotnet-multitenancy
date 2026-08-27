// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy.Testing;

/// <summary>
/// Provides a test double for <see cref="ITenantResolutionStrategy"/> with customizable resolution results.
/// </summary>
public sealed class FakeTenantResolutionStrategy : ITenantResolutionStrategy
{
    private Result<TenantId> _result;

    /// <inheritdoc />
    public string StrategyName { get; }

    /// <summary>
    /// Gets the number of times <see cref="ResolveTenantIdAsync"/> has been invoked.
    /// </summary>
    public int InvocationCount { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTenantResolutionStrategy"/> class with a successful outcome for the specified tenant identifier.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to return upon resolution.</param>
    /// <param name="strategyName">The human-readable strategy name (default: <c>FakeStrategy</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="strategyName"/> is <see langword="null"/></exception>
    public FakeTenantResolutionStrategy(TenantId tenantId, string strategyName = "FakeStrategy")
    {
        _result = tenantId;
        StrategyName = strategyName ?? throw new ArgumentNullException(nameof(strategyName));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTenantResolutionStrategy"/> class with a preset resolution result.
    /// </summary>
    /// <param name="result">The preset result to return upon resolution.</param>
    /// <param name="strategyName">The human-readable strategy name (default: <c>FakeStrategy</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="strategyName"/> is <see langword="null"/></exception>
    public FakeTenantResolutionStrategy(Result<TenantId> result, string strategyName = "FakeStrategy")
    {
        _result = result;
        StrategyName = strategyName ?? throw new ArgumentNullException(nameof(strategyName));
    }

    /// <summary>
    /// Sets the resolution result to be returned on subsequent resolution calls.
    /// </summary>
    /// <param name="result">The new resolution result.</param>
    public void SetResult(Result<TenantId> result)
    {
        _result = result;
    }

    /// <inheritdoc />
    public ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        return ValueTask.FromResult(_result);
    }
}
