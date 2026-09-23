// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EricksonLopez.MultiTenancy.AspNetCore.Strategies;

/// <summary>
/// Resolves the tenant identifier from request route values.
/// </summary>
public sealed class RouteTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _routeParameterName;

    /// <inheritdoc />
    public string StrategyName => "Route";

    /// <inheritdoc />
    public TenantResolutionSource Source => TenantResolutionSource.Route;

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="routeParameterName">The route parameter name used to extract the tenant identifier (default: <c>tenantId</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/></exception>
    public RouteTenantResolutionStrategy(IHttpContextAccessor httpContextAccessor, string routeParameterName = "tenantId")
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _routeParameterName = string.IsNullOrWhiteSpace(routeParameterName) ? "tenantId" : routeParameterName;
    }

    /// <inheritdoc />
    public ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        if (_httpContextAccessor.HttpContext is { } context &&
            context.Request.RouteValues.TryGetValue(_routeParameterName, out var routeValue) &&
            routeValue is string stringValue)
        {
            if (TenantId.TryCreate(stringValue, out var tenantId))
            {
                return ValueTask.FromResult(Result<TenantId>.Success(tenantId));
            }

            if (!string.IsNullOrWhiteSpace(stringValue))
            {
                return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.InvalidId(stringValue)));
            }
        }

        return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, $"Route parameter '{_routeParameterName}' is missing or invalid.")));
    }
}
