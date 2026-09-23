// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.AspNetCore.Options;
using EricksonLopez.MultiTenancy.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests.Options;

public class TenantOptionsCacheTests
{
    private class TestOptions
    {
        public string? Value { get; set; }
    }

    [Fact]
    public void Constructor_NullAccessor_ThrowsArgumentNullException()
    {
        var act = () => new TenantOptionsCache<TestOptions, TenantInfo>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpContextAccessor");
    }

    [Fact]
    public void AddPerTenantOptions_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddPerTenantOptions<TestOptions, TenantInfo>();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddPerTenantOptions_RegistersTenantOptionsCache()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddPerTenantOptions<TestOptions, TenantInfo>();

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IOptionsMonitorCache<TestOptions>));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<TenantOptionsCache<TestOptions, TenantInfo>>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IOptionsMonitorCache<TestOptions>>();
        cache.Should().BeOfType<TenantOptionsCache<TestOptions, TenantInfo>>();
    }

    [Fact]
    public void GetOrAdd_NullFactory_ThrowsArgumentNullException()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor);

        var act = () => cache.GetOrAdd("name", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("createOptions");
    }

    [Fact]
    public void TryAdd_NullOptions_ThrowsArgumentNullException()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor);

        var act = () => cache.TryAdd("name", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Clear_ClearsCachedEntries()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor);

        cache.TryAdd("schema", new TestOptions { Value = "A" });
        cache.Clear();

        var retrieved = cache.GetOrAdd("schema", () => new TestOptions { Value = "B" });
        retrieved.Value.Should().Be("B");
    }

    [Fact]
    public void GetOrAdd_WithResolvedTenant_ReturnsTenantSpecificOptions()
    {
        // Arrange
        var tenantId = TenantId.NewId();
        var tenantInfo = new TenantInfo { Id = tenantId };

        var tenantContext = Substitute.For<ITenantContext<TenantInfo>>();
        tenantContext.IsResolved.Returns(true);
        tenantContext.RequiredTenant.Returns(tenantInfo);

        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(tenantAccessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor);

        // Act
        var opts1 = cache.GetOrAdd(null, () => new TestOptions { Value = "TenantA" });

        // Assert
        opts1.Value.Should().Be("TenantA");

        // Ensure it cached properly for the same tenant
        var opts2 = cache.GetOrAdd(null, () => new TestOptions { Value = "Different" });
        opts2.Value.Should().Be("TenantA"); // should return cached instance
    }

    [Fact]
    public void GetOrAdd_NullHttpContext_UsesGlobalCacheKey()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor);

        var opts1 = cache.GetOrAdd("name", () => new TestOptions { Value = "Global" });
        opts1.Value.Should().Be("Global");

        var opts2 = cache.GetOrAdd("name", () => new TestOptions { Value = "Different" });
        opts2.Value.Should().Be("Global");
    }

    [Fact]
    public void GetOrAdd_TenantContextNotResolved_UsesGlobalCacheKey()
    {
        var tenantContext = Substitute.For<ITenantContext<TenantInfo>>();
        tenantContext.IsResolved.Returns(false);
        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(tenantAccessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor);

        var opts1 = cache.GetOrAdd("name", () => new TestOptions { Value = "Unresolved" });
        opts1.Value.Should().Be("Unresolved");

        var opts2 = cache.GetOrAdd("name", () => new TestOptions { Value = "Different" });
        opts2.Value.Should().Be("Unresolved");
    }

    [Fact]
    public void GetOrAdd_NoTenantContext_UsesGlobalCacheKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider(); // No ITenantContextAccessor registered

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor);

        // Act
        var opts1 = cache.GetOrAdd(Microsoft.Extensions.Options.Options.DefaultName, () => new TestOptions { Value = "Global" });

        // Assert
        opts1.Value.Should().Be("Global");
    }

    [Fact]
    public void TryAdd_And_TryRemove_WorksWithTenantContext()
    {
        // Arrange
        var tenantId = TenantId.NewId();
        var tenantInfo = new TenantInfo { Id = tenantId };
        var tenantContext = Substitute.For<ITenantContext<TenantInfo>>();
        tenantContext.IsResolved.Returns(true);
        tenantContext.RequiredTenant.Returns(tenantInfo);
        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(tenantAccessor);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor);
        var opts = new TestOptions { Value = "Test" };

        // Act
        var added = cache.TryAdd("MySchema", opts);
        var removed = cache.TryRemove("MySchema");
        var removeAgain = cache.TryRemove("MySchema");

        // Assert
        added.Should().BeTrue();
        removed.Should().BeTrue();
        removeAgain.Should().BeFalse();
    }

    [Fact]
    public void GetOrAdd_DifferentTenantsSameName_ReturnsIsolatedOptions()
    {
        var tenant1Id = TenantId.NewId();
        var tenant1Info = new TenantInfo { Id = tenant1Id };
        var tenant1Context = Substitute.For<ITenantContext<TenantInfo>>();
        tenant1Context.IsResolved.Returns(true);
        tenant1Context.RequiredTenant.Returns(tenant1Info);

        var tenant2Id = TenantId.NewId();
        var tenant2Info = new TenantInfo { Id = tenant2Id };
        var tenant2Context = Substitute.For<ITenantContext<TenantInfo>>();
        tenant2Context.IsResolved.Returns(true);
        tenant2Context.RequiredTenant.Returns(tenant2Info);

        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.TenantContext.Returns(tenant1Context);

        var services = new ServiceCollection();
        services.AddSingleton(tenantAccessor);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor);

        var opt1 = cache.GetOrAdd("MyNamedOptions", () => new TestOptions { Value = "Tenant1Value" });
        opt1.Value.Should().Be("Tenant1Value");

        // Switch to tenant 2
        tenantAccessor.TenantContext.Returns(tenant2Context);
        var opt2 = cache.GetOrAdd("MyNamedOptions", () => new TestOptions { Value = "Tenant2Value" });
        opt2.Value.Should().Be("Tenant2Value");

        // Switch back to tenant 1 -> should return cached Tenant1Value
        tenantAccessor.TenantContext.Returns(tenant1Context);
        var opt1Cached = cache.GetOrAdd("MyNamedOptions", () => new TestOptions { Value = "Different" });
        opt1Cached.Value.Should().Be("Tenant1Value");
    }

    [Fact]
    public void GetOrAdd_SameTenantDifferentNames_ReturnsIsolatedOptions()
    {
        var tenantId = TenantId.NewId();
        var tenantInfo = new TenantInfo { Id = tenantId };
        var tenantContext = Substitute.For<ITenantContext<TenantInfo>>();
        tenantContext.IsResolved.Returns(true);
        tenantContext.RequiredTenant.Returns(tenantInfo);

        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(tenantAccessor);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor);

        var optSchemaA = cache.GetOrAdd("SchemaA", () => new TestOptions { Value = "ValA" });
        var optSchemaB = cache.GetOrAdd("SchemaB", () => new TestOptions { Value = "ValB" });
        var optDefault = cache.GetOrAdd(null, () => new TestOptions { Value = "ValDefault" });

        optSchemaA.Value.Should().Be("ValA");
        optSchemaB.Value.Should().Be("ValB");
        optDefault.Value.Should().Be("ValDefault");
    }

    [Fact]
    public void GetOrAdd_ExceedsCapacity_EvictsLeastRecentlyUsedNotOldestInserted()
    {
        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        var services = new ServiceCollection();
        services.AddSingleton(tenantAccessor);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor, maxCapacity: 2);

        var tenant1 = new TenantInfo { Id = TenantId.NewId() };
        var ctx1 = Substitute.For<ITenantContext<TenantInfo>>();
        ctx1.IsResolved.Returns(true);
        ctx1.RequiredTenant.Returns(tenant1);

        var tenant2 = new TenantInfo { Id = TenantId.NewId() };
        var ctx2 = Substitute.For<ITenantContext<TenantInfo>>();
        ctx2.IsResolved.Returns(true);
        ctx2.RequiredTenant.Returns(tenant2);

        var tenant3 = new TenantInfo { Id = TenantId.NewId() };
        var ctx3 = Substitute.For<ITenantContext<TenantInfo>>();
        ctx3.IsResolved.Returns(true);
        ctx3.RequiredTenant.Returns(tenant3);

        // 1. Add Tenant 1
        tenantAccessor.TenantContext.Returns(ctx1);
        cache.GetOrAdd("key", () => new TestOptions { Value = "Val1" });

        // 2. Add Tenant 2
        tenantAccessor.TenantContext.Returns(ctx2);
        cache.GetOrAdd("key", () => new TestOptions { Value = "Val2" });

        // 3. Access Tenant 1 again -> Promotes Tenant 1 to MRU!
        tenantAccessor.TenantContext.Returns(ctx1);
        cache.GetOrAdd("key", () => new TestOptions { Value = "Val1-New" });

        // 4. Add Tenant 3 -> Capacity exceeded, should evict Tenant 2!
        tenantAccessor.TenantContext.Returns(ctx3);
        cache.GetOrAdd("key", () => new TestOptions { Value = "Val3" });

        // 5. Tenant 1 should still be cached
        tenantAccessor.TenantContext.Returns(ctx1);
        var t1 = cache.GetOrAdd("key", () => new TestOptions { Value = "Val1-Recreated" });
        t1.Value.Should().Be("Val1");

        // 6. Tenant 2 should have been evicted and therefore re-created
        tenantAccessor.TenantContext.Returns(ctx2);
        var t2 = cache.GetOrAdd("key", () => new TestOptions { Value = "Val2-Recreated" });
        t2.Value.Should().Be("Val2-Recreated");
    }

    [Fact]
    public void GetOrAdd_ThrowOnMissingTenantTrue_WhenUnresolved_ThrowsTenantNotFoundException()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor, maxCapacity: 10000, throwOnMissingTenant: true);

        var act = () => cache.GetOrAdd("custom", () => new TestOptions { Value = "Test" });

        act.Should().Throw<TenantNotFoundException>()
            .WithMessage("*strict tenant resolution is enabled*");
    }

    [Fact]
    public void GetOrAdd_WithTenantOptionsCacheOptions_StrictResolution_ThrowsTenantNotFoundException()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var options = Microsoft.Extensions.Options.Options.Create(new TenantOptionsCacheOptions
        {
            ThrowOnMissingTenant = true,
            MaxCapacity = 50
        });

        var cache = new TenantOptionsCache<TestOptions, TenantInfo>(httpContextAccessor, options);

        var act = () => cache.GetOrAdd("custom", () => new TestOptions { Value = "Test" });

        act.Should().Throw<TenantNotFoundException>();
    }

    [Fact]
    public void AddPerTenantOptions_WithCacheOptionsConfig_ConfiguresOptionsInDI()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddPerTenantOptions<TestOptions, TenantInfo>(cacheOpts =>
        {
            cacheOpts.ThrowOnMissingTenant = true;
            cacheOpts.MaxCapacity = 42;
        });

        var sp = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<IOptionsMonitorCache<TestOptions>>();

        cache.Should().BeOfType<TenantOptionsCache<TestOptions, TenantInfo>>();
        var cacheOpts = sp.GetRequiredService<IOptions<TenantOptionsCacheOptions>>().Value;
        cacheOpts.ThrowOnMissingTenant.Should().BeTrue();
        cacheOpts.MaxCapacity.Should().Be(42);
    }

    [Fact]
    public void AddPerTenantOptions_WithConfigureOptions_ConfiguresPerTenant()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        var tenant = new TenantInfo(TenantId.NewId(), "TenantAcme");
        var context = new TestTenantContext(tenant);
        services.AddSingleton<ITenantContextAccessor>(context);

        services.AddPerTenantOptions<TestOptions, TenantInfo>((options, t) =>
        {
            options.Value = t.Name;
        });

        var sp = services.BuildServiceProvider();
        var configureOptions = sp.GetRequiredService<IConfigureOptions<TestOptions>>();
        var targetOptions = new TestOptions();
        configureOptions.Configure(targetOptions);

        targetOptions.Value.Should().Be("TenantAcme");
    }

    [Fact]
    public void AddPerTenantOptions_WithCacheOptionsAndConfigureOptions_ConfiguresBoth()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        var tenant = new TenantInfo(TenantId.NewId(), "TenantAcme");
        var context = new TestTenantContext(tenant);
        services.AddSingleton<ITenantContextAccessor>(context);

        services.AddPerTenantOptions<TestOptions, TenantInfo>(
            cacheOpts => { cacheOpts.MaxCapacity = 100; },
            (options, t) => { options.Value = t.Name; });

        var sp = services.BuildServiceProvider();
        var cacheOpts = sp.GetRequiredService<IOptions<TenantOptionsCacheOptions>>().Value;
        cacheOpts.MaxCapacity.Should().Be(100);

        var configureOptions = sp.GetRequiredService<IConfigureOptions<TestOptions>>();
        var targetOptions = new TestOptions();
        configureOptions.Configure(targetOptions);
        targetOptions.Value.Should().Be("TenantAcme");
    }
}

