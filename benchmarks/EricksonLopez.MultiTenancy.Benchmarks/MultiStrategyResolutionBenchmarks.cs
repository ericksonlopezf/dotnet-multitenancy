// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore.Strategies;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Http;

namespace EricksonLopez.MultiTenancy.Benchmarks;

/// <summary>
/// Benchmarks measuring the performance and allocation profile of multi-strategy tenant resolution chains,
/// comparing single-strategy resolution against multi-source fallback chains and fail-closed conflict detection.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class MultiStrategyResolutionBenchmarks
{
    private DefaultHttpContext _httpContext = null!;
    private IHttpContextAccessor _accessor = null!;
    private HeaderTenantResolutionStrategy _headerStrategy = null!;
    private ClaimTenantResolutionStrategy _claimStrategy = null!;
    private BasePathTenantResolutionStrategy _basePathStrategy = null!;
    private DelegateTenantResolutionStrategy _delegateStrategy = null!;
    private List<ITenantResolutionStrategy> _multiStrategyList = null!;
    private Guid _tenantGuid;
    private string _tenantGuidString = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tenantGuid = Guid.NewGuid();
        _tenantGuidString = _tenantGuid.ToString();

        _httpContext = new DefaultHttpContext();
        _httpContext.Request.Headers["X-Tenant-ID"] = _tenantGuidString;
        _httpContext.Request.Path = new PathString($"/{_tenantGuidString}/api/v1/orders");

        var claims = new[] { new Claim("tenant_id", _tenantGuidString) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        _accessor = new HttpContextAccessor { HttpContext = _httpContext };

        _headerStrategy = new HeaderTenantResolutionStrategy(_accessor, "X-Tenant-ID");
        _claimStrategy = new ClaimTenantResolutionStrategy(_accessor, "tenant_id");
        _basePathStrategy = new BasePathTenantResolutionStrategy(_accessor);
        _delegateStrategy = new DelegateTenantResolutionStrategy(_ => ValueTask.FromResult(Result<TenantId>.Success(TenantId.Create(_tenantGuid))));

        _multiStrategyList = new List<ITenantResolutionStrategy>
        {
            _headerStrategy,
            _claimStrategy,
            _basePathStrategy,
            _delegateStrategy
        };
    }

    [Benchmark(Baseline = true)]
    public async ValueTask<Result<TenantId>> SingleStrategy_Header_Resolve()
    {
        return await _headerStrategy.ResolveTenantIdAsync();
    }

    [Benchmark]
    public async ValueTask<Result<TenantId>> SingleStrategy_Claim_Resolve()
    {
        return await _claimStrategy.ResolveTenantIdAsync();
    }

    [Benchmark]
    public async ValueTask<Result<TenantId>> SingleStrategy_BasePath_Resolve()
    {
        return await _basePathStrategy.ResolveTenantIdAsync();
    }

    [Benchmark]
    public async ValueTask<Result<TenantId>> MultiStrategy_Sequential_Fallback()
    {
        for (int i = 0; i < _multiStrategyList.Count; i++)
        {
            var result = await _multiStrategyList[i].ResolveTenantIdAsync();
            if (result.IsSuccess)
            {
                return result;
            }
        }

        return Result<TenantId>.Failure(TenantErrors.NotFound(TenantId.Empty));
    }

    [Benchmark]
    public async ValueTask<Result<TenantId>> MultiStrategy_FailClosed_ConflictDetection()
    {
        TenantId? firstResolved = null;
        for (int i = 0; i < _multiStrategyList.Count; i++)
        {
            var result = await _multiStrategyList[i].ResolveTenantIdAsync();
            if (result.IsSuccess)
            {
                if (firstResolved is null)
                {
                    firstResolved = result.Value;
                }
                else if (firstResolved.Value != result.Value)
                {
                    return Result<TenantId>.Failure(TenantErrors.StrategyFailed(
                        "MultiStrategy",
                        "Multiple strategies resolved conflicting tenant identities."));
                }
            }
        }

        return firstResolved is not null
            ? Result<TenantId>.Success(firstResolved.Value)
            : Result<TenantId>.Failure(TenantErrors.NotFound(TenantId.Empty));
    }
}
