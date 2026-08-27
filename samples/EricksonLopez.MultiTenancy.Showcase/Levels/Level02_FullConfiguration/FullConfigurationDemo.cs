// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore.Options;
using EricksonLopez.MultiTenancy.Configuration;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using EricksonLopez.MultiTenancy.Stores;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level02_FullConfiguration;

/// <summary>
/// Sample options class configured per-tenant.
/// </summary>
public class SampleTenantSettings
{
    public string DefaultCurrency { get; set; } = "USD";
    public int MaxUsers { get; set; } = 50;
}

/// <summary>
/// Level 2 — Full Configuration: Multi-strategy resolution, per-tenant options, configuration store, cached store, and remote store.
/// </summary>
public static class FullConfigurationDemo
{
    public static void ConfigureFullServices(IServiceCollection services, IConfiguration configuration)
    {
        // 1. Core multi-tenancy registration
        services.AddMultiTenancy();

        // 2. Multi-strategy resolution setup (Claims evaluated first)
        services.AddAspNetCoreMultiTenancy();

        // 3. Internal header resolution (for internal cluster/service-to-service calls)
        services.AddInternalHeaderTenantResolution();

        // 4. Subdomain-based host resolution (e.g. acme.app.example.com -> acme-corp)
        services.AddHostNameTenantStrategy();

        // 5. Route-based resolution (e.g. /api/tenants/{tenantId}/...)
        services.AddRouteTenantStrategy("tenantId");

        // 6. Base path segment resolution (e.g. /acme-corp/api/v1/...)
        services.AddBasePathStrategy(segmentIndex: 0);

        // 7. Static tenant strategy (for single-tenant fallback, tests, or fixed routing)
        services.AddStaticTenantStrategy(SeedTenants.AcmeId);

        // 8. Custom delegate strategy
        services.AddDelegateTenantStrategy(cancellationToken =>
        {
            // Custom resolution logic (e.g. query param fallback)
            return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.StrategyFailed("Delegate", "No delegate parameter")));
        });

        // 8. Per-tenant isolated options cache
        services.AddPerTenantOptions<SampleTenantSettings, TenantInfo>();

        // 9. Store configuration from appsettings.json section
        services.AddConfigurationTenantStore<TenantInfo>(options =>
        {
            var section = configuration.GetSection("MultiTenancy:Tenants");
            var tenants = section.Get<System.Collections.Generic.List<TenantInfo>>() ?? new();
            options.Tenants = tenants;
        });

        // 10. Memory Cache decorator over the registered ITenantStore
        services.AddMemoryCache();
        services.AddCachedTenantStore<TenantInfo>(options =>
        {
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            options.SlidingExpiration = TimeSpan.FromMinutes(2);
        });

        // 11. Remote HTTP store configuration (demonstration setup)
        services.AddHttpRemoteTenantStore<TenantInfo>(options =>
        {
            options.BaseAddress = new Uri("https://tenant-service.internal");
            options.EndpointTemplate = "/api/v1/tenants/{0}";
            options.IdentifierEndpointTemplate = "/api/v1/tenants/by-identifier/{0}";
            options.Timeout = TimeSpan.FromSeconds(3);
        });
    }

    public static void MapConfigurationEndpoints(WebApplication app)
    {
        app.MapGet("/api/config/tenant-settings", (
            ITenantContext tenantContext,
            IOptionsMonitor<SampleTenantSettings> optionsMonitor) =>
        {
            if (!tenantContext.IsResolved)
            {
                return Results.BadRequest(new { Error = "Tenant must be resolved to read per-tenant settings." });
            }

            var settings = optionsMonitor.CurrentValue;
            return Results.Ok(new
            {
                TenantId = tenantContext.RequiredTenant.Id.ToString(),
                TenantName = tenantContext.RequiredTenant.Name,
                Settings = settings
            });
        });
    }
}
