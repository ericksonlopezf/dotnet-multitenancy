// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class DefaultTenantScopeFactoryTests
{
    private class FakeScope : IServiceScope, IAsyncDisposable
    {
        public int DisposeCalls { get; private set; }
        public int DisposeAsyncCalls { get; private set; }

        public IServiceProvider ServiceProvider { get; }

        public FakeScope()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ITenantContextAccessor, ScopedTenantContextAccessor>();
            ServiceProvider = services.BuildServiceProvider();
        }

        public void Dispose() => DisposeCalls++;
        public ValueTask DisposeAsync()
        {
            DisposeAsyncCalls++;
            return ValueTask.CompletedTask;
        }
    }

    private class FakeScopeFactory : IServiceScopeFactory
    {
        public FakeScope ScopeToReturn { get; } = new FakeScope();
        public IServiceScope CreateScope() => ScopeToReturn;
    }

    private class FakeScopeSyncOnly : IServiceScope
    {
        public int DisposeCalls { get; private set; }

        public IServiceProvider ServiceProvider { get; }

        public FakeScopeSyncOnly()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ITenantContextAccessor, ScopedTenantContextAccessor>();
            ServiceProvider = services.BuildServiceProvider();
        }

        public void Dispose() => DisposeCalls++;
    }

    private class FakeScopeFactorySyncOnly : IServiceScopeFactory
    {
        public FakeScopeSyncOnly ScopeToReturn { get; } = new FakeScopeSyncOnly();
        public IServiceScope CreateScope() => ScopeToReturn;
    }

    [Fact]
    public void Constructor_NullScopeFactory_ThrowsArgumentNullException()
    {
        var act = () => new DefaultTenantScopeFactory(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("scopeFactory");
    }

    [Fact]
    public void CreateScope_NullTenant_ThrowsArgumentNullException()
    {
        var factory = new DefaultTenantScopeFactory(new FakeScopeFactory());
        var act = () => factory.CreateScope(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tenant");
    }

    [Fact]
    public void CreateScope_EmptyTenantId_ThrowsArgumentException()
    {
        var factory = new DefaultTenantScopeFactory(new FakeScopeFactory());
        var tenant = new TenantInfo(TenantId.Empty, "Empty");
        var act = () => factory.CreateScope(tenant);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Tenant must have a valid, non-empty identifier.*")
           .WithParameterName("tenant");
    }

    [Fact]
    public void CreateScope_ValidTenant_CreatesScopeAndBindsContext()
    {
        var factory = new DefaultTenantScopeFactory(new FakeScopeFactory());
        var tenant = new TenantInfo(TenantId.NewId(), "Valid");

        var scope = factory.CreateScope(tenant, TenantResolutionSource.ExplicitScope);

        scope.TenantContext.IsResolved.Should().BeTrue();
        scope.TenantContext.Tenant.Should().BeSameAs(tenant);
        scope.TenantContext.Source.Should().Be(TenantResolutionSource.ExplicitScope);
        scope.ServiceProvider.Should().NotBeNull();
    }

    [Fact]
    public void DefaultTenantScope_Dispose_DisposesInnerScopeOnlyOnce()
    {
        var fakeFactory = new FakeScopeFactory();
        var factory = new DefaultTenantScopeFactory(fakeFactory);
        var tenant = new TenantInfo(TenantId.NewId(), "Valid");

        var scope = factory.CreateScope(tenant);

        scope.Dispose();
        fakeFactory.ScopeToReturn.DisposeCalls.Should().Be(1);

        // Second call should not trigger inner dispose again
        scope.Dispose();
        fakeFactory.ScopeToReturn.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public async Task DefaultTenantScope_DisposeAsync_DisposesInnerScopeAsyncOnlyOnce()
    {
        var fakeFactory = new FakeScopeFactory();
        var factory = new DefaultTenantScopeFactory(fakeFactory);
        var tenant = new TenantInfo(TenantId.NewId(), "Valid");

        var scope = factory.CreateScope(tenant);

        await scope.DisposeAsync();
        fakeFactory.ScopeToReturn.DisposeAsyncCalls.Should().Be(1);

        // Second call should not trigger inner dispose again
        await scope.DisposeAsync();
        fakeFactory.ScopeToReturn.DisposeAsyncCalls.Should().Be(1);
    }

    [Fact]
    public async Task DefaultTenantScope_DisposeAsync_FallbackToSyncDisposeWhenNotAsyncDisposable()
    {
        var fakeFactory = new FakeScopeFactorySyncOnly();
        var factory = new DefaultTenantScopeFactory(fakeFactory);
        var tenant = new TenantInfo(TenantId.NewId(), "Valid");

        var scope = factory.CreateScope(tenant);

        await scope.DisposeAsync();
        fakeFactory.ScopeToReturn.DisposeCalls.Should().Be(1);
    }
}
