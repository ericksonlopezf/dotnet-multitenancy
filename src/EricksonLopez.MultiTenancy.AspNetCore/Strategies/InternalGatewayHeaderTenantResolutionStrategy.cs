// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Http;

namespace EricksonLopez.MultiTenancy.AspNetCore.Strategies;

/// <summary>
/// Resolves the tenant identifier from an HTTP request header authenticated by a shared gateway secret.
/// </summary>
public sealed class InternalGatewayHeaderTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _expectedSharedSecret;
    private readonly string _headerName;
    private readonly string _secretHeaderName;

    /// <inheritdoc />
    public string StrategyName => "InternalGatewayHeader";

    /// <inheritdoc />
    public TenantResolutionSource Source => TenantResolutionSource.Header;

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalGatewayHeaderTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="expectedSharedSecret">The expected shared secret from the trusted reverse proxy / API gateway.</param>
    /// <param name="headerName">The header name used to extract the tenant identifier (default: <c>X-Tenant-ID</c>).</param>
    /// <param name="secretHeaderName">The header name containing the shared gateway secret (default: <c>X-Gateway-Secret</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="expectedSharedSecret"/> is <see langword="null"/> or whitespace</exception>
    public InternalGatewayHeaderTenantResolutionStrategy(
        IHttpContextAccessor httpContextAccessor,
        string expectedSharedSecret,
        string headerName = "X-Tenant-ID",
        string secretHeaderName = "X-Gateway-Secret")
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        if (string.IsNullOrWhiteSpace(expectedSharedSecret))
        {
            // Stryker disable once string : Exception message
            throw new ArgumentException("Expected shared secret cannot be null or whitespace.", nameof(expectedSharedSecret));
        }

        _expectedSharedSecret = expectedSharedSecret;
        // Stryker disable once conditional, string : Header fallback default
        _headerName = string.IsNullOrWhiteSpace(headerName) ? "X-Tenant-ID" : headerName;
        // Stryker disable once conditional, string : Secret header fallback default
        _secretHeaderName = string.IsNullOrWhiteSpace(secretHeaderName) ? "X-Gateway-Secret" : secretHeaderName;
    }

    /// <inheritdoc />
    public ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, "HttpContext is unavailable.")));
        }

        if (!context.Request.Headers.TryGetValue(_secretHeaderName, out var secretValues) ||
            !string.Equals(secretValues.ToString(), _expectedSharedSecret, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, $"Gateway secret header '{_secretHeaderName}' is missing or invalid.")));
        }

        if (context.Request.Headers.TryGetValue(_headerName, out var values))
        {
            var headerValue = values.ToString();
            if (TenantId.TryCreate(headerValue, out var tenantId))
            {
                return ValueTask.FromResult(Result<TenantId>.Success(tenantId));
            }

            return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.InvalidId(headerValue)));
        }

        // Stryker disable once string : Missing header error detail
        return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, $"Header '{_headerName}' is missing.")));
    }
}
