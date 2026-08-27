// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.Result;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level08_Customization;

/// <summary>
/// Custom enterprise tenant metadata contract and implementation.
/// </summary>
public record CustomEnterpriseTenant : ITenantInfo
{
    public TenantId Id { get; init; } = TenantId.NewId();
    public string Name { get; init; } = string.Empty;
    public string? ConnectionString { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    // Extended domain fields
    public string SubscriptionPlan { get; init; } = "EnterprisePlus";
    public int MaxConcurrentConnections { get; init; } = 500;
    public string SsoMetadataUrl { get; init; } = string.Empty;
}

/// <summary>
/// Custom resolution strategy reading from a custom protocol/header.
/// </summary>
public class CustomApiKeyTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly string _apiKey;

    public string StrategyName => "CustomApiKey";

    public CustomApiKeyTenantResolutionStrategy(string apiKey)
    {
        _apiKey = apiKey;
    }

    public ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        // Example: Map API key to tenant ID
        if (_apiKey == "key-acme-prod-12345")
        {
            return ValueTask.FromResult(Result<TenantId>.Success(TenantId.Create("11111111-1111-1111-1111-111111111111")));
        }

        return ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.StrategyFailed(StrategyName, "Invalid API key")));
    }
}

/// <summary>
/// Level 8 — Customization: Strongly-typed tenant models and custom resolution strategy implementations.
/// </summary>
public static class CustomizationDemo
{
    public static void ConfigureCustomServices(IServiceCollection services)
    {
        // Register strongly-typed multi-tenancy infrastructure
        services.AddMultiTenancy<CustomEnterpriseTenant>();

        // Register in-memory store for custom tenant type
        services.AddInMemoryTenantStore<CustomEnterpriseTenant>(store =>
        {
            store.AddOrUpdate(new CustomEnterpriseTenant
            {
                Id = TenantId.Create("11111111-1111-1111-1111-111111111111"),
                Name = "acme-enterprise",
                SubscriptionPlan = "MissionCritical",
                MaxConcurrentConnections = 1000,
                SsoMetadataUrl = "https://sso.acme.com/saml2"
            });
        });
    }

    public static void DemonstrateTypedContext(ITenantContext<CustomEnterpriseTenant> typedContext)
    {
        if (typedContext.IsResolved)
        {
            var customTenant = typedContext.Tenant;
            Console.WriteLine($"Typed Tenant Resolved: Plan={customTenant?.SubscriptionPlan}, MaxConns={customTenant?.MaxConcurrentConnections}");
        }
    }
}
