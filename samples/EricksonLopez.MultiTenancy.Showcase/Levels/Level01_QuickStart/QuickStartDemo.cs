// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level01_QuickStart;

/// <summary>
/// Level 1 — Quick Start: Minimal setup, service registration, pipeline middleware, and tenant context resolution.
/// </summary>
public static class QuickStartDemo
{
    /// <summary>
    /// Demonstrates minimal DI configuration.
    /// </summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        // 1. Register core multi-tenancy primitives (ITenantContext, ITenantContextAccessor, ITenantScopeFactory)
        services.AddMultiTenancy();

        // 2. Register ASP.NET Core resolution middleware and ClaimTenantResolutionStrategy by default
        services.AddAspNetCoreMultiTenancy();

        // 3. Register and seed an in-memory tenant store for development and testing
        services.AddInMemoryTenantStore<TenantInfo>(store =>
        {
            store.AddOrUpdate(SeedTenants.Acme);
            store.AddOrUpdate(SeedTenants.Globex);
        });
    }

    /// <summary>
    /// Demonstrates pipeline configuration and endpoint handling.
    /// </summary>
    public static void ConfigureApp(WebApplication app)
    {
        // 4. Activate tenant resolution middleware in the request pipeline
        app.UseMultiTenancy();

        // 5. Endpoint resolving ambient ITenantContext
        app.MapGet("/api/quickstart/current-tenant", (ITenantContext tenantContext) =>
        {
            if (!tenantContext.IsResolved)
            {
                return Results.Ok(new
                {
                    Status = "Unresolved",
                    Message = "No tenant resolved for this request."
                });
            }

            var tenant = tenantContext.RequiredTenant;
            return Results.Ok(new
            {
                Status = "Resolved",
                TenantId = tenant.Id.ToString(),
                TenantName = tenant.Name,
                ResolutionSource = tenantContext.Source.ToString(),
                IsActive = tenant.IsActive
            });
        });
    }
}
