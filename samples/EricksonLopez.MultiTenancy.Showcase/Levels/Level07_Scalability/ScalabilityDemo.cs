// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Showcase.Infrastructure;
using EricksonLopez.MultiTenancy.Sqlite;
using EricksonLopez.MultiTenancy.Stores;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level07_Scalability;

/// <summary>
/// Level 7 — Scalability: SQLite Database-per-tenant pattern and high-throughput store caching.
/// </summary>
public static class ScalabilityDemo
{
    /// <summary>
    /// Demonstrates SQLite Database-per-tenant connection factory routing.
    /// </summary>
    public static void DemonstrateDatabasePerTenant()
    {
        Console.WriteLine("--- Demonstrating SQLite Database-per-Tenant Factory ---");

        // ISqliteTenantConnectionFactory is the DI-injectable interface for the factory.
        // Program to the interface when registering in your container:
        //   services.AddSingleton<ISqliteTenantConnectionFactory>(_ =>
        //       new SqliteTenantConnectionFactory("Data Source=tenants/{TenantId}.db;"));
        ISqliteTenantConnectionFactory factory = new SqliteTenantConnectionFactory("Data Source=tenants_data/{TenantId}_{Name}.db;");

        string acmeConnStr = factory.BuildConnectionString(SeedTenants.Acme);
        string globexConnStr = factory.BuildConnectionString(SeedTenants.Globex);

        Console.WriteLine($"Acme Connection String: {acmeConnStr}");
        Console.WriteLine($"Globex Connection String: {globexConnStr}");

        // factory.CreateConnection(tenant) returns a DbConnection ready to open
        // (creates directory structure automatically for file-based SQLite databases)
    }

    /// <summary>
    /// Demonstrates caching store decorator to minimize database/network overhead.
    /// </summary>
    public static async Task DemonstrateCachedStoreAsync()
    {
        Console.WriteLine("--- Demonstrating CachedTenantStore Decorator ---");

        var underlyingStore = new InMemoryTenantStore<TenantInfo>([SeedTenants.Acme]);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var options = Microsoft.Extensions.Options.Options.Create(new CachedTenantStoreOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            SlidingExpiration = TimeSpan.FromMinutes(1)
        });

        var cachedStore = new CachedTenantStore<TenantInfo>(underlyingStore, memoryCache, options);

        // First call loads from underlying store into cache
        var result1 = await cachedStore.GetTenantAsync(SeedTenants.AcmeId);
        Console.WriteLine($"First lookup (cache miss): Success={result1.IsSuccess}, Tenant={result1.Value.Name}");

        // Second call served directly from memory cache
        var result2 = await cachedStore.GetTenantAsync(SeedTenants.AcmeId);
        Console.WriteLine($"Second lookup (cache hit): Success={result2.IsSuccess}, Tenant={result2.Value.Name}");
    }
}
