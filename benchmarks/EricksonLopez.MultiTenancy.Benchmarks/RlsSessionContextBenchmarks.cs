// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;
using EricksonLopez.MultiTenancy.PostgreSql;
using EricksonLopez.MultiTenancy.SqlServer;
using EricksonLopez.MultiTenancy.Testing;

namespace EricksonLopez.MultiTenancy.Benchmarks;

/// <summary>
/// Benchmarks evaluating database session context setup and parameterization overhead
/// across PostgreSQL Row Level Security (SET LOCAL), SQL Server Session Context (sp_set_session_context),
/// and Dapper tenant parameter injection.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public sealed class RlsSessionContextBenchmarks : IDisposable
{
    private ITenantContext _tenantContext = null!;
    private FakeDbConnection _connection = null!;
    private FakeDbTransaction _transaction = null!;

    [GlobalSetup]
    public void Setup()
    {
        var tenantGuid = Guid.NewGuid();
        _tenantContext = new TenantContextBuilder()
            .WithId(tenantGuid)
            .WithName("Enterprise Benchmark Tenant")
            .WithProperty("Tier", "Enterprise")
            .BuildContext();

        _connection = new FakeDbConnection();
        _connection.Open();
        _transaction = (FakeDbTransaction)_connection.BeginTransaction();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Dispose();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public DynamicParameters Dapper_CreateTenantParameters()
    {
        return _tenantContext.CreateTenantParameters();
    }

    [Benchmark]
    public DynamicParameters Dapper_WithTenant_Extension()
    {
        var parameters = new DynamicParameters();
        parameters.Add("Status", "Active", DbType.String);
        parameters.Add("Amount", 100.50m, DbType.Decimal);
        return parameters.WithTenant(_tenantContext);
    }

    [Benchmark]
    public async Task PostgreSql_SetLocal_RlsContext()
    {
        await _connection.SetTenantRlsContextAsync(_transaction, _tenantContext);
    }

    [Benchmark]
    public async Task SqlServer_SetSessionContext()
    {
        await _connection.SetTenantSessionContextAsync(_transaction, _tenantContext, readOnly: true);
    }
}
