// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.Stores;
using EricksonLopez.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests.Stores;

public class CachedTenantStoreExtensionsTests
{
    public class DummyTenantInfo : ITenantInfo
    {
        public TenantId Id { get; set; } = TenantId.NewId();
        public string Identifier { get; set; } = "dummy";
        public string Name { get; set; } = "Dummy";
        public bool IsActive { get; set; } = true;
        public string? ConnectionString { get; set; }
        public System.Collections.Generic.IReadOnlyDictionary<string, string> Properties { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
    }

    [Fact]
    public void WithCache_RegistersCachedTenantStoreDecorator()
    {
        var services = new ServiceCollection();

        // Register dummy inner store
        var innerStore = Substitute.For<ITenantStore<DummyTenantInfo>>();
        services.AddSingleton(innerStore);
        services.AddMemoryCache();

        services.AddCachedTenantStore<DummyTenantInfo>(options =>
        {
            options.AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(10);
        });

        var sp = services.BuildServiceProvider();
        var resolvedStore = sp.GetRequiredService<ITenantStore<DummyTenantInfo>>();

        resolvedStore.Should().BeOfType<CachedTenantStore<DummyTenantInfo>>();

        var options = sp.GetRequiredService<IOptions<CachedTenantStoreOptions>>().Value;
        options.AbsoluteExpirationRelativeToNow.Should().Be(System.TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void AddCachedTenantStore_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddCachedTenantStore<DummyTenantInfo>();
        act.Should().Throw<System.ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddCachedTenantStore_NoInnerStoreRegistered_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();

        var act = () => services.AddCachedTenantStore<DummyTenantInfo>();
        act.Should().Throw<System.InvalidOperationException>()
           .WithMessage("*No ITenantStore`1 is registered to decorate with a cache.*");
    }

    [Fact]
    public void AddCachedTenantStore_WithoutOptions_RegistersSuccessfully()
    {
        var services = new ServiceCollection();
        var innerStore = Substitute.For<ITenantStore<DummyTenantInfo>>();
        services.AddSingleton(innerStore);
        services.AddMemoryCache();

        services.AddCachedTenantStore<DummyTenantInfo>();

        var sp = services.BuildServiceProvider();
        var resolvedStore = sp.GetRequiredService<ITenantStore<DummyTenantInfo>>();
        resolvedStore.Should().BeOfType<CachedTenantStore<DummyTenantInfo>>();
    }

    public class FakeInnerStore : ITenantStore<DummyTenantInfo>
    {
        public Task<Result<DummyTenantInfo>> GetTenantAsync(TenantId tenantId, System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(Result<DummyTenantInfo>.Success(new DummyTenantInfo { Id = tenantId }));

        Task<Result<ITenantInfo>> ITenantStore.GetTenantAsync(TenantId tenantId, System.Threading.CancellationToken cancellationToken)
            => Task.FromResult(Result<ITenantInfo>.Success(new DummyTenantInfo { Id = tenantId }));
    }

    [Fact]
    public void AddCachedTenantStore_WithImplementationType_ResolvesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantStore<DummyTenantInfo>, FakeInnerStore>();
        services.AddMemoryCache();

        services.AddCachedTenantStore<DummyTenantInfo>();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var resolvedStore = scope.ServiceProvider.GetRequiredService<ITenantStore<DummyTenantInfo>>();
        resolvedStore.Should().BeOfType<CachedTenantStore<DummyTenantInfo>>();
    }

    [Fact]
    public void AddCachedTenantStore_WithImplementationFactory_ResolvesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantStore<DummyTenantInfo>>(provider => new FakeInnerStore());
        services.AddMemoryCache();

        services.AddCachedTenantStore<DummyTenantInfo>();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var resolvedStore = scope.ServiceProvider.GetRequiredService<ITenantStore<DummyTenantInfo>>();
        resolvedStore.Should().BeOfType<CachedTenantStore<DummyTenantInfo>>();
    }
}

