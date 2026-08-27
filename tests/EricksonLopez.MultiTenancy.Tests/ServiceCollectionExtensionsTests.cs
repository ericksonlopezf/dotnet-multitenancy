// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class ServiceCollectionExtensionsTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    // ─────────────────────────────────────────────────────────
    // AddMultiTenancy (Non-Generic)
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void AddMultiTenancy_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddMultiTenancy();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddMultiTenancy_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        services.AddMultiTenancy();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.Should().BeOfType<ScopedTenantContextAccessor>();

        var factory = provider.GetRequiredService<ITenantScopeFactory>();
        factory.Should().BeOfType<DefaultTenantScopeFactory>();

        // Default resolution is Empty
        var context = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        context.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void AddMultiTenancy_ITenantContextResolvesFromAccessor()
    {
        var services = new ServiceCollection();
        services.AddMultiTenancy();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "Alpha");
        accessor.TenantContext = TenantContext.Create(tenant);

        var context = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        context.IsResolved.Should().BeTrue();
        context.RequiredTenant.Id.Value.Should().Be(AlphaGuid);
    }

    // ─────────────────────────────────────────────────────────
    // AddMultiTenancy<TTenant> (Generic)
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void AddMultiTenancyGeneric_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddMultiTenancy<TenantInfo>();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddMultiTenancyGeneric_NoContext_ResolvesToEmpty()
    {
        var services = new ServiceCollection();
        services.AddMultiTenancy<TenantInfo>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ITenantContext<TenantInfo>>();
        context.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void AddMultiTenancyGeneric_ContextMatchesTypedContext_ResolvesDirectly()
    {
        var services = new ServiceCollection();
        services.AddMultiTenancy<TenantInfo>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "Alpha");
        var originalContext = new TenantContext<TenantInfo>(tenant);
        accessor.TenantContext = originalContext;

        var context = scope.ServiceProvider.GetRequiredService<ITenantContext<TenantInfo>>();
        context.Should().BeSameAs(originalContext);
    }

    public record CustomTenant : TenantInfo
    {
        public CustomTenant(TenantId id, string name) : base(id, name) { }
    }

    [Fact]
    public void AddMultiTenancyGeneric_TenantMatchesButContextIsNotTyped_WrapsContext()
    {
        var services = new ServiceCollection();
        services.AddMultiTenancy<CustomTenant>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var tenant = new CustomTenant(new TenantId(AlphaGuid), "Custom");

        // Context is non-generic ITenantContext internally
        ITenantContext untypedContext = TenantContext.Create(tenant, TenantResolutionSource.ExplicitScope);
        accessor.TenantContext = untypedContext;

        var context = scope.ServiceProvider.GetRequiredService<ITenantContext<CustomTenant>>();

        context.Should().NotBeSameAs(untypedContext); // Wrapped
        context.IsResolved.Should().BeTrue();
        context.Tenant.Should().BeSameAs(tenant);
        context.Source.Should().Be(TenantResolutionSource.ExplicitScope);
    }

    [Fact]
    public void AddMultiTenancyGeneric_ContextHasIncompatibleTenant_ResolvesToEmpty()
    {
        var services = new ServiceCollection();
        services.AddMultiTenancy<CustomTenant>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "Incompatible base tenant");

        accessor.TenantContext = TenantContext.Create(tenant);

        var context = scope.ServiceProvider.GetRequiredService<ITenantContext<CustomTenant>>();
        context.IsResolved.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────
    // AddInMemoryTenantStore<TTenant>
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void AddInMemoryTenantStore_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddInMemoryTenantStore<TenantInfo>();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public async Task AddInMemoryTenantStore_RegistersSingletonsAndConfiguresStore()
    {
        var services = new ServiceCollection();

        bool configureCalled = false;
        services.AddInMemoryTenantStore<TenantInfo>(store =>
        {
            configureCalled = true;
            store.AddOrUpdate(new TenantInfo(new TenantId(AlphaGuid), "Alpha"));
        });

        using var provider = services.BuildServiceProvider();

        var genericStore = provider.GetRequiredService<ITenantStore<TenantInfo>>();
        var untypedStore = provider.GetRequiredService<ITenantStore>();

        genericStore.Should().BeSameAs(untypedStore);
        configureCalled.Should().BeTrue();

        var tenantResult = await genericStore.GetTenantAsync(new TenantId(AlphaGuid));
        tenantResult.IsSuccess.Should().BeTrue();
        tenantResult.Value.Name.Should().Be("Alpha");
    }

    // ─────────────────────────────────────────────────────────
    // AddHttpRemoteTenantStore<TTenant>
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void AddHttpRemoteTenantStore_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddHttpRemoteTenantStore<TenantInfo>();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");

        var actWithConfig = () => services.AddHttpRemoteTenantStore<TenantInfo>(opt => opt.BaseAddress = new Uri("https://test.com"));
        actWithConfig.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddHttpRemoteTenantStore_RegistersAndResolvesAllStoreInterfaces()
    {
        var services = new ServiceCollection();
        services.AddHttpRemoteTenantStore<TenantInfo>(options =>
        {
            options.BaseAddress = new Uri("https://tenants.test.internal");
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EricksonLopez.MultiTenancy.Stores.HttpRemoteTenantStoreOptions>>().Value;
        options.BaseAddress.Should().Be(new Uri("https://tenants.test.internal"));

        var typedStore = scope.ServiceProvider.GetRequiredService<EricksonLopez.MultiTenancy.Stores.HttpRemoteTenantStore<TenantInfo>>();
        typedStore.Should().NotBeNull();

        var genericStore = scope.ServiceProvider.GetRequiredService<ITenantStore<TenantInfo>>();
        genericStore.Should().BeSameAs(typedStore);

        var untypedStore = scope.ServiceProvider.GetRequiredService<ITenantStore>();
        untypedStore.Should().BeSameAs(typedStore);

        var genericLookupStore = scope.ServiceProvider.GetRequiredService<ITenantLookupStore<TenantInfo>>();
        genericLookupStore.Should().BeSameAs(typedStore);

        var untypedLookupStore = scope.ServiceProvider.GetRequiredService<ITenantLookupStore>();
        untypedLookupStore.Should().BeSameAs(typedStore);
    }

    [Fact]
    public void AddHttpRemoteTenantStore_WithoutConfigure_UsesDefaults()
    {
        var services = new ServiceCollection();
        services.AddHttpRemoteTenantStore<TenantInfo>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var typedStore = scope.ServiceProvider.GetRequiredService<EricksonLopez.MultiTenancy.Stores.HttpRemoteTenantStore<TenantInfo>>();
        typedStore.Should().NotBeNull();
    }

    [Fact]
    public async Task AddHttpRemoteTenantStore_WithRegisteredHttpClient_UsesRegisteredClient()
    {
        var services = new ServiceCollection();
        var clientUsed = false;
        var handler = new DelegateHttpMessageHandler((req, ct) =>
        {
            clientUsed = true;
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        });
        var customClient = new System.Net.Http.HttpClient(handler) { BaseAddress = new Uri("https://preconfigured.tenants.com") };
        services.AddSingleton(customClient);
        services.AddHttpRemoteTenantStore<TenantInfo>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var typedStore = scope.ServiceProvider.GetRequiredService<EricksonLopez.MultiTenancy.Stores.HttpRemoteTenantStore<TenantInfo>>();
        typedStore.Should().NotBeNull();

        await typedStore.GetTenantAsync(TenantId.NewId());
        clientUsed.Should().BeTrue();
    }

    [Fact]
    public async Task AddHttpRemoteTenantStore_WithRegisteredOptions_UsesRegisteredOptions()
    {
        var services = new ServiceCollection();
        string? requestedPath = null;
        var handler = new DelegateHttpMessageHandler((req, ct) =>
        {
            requestedPath = req.RequestUri?.PathAndQuery;
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        });
        var customClient = new System.Net.Http.HttpClient(handler) { BaseAddress = new Uri("https://custom-opts.tenants.com") };
        services.AddSingleton(customClient);

        services.AddSingleton<Microsoft.Extensions.Options.IOptions<EricksonLopez.MultiTenancy.Stores.HttpRemoteTenantStoreOptions>>(
            Microsoft.Extensions.Options.Options.Create(new EricksonLopez.MultiTenancy.Stores.HttpRemoteTenantStoreOptions
            {
                EndpointTemplate = "/custom-remote-tenants/{0}"
            }));
        services.AddHttpRemoteTenantStore<TenantInfo>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var typedStore = scope.ServiceProvider.GetRequiredService<EricksonLopez.MultiTenancy.Stores.HttpRemoteTenantStore<TenantInfo>>();
        typedStore.Should().NotBeNull();

        var testId = TenantId.NewId();
        await typedStore.GetTenantAsync(testId);
        requestedPath.Should().Be($"/custom-remote-tenants/{testId.Value}");
    }

    private sealed class DelegateHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, Task<System.Net.Http.HttpResponseMessage>> _handler;

        public DelegateHttpMessageHandler(Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, Task<System.Net.Http.HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}



