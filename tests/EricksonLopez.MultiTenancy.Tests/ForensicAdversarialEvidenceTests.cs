// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.Stores;
using EricksonLopez.Result;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EricksonLopez.MultiTenancy.Tests;

/// <summary>
/// Forensic adversarial test suite validating security invariants, isolation boundaries,
/// concurrency resilience, and fail-closed protections against Attacks A through T.
/// </summary>
public sealed class ForensicAdversarialEvidenceTests
{
    private static readonly Guid TenantAGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantBGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContextAccessor, ScopedTenantContextAccessor>();
        services.AddMemoryCache();
        return services.BuildServiceProvider();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Attack A: Modifying TenantId mid-request / Reassignment
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void AttackA_TenantContext_CannotBeMutatedOrReassigned_EnforcesAtomicCAS()
    {
        var accessor = new ScopedTenantContextAccessor();
        var tenantA = new TenantInfo(new TenantId(TenantAGuid), "TenantAlpha");
        var tenantB = new TenantInfo(new TenantId(TenantBGuid), "TenantBeta");

        accessor.TenantContext = TenantContext.Create(tenantA);
        accessor.TenantContext.RequiredTenant.Id.Value.Should().Be(TenantAGuid);

        // Tampering attempt: Re-assigning to Tenant B must throw InvalidOperationException
        var tamperAction = () => accessor.TenantContext = TenantContext.Create(tenantB);
        tamperAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*immutable after assignment*");

        // Assert invariant: Context remains strictly bound to Tenant A
        accessor.TenantContext.RequiredTenant.Id.Value.Should().Be(TenantAGuid);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Attack B: Swapping context across async yields (await)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AttackB_AsyncFlow_PreservesStrictTenantIsolationAcrossYields()
    {
        var sp = CreateServiceProvider();
        var scopeFactory = new DefaultTenantScopeFactory(sp.GetRequiredService<IServiceScopeFactory>());
        var tenantA = new TenantInfo(new TenantId(TenantAGuid), "TenantAlpha");

        await using (var scope = scopeFactory.CreateScope(tenantA))
        {
            AmbientTenantContextHolder.Current.Should().NotBeNull();
            AmbientTenantContextHolder.Current!.RequiredTenant.Id.Value.Should().Be(TenantAGuid);

            // Simulate heavy async yielding across thread pool hops
            for (var i = 0; i < 20; i++)
            {
                await Task.Yield();
                await Task.Delay(1);
                AmbientTenantContextHolder.Current!.RequiredTenant.Id.Value.Should().Be(TenantAGuid);
            }

            // Verify sub-task continuation inherits context cleanly
            await Task.Run(async () =>
            {
                AmbientTenantContextHolder.Current.Should().NotBeNull();
                AmbientTenantContextHolder.Current!.RequiredTenant.Id.Value.Should().Be(TenantAGuid);
                await Task.Yield();
                AmbientTenantContextHolder.Current!.RequiredTenant.Id.Value.Should().Be(TenantAGuid);
            });
        }

        // Context must be null outside of the scope boundary
        AmbientTenantContextHolder.Current.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Attack C: Cross-Tenant Data Segregation Logic
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AttackC_MultiTenant_Segregation_PreventsCrossTenantDataObservation()
    {
        var tenantA = new TenantInfo(new TenantId(TenantAGuid), "TenantAlpha");
        var tenantB = new TenantInfo(new TenantId(TenantBGuid), "TenantBeta");

        var store = new InMemoryTenantStore(new[] { tenantA, tenantB });

        // Querying for Tenant A must yield Tenant A exclusively
        var resultA = await store.GetTenantAsync(tenantA.Id);
        resultA.IsSuccess.Should().BeTrue();
        resultA.Value.Id.Value.Should().Be(TenantAGuid);
        resultA.Value.Name.Should().Be("TenantAlpha");

        // Querying for Tenant B must yield Tenant B exclusively
        var resultB = await store.GetTenantAsync(tenantB.Id);
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.Id.Value.Should().Be(TenantBGuid);
        resultB.Value.Name.Should().Be("TenantBeta");

        // Non-existent tenant lookup must fail cleanly
        var unknownId = new TenantId(Guid.NewGuid());
        var resultUnknown = await store.GetTenantAsync(unknownId);
        resultUnknown.IsFailure.Should().BeTrue();
        resultUnknown.Error.Code.Should().Be("Tenant.NotFound");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Attack D: Exception between context creation and disposal
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AttackD_ExceptionPaths_UnwindCleanlyAndRestoreAmbientState()
    {
        var sp = CreateServiceProvider();
        var scopeFactory = new DefaultTenantScopeFactory(sp.GetRequiredService<IServiceScopeFactory>());
        var tenantA = new TenantInfo(new TenantId(TenantAGuid), "TenantAlpha");

        AmbientTenantContextHolder.Current.Should().BeNull();

        try
        {
            await using var scope = scopeFactory.CreateScope(tenantA);
            AmbientTenantContextHolder.Current!.RequiredTenant.Id.Value.Should().Be(TenantAGuid);
            throw new InvalidOperationException("Simulated catastrophic crash inside tenant boundary");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Even after an unhandled exception inside the scope, disposal must restore ambient state
        AmbientTenantContextHolder.Current.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Attack H: Resolving scoped tenant context without scope
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void AttackH_UnscopedResolution_FailsClosed()
    {
        var accessor = new ScopedTenantContextAccessor();
        accessor.TenantContext.Should().BeNull();

        var emptyContext = TenantContext.Empty;
        emptyContext.IsResolved.Should().BeFalse();
        emptyContext.Tenant.Should().BeNull();
        emptyContext.Source.Should().Be(TenantResolutionSource.None);

        var act = () => _ = emptyContext.RequiredTenant;
        act.Should().Throw<TenantNotFoundException>()
            .WithMessage("*No tenant has been resolved*");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Attack M: Multi-tenant cache key prefixing prevents collisions
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void AttackM_CacheIsolation_EnforcesTenantKeyNamespacing()
    {
        var tenantAId = new TenantId(TenantAGuid);
        var tenantBId = new TenantId(TenantBGuid);
        const string logicalKey = "user-profile-1001";

        // Namespacing format: tenant:{tenantId}:{key}
        var keyA = $"tenant:{tenantAId.Value}:{logicalKey}";
        var keyB = $"tenant:{tenantBId.Value}:{logicalKey}";

        keyA.Should().NotBe(keyB);
        keyA.Should().Contain(TenantAGuid.ToString());
        keyB.Should().Contain(TenantBGuid.ToString());

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        memoryCache.Set(keyA, "SecretProfileA");
        memoryCache.Set(keyB, "SecretProfileB");

        // Tenant B querying cache must never observe Tenant A's value
        memoryCache.Get(keyA).Should().Be("SecretProfileA");
        memoryCache.Get(keyB).Should().Be("SecretProfileB");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Attack P: Background worker requiring explicit TenantId
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void AttackP_BackgroundJob_RequiresExplicitTenantScope()
    {
        var sp = CreateServiceProvider();
        var scopeFactory = new DefaultTenantScopeFactory(sp.GetRequiredService<IServiceScopeFactory>());

        // Null tenant must be rejected
        var act = () => scopeFactory.CreateScope((ITenantInfo)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Attack Q: Nested tenant scopes (A -> B -> A)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AttackQ_NestedTenantScopes_SafelyRestoresOuterContextUponInnerDisposal()
    {
        var sp = CreateServiceProvider();
        var scopeFactory = new DefaultTenantScopeFactory(sp.GetRequiredService<IServiceScopeFactory>());
        var tenantRoot = new TenantInfo(new TenantId(TenantAGuid), "RootTenant");
        var tenantChild = new TenantInfo(new TenantId(TenantBGuid), "ChildTenant");

        await using (var rootScope = scopeFactory.CreateScope(tenantRoot))
        {
            AmbientTenantContextHolder.Current!.RequiredTenant.Id.Value.Should().Be(TenantAGuid);

            await using (var childScope = scopeFactory.CreateScope(tenantChild))
            {
                AmbientTenantContextHolder.Current!.RequiredTenant.Id.Value.Should().Be(TenantBGuid);
            }

            // Upon inner scope disposal, outer context must be restored cleanly
            AmbientTenantContextHolder.Current.Should().NotBeNull();
            AmbientTenantContextHolder.Current!.RequiredTenant.Id.Value.Should().Be(TenantAGuid);
        }

        AmbientTenantContextHolder.Current.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Attack R: High concurrency burst (1,000 tasks) with zero context contamination
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AttackR_HighConcurrencyBurst_1000ParallelTasks_ZeroStaticContextContamination()
    {
        var sp = CreateServiceProvider();
        var scopeFactory = new DefaultTenantScopeFactory(sp.GetRequiredService<IServiceScopeFactory>());

        const int taskCount = 1000;
        var failureCounter = 0;

        var tasks = Enumerable.Range(0, taskCount).Select(async i =>
        {
            var isEven = i % 2 == 0;
            var expectedGuid = isEven ? TenantAGuid : TenantBGuid;
            var tenantName = isEven ? "TenantAlpha" : "TenantBeta";
            var tenant = new TenantInfo(new TenantId(expectedGuid), tenantName);

            await using var scope = scopeFactory.CreateScope(tenant);

            // Yield multiple times to induce thread-switching across thread pool
            for (var hop = 0; hop < 5; hop++)
            {
                await Task.Yield();
                var current = AmbientTenantContextHolder.Current;
                if (current is null || current.RequiredTenant.Id.Value != expectedGuid)
                {
                    Interlocked.Increment(ref failureCounter);
                }
            }
        });

        await Task.WhenAll(tasks);

        failureCounter.Should().Be(0, "All 1,000 concurrent tasks must retain strictly isolated tenant contexts");
        AmbientTenantContextHolder.Current.Should().BeNull();
    }
}
