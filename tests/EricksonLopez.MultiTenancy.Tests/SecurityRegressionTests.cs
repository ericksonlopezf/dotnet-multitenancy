// Copyright © Erickson Lopez. MIT License.
using System;
using System.Reflection;
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

public sealed class SecurityRegressionTests
{
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContextAccessor, ScopedTenantContextAccessor>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SEC001_DisposeAsync_DefaultTenantScope_MustResetAmbientContext()
    {
        var serviceProvider = CreateServiceProvider();
        var scopeFactory = new DefaultTenantScopeFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        var tenantInfo = new TenantInfo(TenantId.NewId(), "TenantAlpha");

        await using (var scope = scopeFactory.CreateScope(tenantInfo))
        {
            AmbientTenantContextHolder.Current.Should().NotBeNull();
            AmbientTenantContextHolder.Current!.Tenant!.Id.Should().Be(tenantInfo.Id);
        }

        AmbientTenantContextHolder.Current.Should().BeNull();
    }

    [Fact]
    public void CTX001_Dispose_NestedTenantScopes_MustRestoreParentContext()
    {
        var serviceProvider = CreateServiceProvider();
        var scopeFactory = new DefaultTenantScopeFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        var parentTenant = new TenantInfo(TenantId.NewId(), "ParentTenant");
        var childTenant = new TenantInfo(TenantId.NewId(), "ChildTenant");

        using (var parentScope = scopeFactory.CreateScope(parentTenant))
        {
            AmbientTenantContextHolder.Current!.Tenant!.Id.Should().Be(parentTenant.Id);

            using (var childScope = scopeFactory.CreateScope(childTenant))
            {
                AmbientTenantContextHolder.Current!.Tenant!.Id.Should().Be(childTenant.Id);
            }

            AmbientTenantContextHolder.Current.Should().NotBeNull();
            AmbientTenantContextHolder.Current!.Tenant!.Id.Should().Be(parentTenant.Id);
        }

        AmbientTenantContextHolder.Current.Should().BeNull();
    }

    [Fact]
    public async Task CTX001_DisposeAsync_NestedTenantScopes_MustRestoreParentContext()
    {
        var serviceProvider = CreateServiceProvider();
        var scopeFactory = new DefaultTenantScopeFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        var parentTenant = new TenantInfo(TenantId.NewId(), "ParentTenant");
        var childTenant = new TenantInfo(TenantId.NewId(), "ChildTenant");

        await using (var parentScope = scopeFactory.CreateScope(parentTenant))
        {
            AmbientTenantContextHolder.Current!.Tenant!.Id.Should().Be(parentTenant.Id);

            await using (var childScope = scopeFactory.CreateScope(childTenant))
            {
                AmbientTenantContextHolder.Current!.Tenant!.Id.Should().Be(childTenant.Id);
            }

            AmbientTenantContextHolder.Current.Should().NotBeNull();
            AmbientTenantContextHolder.Current!.Tenant!.Id.Should().Be(parentTenant.Id);
        }

        AmbientTenantContextHolder.Current.Should().BeNull();
    }

    [Fact]
    public void LEAK02_OutOfOrderScopeDisposal_DoesNotResurrectDisposedContext()
    {
        var serviceProvider = CreateServiceProvider();
        var scopeFactory = new DefaultTenantScopeFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var tenantRoot = new TenantInfo(TenantId.NewId(), "Root");
        var tenantChild = new TenantInfo(TenantId.NewId(), "Child");

        var rootScope = scopeFactory.CreateScope(tenantRoot);
        var childScope = scopeFactory.CreateScope(tenantChild);

        // Root scope is disposed BEFORE child scope (e.g. timeout or background cancellation)
        rootScope.Dispose();

        // Ambient context is still child
        AmbientTenantContextHolder.Current!.Tenant!.Id.Should().Be(tenantChild.Id);

        // Now child scope is disposed
        childScope.Dispose();

        // Must NOT resurrect disposed root scope context
        AmbientTenantContextHolder.Current.Should().BeNull("Disposed parent context must never be resurrected into ambient slot.");
    }

    [Fact]
    public async Task LEAK02_OutOfOrderScopeDisposalAsync_DoesNotResurrectDisposedContext()
    {
        var serviceProvider = CreateServiceProvider();
        var scopeFactory = new DefaultTenantScopeFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var tenantRoot = new TenantInfo(TenantId.NewId(), "Root");
        var tenantChild = new TenantInfo(TenantId.NewId(), "Child");

        var rootScope = scopeFactory.CreateScope(tenantRoot);
        var childScope = scopeFactory.CreateScope(tenantChild);

        // Root scope is disposed BEFORE child scope asynchronously
        await rootScope.DisposeAsync();

        // Ambient context is still child
        AmbientTenantContextHolder.Current!.Tenant!.Id.Should().Be(tenantChild.Id);

        // Now child scope is disposed asynchronously
        await childScope.DisposeAsync();

        // Must NOT resurrect disposed root scope context
        AmbientTenantContextHolder.Current.Should().BeNull("Disposed parent context must never be resurrected into ambient slot.");
    }

    [Fact]
    public void SEC003_AmbientTenantContextHolder_Current_MustNotExposePublicSetter()
    {
        var property = typeof(AmbientTenantContextHolder).GetProperty(
            nameof(AmbientTenantContextHolder.Current),
            BindingFlags.Public | BindingFlags.Static);

        property.Should().NotBeNull();
        var setMethod = property!.GetSetMethod(nonPublic: false);
        setMethod.Should().BeNull("AmbientTenantContextHolder.Current must not have a public setter to prevent tampering.");
    }

    private sealed class FakeInnerStore : ITenantStore<TenantInfo>
    {
        public Task<Result<TenantInfo>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<TenantInfo>.Failure(TenantErrors.NotFound(tenantId)));

        async Task<Result<ITenantInfo>> ITenantStore.GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
        {
            var result = await GetTenantAsync(tenantId, cancellationToken);
            return result.IsSuccess
                ? Result<ITenantInfo>.Success(result.Value)
                : Result<ITenantInfo>.Failure(result.Error);
        }
    }

    [Fact]
    public async Task PERF001_CachedTenantStore_UnderMassiveRandomTenantQueries_DoesNotLeakLocks()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var store = new CachedTenantStore<TenantInfo>(
            new FakeInnerStore(),
            memoryCache,
            Options.Create(new CachedTenantStoreOptions()));

        for (var i = 0; i < 500; i++)
        {
            var randomId = TenantId.NewId();
            var result = await store.GetTenantAsync(randomId);
            result.IsFailure.Should().BeTrue();
        }

        // Verify with reflection that the old unbounded dictionary does not exist
        var field = typeof(CachedTenantStore<TenantInfo>).GetField(
            "_keyLocks",
            BindingFlags.NonPublic | BindingFlags.Instance);

        field.Should().BeNull("CachedTenantStore should use striped locks instead of an unbounded ConcurrentDictionary.");
    }

    [Fact]
    public async Task SEC004_SetCurrentScoped_NestedScopes_DeterministicallyRestoresParentContext()
    {
        var tenantRoot = new TenantContext<TenantInfo>(new TenantInfo(TenantId.NewId(), "RootTenant"));
        var tenantChild = new TenantContext<TenantInfo>(new TenantInfo(TenantId.NewId(), "ChildTenant"));

        using (AmbientTenantContextHolder.SetCurrentScoped(tenantRoot))
        {
            AmbientTenantContextHolder.Current.Should().BeSameAs(tenantRoot);

            using (AmbientTenantContextHolder.SetCurrentScoped(tenantChild))
            {
                AmbientTenantContextHolder.Current.Should().BeSameAs(tenantChild);

                await Task.Yield();

                AmbientTenantContextHolder.Current.Should().BeSameAs(tenantChild);
            }

            AmbientTenantContextHolder.Current.Should().BeSameAs(tenantRoot);
        }

        AmbientTenantContextHolder.Current.Should().BeNull();
    }
}
