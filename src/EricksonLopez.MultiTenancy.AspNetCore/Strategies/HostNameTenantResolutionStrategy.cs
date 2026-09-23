// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy.AspNetCore.Options;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.AspNetCore.Strategies;

/// <summary>
/// Resolves the tenant identifier from the request host by looking up the subdomain in the tenant store.
/// </summary>
public sealed partial class HostNameTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly HostNameTenantResolutionStrategyOptions _options;
    private readonly Microsoft.Extensions.Logging.ILogger<HostNameTenantResolutionStrategy>? _logger;

    /// <inheritdoc />
    public string StrategyName => "HostName";

    /// <inheritdoc />
    public TenantResolutionSource Source => TenantResolutionSource.Host;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostNameTenantResolutionStrategy"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="serviceProvider">The service provider used to resolve the tenant store.</param>
    /// <param name="options">The optional configuration options for hostname-based resolution.</param>
    /// <param name="logger">The optional logger for resolution diagnostic events.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> or <paramref name="serviceProvider"/> is <see langword="null"/></exception>
    public HostNameTenantResolutionStrategy(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IOptions<HostNameTenantResolutionStrategyOptions>? options = null,
        Microsoft.Extensions.Logging.ILogger<HostNameTenantResolutionStrategy>? logger = null)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? new HostNameTenantResolutionStrategyOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    [SuppressMessage("Major Code Smell", "S3776:Cognitive Complexity of methods should not be too high", Justification = "Subdomain parsing and fallback tenant store resolution logic requires nested condition branches.")]
    public async ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is not null)
        {
            var host = context.Request.Host.Host.TrimEnd('.');
            // Stryker disable once logical : Host name validation
            if (string.IsNullOrEmpty(host) || IPAddress.TryParse(host, out _))
            {
                // Stryker disable once string : Error description
                return Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, "Host does not match expected tenant subdomain format or tenant not found."));
            }

            var subdomain = ExtractSubdomain(host);
            if (!string.IsNullOrEmpty(subdomain))
            {
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
                        // Stryker disable once string : Stashed resolved tenant key
                        context.Items["__TenantResolution_ResolvedTenant"] = result.Value;
                        return Result<TenantId>.Success(result.Value.Id);
                    }
                }
                else
                {
                    // Fallback to GUID parsing for stores that do not implement ITenantLookupStore
                    if (TenantId.TryCreate(subdomain.AsSpan(), out var tenantId))
                    {
                        // Stryker disable once boolean : ConfigureAwait is not verifiable
                        var result = await tenantStore.GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
                        if (result.IsSuccess)
                        {
                            // Stryker disable once string : Stashed resolved tenant key
                            context.Items["__TenantResolution_ResolvedTenant"] = result.Value;
                            return Result<TenantId>.Success(tenantId);
                        }
                    }
                    else if (_logger is not null)
                    {
                        // Stryker disable once statement : Diagnostics logging
                        LogFallbackGuidParsingSkipped(_logger, subdomain);
                    }
                }
            }
        }

        return Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, "Host does not match expected tenant subdomain format or tenant not found."));
    }

    private string? ExtractSubdomain(string host)
    {
        if (!string.IsNullOrEmpty(_options.ExtractionPattern))
        {
            var match = Regex.Match(host, _options.ExtractionPattern, RegexOptions.None, TimeSpan.FromMilliseconds(200));
            if (match.Success)
            {
                var group = match.Groups["tenant"];
                // Stryker disable once logical : Regex named capture group validation
                if (group.Success && !string.IsNullOrEmpty(group.Value))
                {
                    return group.Value;
                }
            }
        }

        if (!string.IsNullOrEmpty(_options.BaseDomain))
        {
            var baseDomain = _options.BaseDomain.TrimStart('.');
            if (host.EndsWith(baseDomain, StringComparison.OrdinalIgnoreCase))
            {
                var prefixLength = host.Length - baseDomain.Length;
                // Stryker disable once logical, equality : Subdomain prefix length check
                if (prefixLength > 1 && host[prefixLength - 1] == '.')
                {
                    var prefix = host[..(prefixLength - 1)];
                    var segments = prefix.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    // Stryker disable once equality : Non-empty prefix segments check
                    if (segments.Length > 0)
                    {
                        // Stryker disable once arithmetic : Subdomain index from right
                        var targetIndex = segments.Length - 1 - _options.SubdomainIndexFromRight;
                        // Stryker disable once logical, equality : Subdomain index bounds check
                        if (targetIndex >= 0 && targetIndex < segments.Length)
                        {
                            return segments[targetIndex];
                        }
                    }
                }
            }
        }

        // Default behavior: first dot split
        ReadOnlySpan<char> hostSpan = host.AsSpan();
        int firstDot = hostSpan.IndexOf('.');
        // Stryker disable once equality : Positive first dot index
        if (firstDot > 0)
        {
            int secondDot = hostSpan.Slice(firstDot + 1).IndexOf('.');
            bool isLocalhost = hostSpan.Slice(firstDot + 1).Equals("localhost".AsSpan(), StringComparison.OrdinalIgnoreCase);
            if (secondDot != -1 || isLocalhost)
            {
                return host[..firstDot];
            }
        }

        return null;
    }

    [Microsoft.Extensions.Logging.LoggerMessage(
        EventId = 1,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "ITenantStore does not implement ITenantLookupStore. Fallback GUID parsing skipped for non-GUID subdomain '{Subdomain}'.")]
    private static partial void LogFallbackGuidParsingSkipped(Microsoft.Extensions.Logging.ILogger logger, string subdomain);
}
