// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using EricksonLopez.MultiTenancy;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy.Benchmarks;

/// <summary>
/// Benchmarks evaluating concurrent tenant store throughput and contention characteristics
/// under multi-threaded parallel workloads simulating high-throughput cloud environments.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ConcurrentTenantStoreBenchmarks
{
    private InMemoryTenantStore _store = null!;
    private TenantId[] _tenantIds = null!;
    private string[] _tenantIdentifiers = null!;
    private const int _tenantCount = 1000;
    private const int _concurrencyIterations = 10000;

    [GlobalSetup]
    public void Setup()
    {
        var tenants = new List<TenantInfo>(_tenantCount);
        _tenantIds = new TenantId[_tenantCount];
        _tenantIdentifiers = new string[_tenantCount];

        for (int i = 0; i < _tenantCount; i++)
        {
            var guid = Guid.NewGuid();
            var id = TenantId.Create(guid);
            var identifier = $"tenant-domain-{i:D4}";

            _tenantIds[i] = id;
            _tenantIdentifiers[i] = identifier;

            tenants.Add(new TenantInfo
            {
                Id = id,
                Name = identifier,
                Properties = new Dictionary<string, string>
                {
                    ["Region"] = "us-east-1",
                    ["Tier"] = "Enterprise",
                    ["IsolationMode"] = "SharedSchemaRLS"
                }
            });
        }

        _store = new InMemoryTenantStore(tenants);
    }

    [Benchmark(Baseline = true)]
    public void Parallel_GetTenantById_10K()
    {
        Parallel.For(0, _concurrencyIterations, i =>
        {
            var targetId = _tenantIds[i % _tenantCount];
            var task = _store.GetTenantAsync(targetId);
            var result = task.GetAwaiter().GetResult();
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException("Tenant lookup failed unexpectedly.");
            }
        });
    }

    [Benchmark]
    public void Parallel_GetTenantByIdentifier_10K()
    {
        Parallel.For(0, _concurrencyIterations, i =>
        {
            var targetIdentifier = _tenantIdentifiers[i % _tenantCount];
            var task = _store.GetTenantByIdentifierAsync(targetIdentifier);
            var result = task.GetAwaiter().GetResult();
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException("Tenant identifier lookup failed unexpectedly.");
            }
        });
    }

    [Benchmark]
    public async Task<int> Concurrent_WhenAll_BatchedLookup()
    {
        var tasks = new Task<Result<TenantInfo>>[100];
        for (int i = 0; i < 100; i++)
        {
            tasks[i] = _store.GetTenantAsync(_tenantIds[i % _tenantCount]);
        }

        var results = await Task.WhenAll(tasks);
        return results.Length;
    }
}
