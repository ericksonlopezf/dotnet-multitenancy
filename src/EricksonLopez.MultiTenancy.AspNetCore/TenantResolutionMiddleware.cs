// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.MultiTenancy.AspNetCore;

/// <summary>
/// Intercepts HTTP requests to evaluate configured resolution strategies and bind the ambient tenant context.
/// </summary>
public sealed partial class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantResolutionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the request pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="next"/> or <paramref name="logger"/> is <see langword="null"/></exception>
    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the middleware for the current HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="strategies">The ordered collection of resolution strategies to evaluate.</param>
    /// <param name="tenantStore">The store used to retrieve tenant metadata.</param>
    /// <param name="accessor">The ambient tenant context accessor.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="strategies"/>, <paramref name="tenantStore"/>, or <paramref name="accessor"/> is <see langword="null"/></exception>
    /// <exception cref="InvalidOperationException">Multiple strategies resolved different tenant identifiers for the same request</exception>
    public async Task InvokeAsync(
        HttpContext context,
        IEnumerable<ITenantResolutionStrategy> strategies,
        ITenantStore tenantStore,
        ITenantContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(tenantStore);
        ArgumentNullException.ThrowIfNull(accessor);

        var cancellationToken = context.RequestAborted;
        TenantId resolvedId = TenantId.Empty;
        string? resolvedBy = null;

        foreach (var strategy in strategies)
        {
            // Stryker disable once boolean
            var strategyResult = await strategy.ResolveTenantIdAsync(cancellationToken).ConfigureAwait(false);
            if (strategyResult.IsSuccess && !strategyResult.Value.IsEmpty)
            {
                if (!resolvedId.IsEmpty && resolvedId != strategyResult.Value)
                {
                    LogResolutionConflict(_logger, resolvedBy, resolvedId.ToString(), strategy.StrategyName, strategyResult.Value.ToString());
                    throw new InvalidOperationException($"Tenant resolution conflict detected. Multiple strategies resolved different tenant identifiers. First: '{resolvedId}' (by {resolvedBy}), Second: '{strategyResult.Value}' (by {strategy.StrategyName}).");
                }

                resolvedId = strategyResult.Value;
                resolvedBy ??= strategy.StrategyName;
            }
        }

        if (!resolvedId.IsEmpty)
        {
            // Stryker disable once boolean
            var storeResult = await tenantStore.GetTenantAsync(resolvedId, cancellationToken).ConfigureAwait(false);
            if (storeResult.IsSuccess)
            {
                accessor.TenantContext = TenantContext.Create(storeResult.Value);
            }
        }

        // Stryker disable once boolean
        await _next(context).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Tenant resolution conflict: strategy '{Strategy1}' resolved tenant '{Tenant1}' but strategy '{Strategy2}' resolved tenant '{Tenant2}'.")]
    private static partial void LogResolutionConflict(ILogger logger, string? strategy1, string tenant1, string strategy2, string tenant2);
}
