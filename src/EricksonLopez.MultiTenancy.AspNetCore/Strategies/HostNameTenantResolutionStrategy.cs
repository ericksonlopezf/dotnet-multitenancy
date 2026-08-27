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
/// Resolves the tenant identifier from the request host by looking up the subdomain in the tenant store.
/// </summary>
public sealed class HostNameTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;

    /// <inheritdoc />
    public string StrategyName => "HostName";

    /// <summary>
    /// Initializes a new instance of the <see cref="HostNameTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="serviceProvider">The service provider used to resolve the tenant store.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> or <paramref name="serviceProvider"/> is <see langword="null"/></exception>
    public HostNameTenantResolutionStrategy(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    [SuppressMessage("Major Code Smell", "S3776:Cognitive Complexity of methods should not be too high", Justification = "Subdomain parsing and fallback tenant store resolution logic requires nested condition branches.")]
    public async ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is not null)
        {
            var host = context.Request.Host.Host;
            int firstDot = host.IndexOf('.');
            if (firstDot > 0)
            {
                int secondDot = host.IndexOf('.', firstDot + 1);
                if (secondDot != -1)
                {
                    var subdomain = host[..firstDot];
                    var tenantStore = _serviceProvider.GetService<ITenantStore>();

                    if (tenantStore is null)
                    {
                        return Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, "ITenantStore is not registered."));
                    }

                    if (tenantStore is ITenantLookupStore lookupStore)
                    {
                        // Stryker disable once boolean : ConfigureAwait is not verifiable
                        var result = await lookupStore.GetTenantByIdentifierAsync(subdomain, cancellationToken).ConfigureAwait(false);
                        if (result.IsSuccess)
                        {
                            return Result<TenantId>.Success(result.Value.Id);
                        }
                    }
                    else
                    {
                        // Fallback to GUID parsing for stores that do not implement ITenantLookupStore
                        if (TenantId.TryCreate(subdomain, out var tenantId))
                        {
                            // Stryker disable once boolean : ConfigureAwait is not verifiable
                            var result = await tenantStore.GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
                            if (result.IsSuccess)
                            {
                                return Result<TenantId>.Success(tenantId);
                            }
                        }
                    }
                }
            }
        }

        return Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, "Host does not match expected tenant subdomain format or tenant not found."));
    }
}
