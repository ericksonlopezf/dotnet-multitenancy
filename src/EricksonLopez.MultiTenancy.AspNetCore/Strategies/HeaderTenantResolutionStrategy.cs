// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Http;

namespace EricksonLopez.MultiTenancy.AspNetCore.Strategies;

/// <summary>
/// Resolves the tenant identifier from an HTTP request header.
/// </summary>
/// <remarks>
/// <para>
/// <b>SECURITY WARNING:</b> Using this strategy in a public-facing API without a trusted reverse proxy
/// or API gateway that sanitizes incoming headers makes the application vulnerable to <i>Tenant Spoofing</i>.
/// An attacker could supply an arbitrary tenant ID header to impersonate another tenant.
/// </para>
/// <para>
/// For internal microservices behind a trusted gateway, use <see cref="InternalGatewayHeaderTenantResolutionStrategy"/>
/// which validates a shared gateway secret, or configure the API gateway to definitively overwrite this header
/// based on verified authentication tokens.
/// </para>
/// </remarks>
public sealed class HeaderTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _headerName;

    /// <inheritdoc />
    public string StrategyName => "Header";

    /// <inheritdoc />
    public TenantResolutionSource Source => TenantResolutionSource.Header;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="headerName">The header name used to extract the tenant identifier (default: <c>X-Tenant-ID</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/></exception>
    public HeaderTenantResolutionStrategy(IHttpContextAccessor httpContextAccessor, string headerName = "X-Tenant-ID")
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _headerName = string.IsNullOrWhiteSpace(headerName) ? "X-Tenant-ID" : headerName;
    }

    /// <inheritdoc />
    public ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is not null && context.Request.Headers.TryGetValue(_headerName, out var values))
        {
            var headerValue = values.ToString();
            if (TenantId.TryCreate(headerValue, out var tenantId))
            {
                return ValueTask.FromResult(Result<TenantId>.Success(tenantId));
            }

            return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.InvalidId(headerValue)));
        }

        return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, $"Header '{_headerName}' is missing.")));
    }
}
