// Copyright © Erickson Lopez. MIT License.
using System;
using BenchmarkDotNet.Attributes;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;
using EricksonLopez.MultiTenancy.Testing;

namespace EricksonLopez.MultiTenancy.Benchmarks;

[MemoryDiagnoser]
public class DapperTenantParameterBenchmarks
{
    private ITenantContext _tenantContext = null!;
    private Guid _guid;

    [GlobalSetup]
    public void Setup()
    {
        _guid = Guid.NewGuid();
        _tenantContext = new TenantContextBuilder()
            .WithId(_guid)
            .WithName("Benchmark Tenant")
            .BuildContext();
    }

    [Benchmark(Baseline = true)]
    public DynamicParameters Create_DynamicParameters_Manual()
    {
        var parameters = new DynamicParameters();
        parameters.Add("TenantId", _guid, System.Data.DbType.Guid);
        return parameters;
    }

    [Benchmark]
    public DynamicParameters Create_TenantParameters_Extension()
    {
        return _tenantContext.CreateTenantParameters();
    }

    [Benchmark]
    public DynamicParameters WithTenant_Existing_Parameters()
    {
        var parameters = new DynamicParameters();
        parameters.Add("Status", "Active");
        return parameters.WithTenant(_tenantContext);
    }
}
