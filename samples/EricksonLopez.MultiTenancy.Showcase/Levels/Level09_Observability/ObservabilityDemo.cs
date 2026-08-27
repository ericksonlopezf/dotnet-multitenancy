// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.HealthChecks;
using EricksonLopez.MultiTenancy.OpenTelemetry;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level09_Observability;

/// <summary>
/// Level 9 — Observability: OpenTelemetry distributed tracing, metrics, W3C baggage, and subsystem health checks.
/// </summary>
public static class ObservabilityDemo
{
    public static void ConfigureObservabilityServices(IServiceCollection services)
    {
        // 1. Register OpenTelemetry tracing enricher
        services.AddMultiTenancyOpenTelemetry();

        // 2. Register multi-tenancy health check with custom probe
        services.AddMultiTenancyHealthCheck(options =>
        {
            // FailureStatus controls what health status to report on probe failure
            options.FailureStatus = HealthStatus.Degraded;
            options.IncludeDiagnosticData = true;
            options.StoreProbe = async (store, cancellationToken) =>
            {
                // Verify store responds to query
                var testQuery = await store.GetTenantAsync(SeedTenants.AcmeId, cancellationToken);
                return testQuery.IsSuccess;
            };
        });

        // 3. When wiring OpenTelemetry SDK, register the canonical source and meter names:
        //    tracerProviderBuilder.AddSource(TenantActivitySource.ActivitySourceName)
        //    meterProviderBuilder.AddMeter(TenantMetrics.MeterName)
        Console.WriteLine($"Register ActivitySource: '{TenantActivitySource.ActivitySourceName}'");
        Console.WriteLine($"Register Meter: '{TenantMetrics.MeterName}'");
    }

    /// <summary>
    /// Demonstrates tracing and metric recording during a tenant request lifecycle.
    /// </summary>
    public static void DemonstrateTelemetryWorkflow(ITenantContext tenantContext)
    {
        Console.WriteLine("--- Demonstrating OpenTelemetry Distributed Tracing & Metrics ---");

        using var activity = TenantActivitySource.Source.StartActivity("ProcessTenantOrder");

        if (activity is not null)
        {
            // Enrich current activity with tenant tags (tenant.id, tenant.name, tenant.source, tenant.is_active)
            activity.EnrichWithTenant(tenantContext);

            if (tenantContext.IsResolved)
            {
                // Set W3C Baggage for cross-service propagation
                activity.SetTenantBaggage(tenantContext.RequiredTenant.Id);

                // Record success metrics
                TenantMetrics.RecordResolutionSuccess(
                    strategyName: "ClaimTenantResolutionStrategy",
                    source: tenantContext.Source,
                    durationMs: 1.45);
            }
            else
            {
                // Record resolution failure
                var error = TenantErrors.Unresolved;
                activity.RecordTenantResolutionFailure(error, strategyName: "HeaderTenantResolutionStrategy");

                TenantMetrics.RecordResolutionFailure(
                    strategyName: "HeaderTenantResolutionStrategy",
                    errorCode: error.Code,
                    durationMs: 0.82);

                // Record a resolution conflict if two strategies returned different tenant IDs
                // This counter drives the 'tenant.resolution.conflicts' metric in your OTel backend
                TenantMetrics.RecordResolutionConflict(
                    firstStrategy: "ClaimTenantResolutionStrategy",
                    secondStrategy: "HeaderTenantResolutionStrategy");
            }
        }
    }
}
