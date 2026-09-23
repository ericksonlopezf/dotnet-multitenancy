// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;
using EricksonLopez.MultiTenancy.AspNetCore.Options;
using EricksonLopez.MultiTenancy.Authentication;
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

        // 2. Multi-strategy resolution setup with explicit middleware options
        //    - All five TenantResolutionMiddlewareOptions properties demonstrated
        services.AddAspNetCoreMultiTenancy(options =>
        {
            // Fail-closed (safe by default): 404 when resolved ID not found in the store.
            options.FailOnStoreMiss = true;
            // When true: returns RFC 7807 ProblemDetails (409) on conflict instead of throwing.
            options.WriteProblemDetailsOnConflict = true;
            // When true: strategy names are disclosed in the conflict response (disable in production).
            options.IncludeStrategyDetailsInConflictResponse = false;
            // When true: the JWT/cookie tenant_id claim must match the resolved tenant.
            options.ValidateAuthenticatedPrincipalTenantClaim = true;
            // The claim type used to identify the tenant in the authenticated principal.
            options.TenantClaimType = "tenant_id";
        });

        // 3. Internal header resolution (authenticated with gateway secret)
        services.AddInternalHeaderTenantResolution("gateway-secret-token");

        // 4. Subdomain-based host resolution with explicit HostNameTenantResolutionStrategyOptions
        services.AddHostNameTenantStrategy(opts =>
        {
            // Strip base domain so "acme.platform.example.com" → extracts "acme"
            opts.BaseDomain = "platform.example.com";
            // SubdomainIndexFromRight: 0 = immediate segment before BaseDomain
            opts.SubdomainIndexFromRight = 0;
            // Optional regex with named capture group to extract tenant from complex hostnames
            opts.ExtractionPattern = null; // e.g. @"(?<tenant>[a-z0-9\-]+)\.platform\.example\.com"
        });

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

        // 9. Per-tenant isolated options — Overload 1 (no configure action)
        services.AddPerTenantOptions<SampleTenantSettings, TenantInfo>();

        // 9a. Per-tenant options — Overload 2 (with Action<TOptions, TTenant> configure action)
        services.AddPerTenantOptions<SampleTenantSettings, TenantInfo>((settingsOptions, tenant) =>
        {
            // Customize options based on the active tenant's metadata
            settingsOptions.DefaultCurrency = tenant.Properties.TryGetValue("Currency", out var cur) ? cur : "USD";
            settingsOptions.MaxUsers = tenant.IsActive ? 100 : 0;
        });

        // 9b. Per-tenant options — Overload 3 (with Action<TenantOptionsCacheOptions>)
        services.AddPerTenantOptions<SampleTenantSettings, TenantInfo>(cache =>
        {
            // Max number of cached tenant options instances before LRU eviction
            cache.MaxCapacity = 500;
            // Throw TenantNotFoundException when resolving options with no tenant in scope
            cache.ThrowOnMissingTenant = false;
        });

        // 9c. Per-tenant options — Overload 4 (both cache options and configure action)
        services.AddPerTenantOptions<SampleTenantSettings, TenantInfo>(
            configureCache: cache => { cache.MaxCapacity = 1000; },
            configureOptions: (settingsOptions, tenant) => { settingsOptions.MaxUsers = 500; });

        // 10. Per-tenant authentication — Overload 1 (no configure action)
        services.AddPerTenantAuthentication<TenantInfo>();

        // 10a. Per-tenant authentication — Overload 2 (with DefaultSchemeSelector for dynamic scheme per tenant)
        services.AddPerTenantAuthentication<TenantInfo>(authOpts =>
        {
            // Route different authentication schemes to different tenants
            authOpts.DefaultSchemeSelector = tenant =>
                tenant.Properties.TryGetValue("AuthScheme", out var scheme) ? scheme : "Cookies";
        });

        // 11. Store configuration from appsettings.json section
        services.AddConfigurationTenantStore<TenantInfo>(options =>
        {
            var section = configuration.GetSection("MultiTenancy:Tenants");
            var tenants = section.Get<System.Collections.Generic.List<TenantInfo>>() ?? new();
            options.Tenants = tenants;
        });

        // 12. Memory Cache decorator over the registered ITenantStore
        services.AddMemoryCache();
        services.AddCachedTenantStore<TenantInfo>(options =>
        {
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            options.SlidingExpiration = TimeSpan.FromMinutes(2);
            // Negative caching prevents repeated store lookups for non-existent tenant IDs
            options.EnableNegativeCaching = true;
            options.NegativeCacheExpirationRelativeToNow = TimeSpan.FromSeconds(30);
        });

        // 13. Remote HTTP store configuration (demonstration setup)
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
