// Copyright © Erickson Lopez. MIT License.
using System;
using BenchmarkDotNet.Attributes;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Testing;

namespace EricksonLopez.MultiTenancy.Benchmarks;

[MemoryDiagnoser]
public class TenantResolutionBenchmarks
{
    private Guid _guid;
    private string _guidString = null!;
    private TenantId _tenantId;
    private ITenantContext _resolvedContext = null!;
    private ScopedTenantContextAccessor _accessor = null!;

    [GlobalSetup]
    public void Setup()
    {
        _guid = Guid.NewGuid();
        _guidString = _guid.ToString();
        _tenantId = TenantId.Create(_guid);

        _resolvedContext = new TenantContextBuilder()
            .WithId(_guid)
            .WithName("Benchmark Tenant")
            .WithProperty("Tier", "Enterprise")
            .BuildContext();

        _accessor = new ScopedTenantContextAccessor();
        _accessor.TenantContext = _resolvedContext;
    }

    [Benchmark(Baseline = true)]
    public TenantId TenantId_Create_FromGuid()
    {
        return TenantId.Create(_guid);
    }

    [Benchmark]
    public bool TenantId_TryCreate_FromString()
    {
        return TenantId.TryCreate(_guidString, out _);
    }

    [Benchmark]
    public bool TenantId_Equality()
    {
        return _tenantId == TenantId.Create(_guid);
    }

    [Benchmark]
    public ITenantContext? ContextAccessor_GetContext()
    {
        return _accessor.TenantContext;
    }

    [Benchmark]
    public bool ContextAccessor_IsResolved()
    {
        return _accessor.TenantContext?.IsResolved ?? false;
    }
}
