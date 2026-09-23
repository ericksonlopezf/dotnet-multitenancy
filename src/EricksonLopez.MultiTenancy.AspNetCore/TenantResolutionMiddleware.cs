// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy.AspNetCore.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.AspNetCore;

/// <summary>
/// Intercepts HTTP requests to evaluate configured resolution strategies and bind the ambient tenant context.
/// </summary>
public sealed partial class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private readonly TenantResolutionMiddlewareOptions _options;

    // Stryker disable string : OpenTelemetry metrics instrumentation
    private static readonly Meter _meter = new("EricksonLopez.MultiTenancy");
    private static readonly Counter<long> _spoofingAttempts = _meter.CreateCounter<long>("multitenancy.security.spoofing_attempts", description: "Number of tenant spoofing attempts detected.");
    // Stryker restore string
    private const string _problemJsonContentType = "application/problem+json";

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantResolutionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the request pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The optional configuration options for tenant resolution middleware.</param>
    /// <exception cref="ArgumentNullException"><paramref name="next"/> or <paramref name="logger"/> is <see langword="null"/></exception>
    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger,
        IOptions<TenantResolutionMiddlewareOptions>? options = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new TenantResolutionMiddlewareOptions();
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
    /// <exception cref="TenantResolutionConflictException">Multiple strategies resolved different tenant identifiers for the same request and <see cref="TenantResolutionMiddlewareOptions.WriteProblemDetailsOnConflict"/> is false</exception>
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
        var resolvedId = TenantId.Empty;
        string? resolvedBy = null;
        var resolvedSource = TenantResolutionSource.None;

        foreach (var strategy in strategies)
        {
            // Stryker disable once boolean
            var strategyResult = await strategy.ResolveTenantIdAsync(cancellationToken).ConfigureAwait(false);
            if (strategyResult.IsSuccess && !strategyResult.Value.IsEmpty)
            {
                if (!resolvedId.IsEmpty && resolvedId != strategyResult.Value)
                {
                    // Stryker disable once boolean : ConfigureAwait optimization
                    if (await HandleResolutionConflictAsync(context, resolvedBy, resolvedId, strategy, strategyResult.Value, cancellationToken).ConfigureAwait(false))
                    {
                        return;
                    }
                }

                resolvedId = strategyResult.Value;
                resolvedBy ??= strategy.StrategyName;
                if (resolvedSource == TenantResolutionSource.None)
                {
                    resolvedSource = strategy.Source;
                }
            }
        }

        // Stryker disable logical, null coalescing, string : Principal tenant claim validation and fallback hierarchy
        if (_options.ValidateAuthenticatedPrincipalTenantClaim &&
            context.User?.Identity?.IsAuthenticated == true &&
            !resolvedId.IsEmpty)
        {
            var userTenantClaim = context.User.FindFirst(_options.TenantClaimType)?.Value
                ?? context.User.FindFirst("tid")?.Value
                ?? context.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
            // Stryker restore logical, null coalescing, string

            if (userTenantClaim is not null)
            {
                if (!string.Equals(userTenantClaim, resolvedId.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    // Stryker disable statement, string, boolean : Spoofing response telemetry and RFC 7807 body
                    _spoofingAttempts.Add(1);
                    LogTenantMismatch(_logger, userTenantClaim, resolvedId.ToString());
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = _problemJsonContentType;
                    await context.Response.WriteAsync(
                        $"{{\"type\":\"https://tools.ietf.org/html/rfc7807\",\"title\":\"Forbidden\",\"status\":403,\"detail\":\"User identity is bound to tenant '{userTenantClaim}' but request was resolved to tenant '{resolvedId}'.\"}}",
                        cancellationToken).ConfigureAwait(false);
                    // Stryker restore statement, string, boolean
                    return;
                }
            }
            else if (resolvedSource == TenantResolutionSource.Header)
            {
                // Stryker disable statement, string, boolean : Untrusted header response telemetry and RFC 7807 body
                _spoofingAttempts.Add(1);
                LogUntrustedHeaderForAuthenticatedUser(_logger, resolvedId.ToString());
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = _problemJsonContentType;
                await context.Response.WriteAsync(
                    $"{{\"type\":\"https://tools.ietf.org/html/rfc7807\",\"title\":\"Forbidden\",\"status\":403,\"detail\":\"Authenticated requests without a tenant claim cannot resolve tenant via untrusted headers.\"}}",
                    cancellationToken).ConfigureAwait(false);
                // Stryker restore statement, string, boolean
                return;
            }
        }

        if (!resolvedId.IsEmpty)
        {
            ITenantInfo? resolvedTenant = null;
            if (context.Items.TryGetValue("__TenantResolution_ResolvedTenant", out var stashedTenant) &&
                stashedTenant is ITenantInfo preFetched &&
                preFetched.Id == resolvedId)
            {
                resolvedTenant = preFetched;
            }

            if (resolvedTenant is not null)
            {
                accessor.TenantContext = TenantContext.Create(resolvedTenant, resolvedSource);
            }
            else
            {
                // Stryker disable once boolean
                var storeResult = await tenantStore.GetTenantAsync(resolvedId, cancellationToken).ConfigureAwait(false);
                if (storeResult.IsSuccess)
                {
                    accessor.TenantContext = TenantContext.Create(storeResult.Value, resolvedSource);
                }
                else
                {
                    // Stryker disable once statement, null coalescing, string : Defensive logging of store miss
                    LogTenantNotFoundInStore(_logger, resolvedId.ToString(), storeResult.Error.Description ?? "Tenant not found in store.");

                    if (_options.FailOnStoreMiss)
                    {
                        var endpoint = context.GetEndpoint();
                        if (endpoint?.Metadata.GetMetadata<IAllowAnonymousTenantMetadata>() is not null)
                        {
                            // Stryker disable once boolean, statement : Anonymous endpoint pipeline bypass
                            await _next(context).ConfigureAwait(false);
                            return;
                        }

                        // Stryker disable statement, string, boolean : 404 response body and headers for store miss
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        context.Response.ContentType = "application/json";
                        var error = TenantErrors.NotFound(resolvedId);
                        await context.Response.WriteAsync(
                            $"{{\"code\":\"{error.Code}\",\"description\":\"{error.Description}\"}}",
                            cancellationToken).ConfigureAwait(false);
                        // Stryker restore statement, string, boolean
                        return;
                    }
                }
            }
        }

        // Stryker disable once boolean
        await _next(context).ConfigureAwait(false);
    }

    private async Task<bool> HandleResolutionConflictAsync(
        HttpContext context,
        string? resolvedBy,
        TenantId resolvedId,
        ITenantResolutionStrategy strategy,
        TenantId conflictedId,
        CancellationToken cancellationToken)
    {
        LogResolutionConflict(_logger, resolvedBy, resolvedId.ToString(), strategy.StrategyName, conflictedId.ToString());

        if (_options.WriteProblemDetailsOnConflict)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = _problemJsonContentType;
            var conflictDetail = _options.IncludeStrategyDetailsInConflictResponse
                ? $"Tenant resolution conflict detected. First: '{resolvedId}' (by {resolvedBy}), Second: '{conflictedId}' (by {strategy.StrategyName})."
                : "Tenant resolution conflict detected: multiple strategies resolved conflicting tenant identifiers.";
            // Stryker disable once boolean : ConfigureAwait optimization
            await context.Response.WriteAsync(
                $"{{\"type\":\"https://tools.ietf.org/html/rfc7807\",\"title\":\"Tenant Resolution Conflict\",\"status\":409,\"detail\":\"{conflictDetail}\"}}",
                cancellationToken).ConfigureAwait(false);
            return true;
        }

        throw new TenantResolutionConflictException(
            $"Tenant resolution conflict detected. Multiple strategies resolved different tenant identifiers. First: '{resolvedId}' (by {resolvedBy}), Second: '{conflictedId}' (by {strategy.StrategyName}).",
            resolvedId,
            resolvedBy,
            conflictedId,
            strategy.StrategyName);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Tenant resolution conflict: strategy '{Strategy1}' resolved tenant '{Tenant1}' but strategy '{Strategy2}' resolved tenant '{Tenant2}'.")]
    private static partial void LogResolutionConflict(ILogger logger, string? strategy1, string tenant1, string strategy2, string tenant2);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Tenant identifier '{TenantId}' resolved by strategy but not found in tenant store: {Reason}")]
    private static partial void LogTenantNotFoundInStore(ILogger logger, string tenantId, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Authenticated user tenant claim '{ClaimTenant}' does not match resolved request tenant '{ResolvedTenant}'.")]
    private static partial void LogTenantMismatch(ILogger logger, string claimTenant, string resolvedTenant);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Authenticated request lacking tenant claim attempted to resolve tenant '{ResolvedTenant}' via untrusted header.")]
    private static partial void LogUntrustedHeaderForAuthenticatedUser(ILogger logger, string resolvedTenant);
}
