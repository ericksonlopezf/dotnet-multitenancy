// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using EricksonLopez.MultiTenancy;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy.Benchmarks;

[MemoryDiagnoser]
public class TenantStoreBenchmarks
{
    private InMemoryTenantStore _store = null!;
    private TenantId _existingTenantId;
    private TenantId _nonExistingTenantId;
    private string _existingIdentifier = null!;

    [GlobalSetup]
    public void Setup()
    {
        var tenants = new List<TenantInfo>();
        for (int i = 0; i < 100; i++)
        {
            var guid = Guid.NewGuid();
            var info = new TenantInfo
            {
                Id = TenantId.Create(guid),
                Name = $"tenant-{i}",
                Properties = new Dictionary<string, string>
                {
                    ["Tier"] = "Standard",
                    ["Region"] = "us-east-1"
                }
            };
            tenants.Add(info);
        }

        _existingTenantId = tenants[50].Id;
        _existingIdentifier = tenants[50].Name;
        _nonExistingTenantId = TenantId.Create(Guid.NewGuid());
        _store = new InMemoryTenantStore(tenants);
    }

    [Benchmark(Baseline = true)]
    public async Task<Result<TenantInfo>> GetTenantAsync_Hit()
    {
        return await _store.GetTenantAsync(_existingTenantId);
    }

    [Benchmark]
    public async Task<Result<TenantInfo>> GetTenantAsync_Miss()
    {
        return await _store.GetTenantAsync(_nonExistingTenantId);
    }

    [Benchmark]
    public async Task<Result<TenantInfo>> GetTenantByIdentifierAsync_Hit()
    {
        return await _store.GetTenantByIdentifierAsync(_existingIdentifier);
    }
}
