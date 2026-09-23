// Copyright © Erickson Lopez. MIT License.
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Http;

namespace EricksonLopez.MultiTenancy.AspNetCore.Strategies;

/// <summary>
/// Resolves the tenant identifier from authenticated user claims.
/// </summary>
public sealed class ClaimTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _claimType;

    /// <inheritdoc />
    public string StrategyName => "Claims";

    /// <inheritdoc />
    public TenantResolutionSource Source => TenantResolutionSource.JwtClaim;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="claimType">The claim type representing the tenant identifier (default: <c>tenant_id</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/></exception>
    public ClaimTenantResolutionStrategy(IHttpContextAccessor httpContextAccessor, string claimType = "tenant_id")
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _claimType = string.IsNullOrWhiteSpace(claimType) ? "tenant_id" : claimType;
    }

    /// <inheritdoc />
    public ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is not null && user.Identity?.IsAuthenticated == true)
        {
            var claimValue = user.FindFirst(_claimType)?.Value
                ?? user.FindFirst("tid")?.Value
                ?? user.FindFirst("tenant")?.Value;

            if (TenantId.TryCreate(claimValue, out var tenantId))
            {
                return ValueTask.FromResult(Result<TenantId>.Success(tenantId));
            }

            if (!string.IsNullOrWhiteSpace(claimValue))
            {
                return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.InvalidId(claimValue)));
            }
        }

        return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, "User is not authenticated or tenant claim is missing.")));
    }
}
