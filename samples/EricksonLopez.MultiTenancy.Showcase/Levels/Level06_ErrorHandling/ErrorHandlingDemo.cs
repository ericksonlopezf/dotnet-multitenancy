// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;
using EricksonLopez.MultiTenancy.AspNetCore.Strategies;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using EricksonLopez.MultiTenancy.Testing;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level06_ErrorHandling;

/// <summary>
/// Level 6 — Error Handling: Conflict detection (fail-closed ADR-008), inactive tenants, and domain error inspection.
/// </summary>
public static class ErrorHandlingDemo
{
    public static async Task DemonstrateConflictDetectionAsync()
    {
        Console.WriteLine("--- Demonstrating Fail-Closed Resolution Conflict Detection (ADR-008) ---");

        var store = new FakeTenantStore([SeedTenants.Acme, SeedTenants.Globex]);
        var accessor = new ScopedTenantContextAccessor();

        // Strategy 1 resolves Acme
        var strat1 = new FakeTenantResolutionStrategy(SeedTenants.AcmeId, "JwtClaims");
        // Strategy 2 resolves Globex (Conflicting!)
        var strat2 = new FakeTenantResolutionStrategy(SeedTenants.GlobexId, "Header");

        var middleware = new TenantResolutionMiddleware(
            next: (HttpContext ctx) => Task.CompletedTask,
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var httpContext = new DefaultHttpContext();
        var strategies = new List<ITenantResolutionStrategy> { strat1, strat2 };

        try
        {
            await middleware.InvokeAsync(httpContext, strategies, store, accessor);
            Console.WriteLine("ERROR: Middleware should have thrown a conflict exception!");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"[Expected Fail-Closed Security] Resolution conflict caught: {ex.Message}");
        }
    }

    public static void DemonstrateInactiveTenantHandling()
    {
        Console.WriteLine("--- Demonstrating Inactive/Suspended Tenant Handling ---");

        var inactiveTenant = SeedTenants.InactiveTenant;
        var error = TenantErrors.Inactive(inactiveTenant.Id);

        Console.WriteLine($"Domain Error: Code='{error.Code}', Description='{error.Description}'");

        try
        {
            if (!inactiveTenant.IsActive)
            {
                throw new TenantInactiveException(inactiveTenant.Id, $"Tenant '{inactiveTenant.Name}' account is suspended.");
            }
        }
        catch (TenantInactiveException ex)
        {
            Console.WriteLine($"[Expected Inactive Exception] Inactive tenant rejected: {ex.Message} (TenantId: {ex.TenantId})");
        }
    }

    public static void DemonstrateUnresolvedContextHandling()
    {
        Console.WriteLine("--- Demonstrating Unresolved Context Guard ---");

        ITenantContext context = TenantContext.Empty;

        Console.WriteLine($"IsResolved: {context.IsResolved}");
        Console.WriteLine($"Source: {context.Source}");

        try
        {
            // Accessing RequiredTenant throws TenantNotFoundException when not resolved
            _ = context.RequiredTenant;
        }
        catch (TenantNotFoundException ex)
        {
            Console.WriteLine($"[Expected Guard] Attempt to access RequiredTenant on unresolved context threw: {ex.Message}");
        }
    }
}
