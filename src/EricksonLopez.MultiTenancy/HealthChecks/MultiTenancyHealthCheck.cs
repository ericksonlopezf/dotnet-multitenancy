// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.HealthChecks;

/// <summary>
/// Provides a health check that verifies multi-tenant resolution readiness and tenant store availability.
/// </summary>
public sealed class MultiTenancyHealthCheck : IHealthCheck
{
    private readonly ITenantStore? _tenantStore;
    private readonly MultiTenancyHealthCheckOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiTenancyHealthCheck"/> class.
    /// </summary>
    /// <param name="tenantStore">The optional tenant store instance to check.</param>
    /// <param name="options">The optional health check configuration options.</param>
    public MultiTenancyHealthCheck(
        ITenantStore? tenantStore = null,
        IOptions<MultiTenancyHealthCheckOptions>? options = null)
    {
        _tenantStore = tenantStore;
        _options = options?.Value ?? new MultiTenancyHealthCheckOptions();
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = _options.IncludeDiagnosticData
            ? new Dictionary<string, object>
            {
                ["store_registered"] = _tenantStore is not null,
                ["store_type"] = _tenantStore?.GetType().Name ?? "None",
                ["supports_lookup"] = _tenantStore is ITenantLookupStore
            }
            : null;

        if (_tenantStore is null)
        {
            return new HealthCheckResult(
                _options.FailureStatus,
                description: "Tenant store is not registered in the service provider.",
                data: data);
        }

        if (_options.StoreProbe is not null)
        {
            try
            {
                // Stryker disable once boolean : ConfigureAwait is not verifiable
                var probeSuccess = await _options.StoreProbe(_tenantStore, cancellationToken).ConfigureAwait(false);
                if (!probeSuccess)
                {
                    return HealthCheckResult.Degraded(
                        description: "Tenant store probe check failed.",
                        data: data);
                }
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    description: $"Tenant store probe threw an exception: {ex.Message}",
                    exception: ex,
                    data: data);
            }
        }

        return HealthCheckResult.Healthy(
            description: "Multi-tenancy subsystem and tenant store are active and ready.",
            data: data);
    }
}
