// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy.AspNetCore.Strategies;

/// <summary>
/// Resolves the tenant identifier from a URL base path segment.
/// </summary>
public sealed class BasePathTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider? _serviceProvider;
    private readonly int _segmentIndex;

    /// <inheritdoc />
    public string StrategyName => "BasePath";

    /// <inheritdoc />
    public TenantResolutionSource Source => TenantResolutionSource.Route;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasePathTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="serviceProvider">The optional service provider used to resolve the tenant lookup store.</param>
    /// <param name="segmentIndex">The zero-based path segment index containing the tenant identifier.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="segmentIndex"/> is less than zero</exception>
    public BasePathTenantResolutionStrategy(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider? serviceProvider = null,
        int segmentIndex = 0)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _serviceProvider = serviceProvider;
        _segmentIndex = segmentIndex >= 0 ? segmentIndex : throw new ArgumentOutOfRangeException(nameof(segmentIndex), "Segment index must be non-negative.");
    }

    /// <inheritdoc />
    [SuppressMessage("Major Code Smell", "S3776:Cognitive Complexity of methods should not be too high", Justification = "Hierarchical fallback resolution logic inherently involves sequential fallback branches.")]
    public async ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is not null)
        {
            var path = context.Request.Path.Value;
            if (!string.IsNullOrWhiteSpace(path) && TryGetPathSegment(path, _segmentIndex, out var identifier))
            {
                var tenantStore = _serviceProvider?.GetService<ITenantStore>();
                if (tenantStore is ITenantLookupStore lookupStore)
                {
                    // Stryker disable once boolean : ConfigureAwait is not verifiable
                    var result = await lookupStore.GetTenantByIdentifierAsync(identifier, cancellationToken).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        return Result<TenantId>.Success(result.Value.Id);
                    }
                }
                else if (tenantStore is not null)
                {
                    if (TenantId.TryCreate(identifier, out var tenantId))
                    {
                        // Stryker disable once boolean : ConfigureAwait is not verifiable
                        var result = await tenantStore.GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
                        if (result.IsSuccess)
                        {
                            return Result<TenantId>.Success(tenantId);
                        }
                    }
                }
                else
                {
                    if (TenantId.TryCreate(identifier, out var tenantId))
                    {
                        return Result<TenantId>.Success(tenantId);
                    }
                }

                if (!string.IsNullOrWhiteSpace(identifier))
                {
                    return Result<TenantId>.Failure(TenantErrors.InvalidId(identifier));
                }
            }
        }

        return Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, $"Base path segment at index {_segmentIndex} is missing or invalid."));
    }

    private static bool TryGetPathSegment(string path, int targetIndex, out string segment)
    {
        ReadOnlySpan<char> span = path.AsSpan();
        int currentIndex = 0;

        while (!span.IsEmpty)
        {
            while (!span.IsEmpty && span[0] == '/')
            {
                span = span.Slice(1);
            }

            if (span.IsEmpty)
            {
                break;
            }

            int slashIndex = span.IndexOf('/');
            ReadOnlySpan<char> currentSegment = slashIndex >= 0 ? span.Slice(0, slashIndex) : span;

            if (currentIndex == targetIndex)
            {
                segment = currentSegment.ToString();
                return true;
            }

            currentIndex++;
            span = slashIndex >= 0 ? span.Slice(slashIndex + 1) : default;
        }

        segment = string.Empty;
        return false;
    }
}
