// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.AspNetCore.Strategies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class HostNameTenantResolutionStrategyTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly TenantId AlphaTenantId = new TenantId(AlphaGuid);

    [Fact]
    public void Constructor_NullHttpContextAccessor_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var act = () => new HostNameTenantResolutionStrategy(null!, services);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpContextAccessor");
    }

    [Fact]
    public void Constructor_NullServiceProvider_ThrowsArgumentNullException()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var act = () => new HostNameTenantResolutionStrategy(accessor, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("serviceProvider");
    }

    [Fact]
    public void StrategyName_ReturnsHostName()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var services = new ServiceCollection().BuildServiceProvider();
        var strategy = new HostNameTenantResolutionStrategy(accessor, services);
        strategy.StrategyName.Should().Be("HostName");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_NullHttpContext_ReturnsFailure()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var services = new ServiceCollection().BuildServiceProvider();
        var strategy = new HostNameTenantResolutionStrategy(accessor, services);

        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ResolutionFailed");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_NoDotHost_ReturnsFailure()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost");
        accessor.HttpContext.Returns(context);
        var services = new ServiceCollection().BuildServiceProvider();
        var strategy = new HostNameTenantResolutionStrategy(accessor, services);

        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_SingleDotHost_ReturnsFailure()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("example.com");
        accessor.HttpContext.Returns(context);
        var services = new ServiceCollection().BuildServiceProvider();
        var strategy = new HostNameTenantResolutionStrategy(accessor, services);

        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Resolution strategy 'HostName' failed: Host does not match expected tenant subdomain format or tenant not found.");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_LeadingDotHost_ReturnsFailure()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(".example.com");
        accessor.HttpContext.Returns(context);
        var services = new ServiceCollection().BuildServiceProvider();
        var strategy = new HostNameTenantResolutionStrategy(accessor, services);

        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Resolution strategy 'HostName' failed: Host does not match expected tenant subdomain format or tenant not found.");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ConsecutiveDotsHost_ReturnsFailure()
    {
        var store = Substitute.For<ITenantStore, ITenantLookupStore>();
        var lookupStore = (ITenantLookupStore)store;
        lookupStore.GetTenantByIdentifierAsync("acme", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Failure(TenantErrors.NotFound(AlphaTenantId))));

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var accessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("acme..com");
        accessor.HttpContext.Returns(context);

        var strategy = new HostNameTenantResolutionStrategy(accessor, provider);

        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Resolution strategy 'HostName' failed: Host does not match expected tenant subdomain format or tenant not found.");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_StoreNotRegistered_ReturnsFailure()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("tenant.example.com");
        accessor.HttpContext.Returns(context);
        var services = new ServiceCollection().BuildServiceProvider();
        var strategy = new HostNameTenantResolutionStrategy(accessor, services);

        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Resolution strategy 'HostName' failed: ITenantStore is not registered.");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ValidSubdomain_StoreSupportsLookup_ReturnsTenantId()
    {
        var store = Substitute.For<ITenantStore, ITenantLookupStore>();
        var lookupStore = (ITenantLookupStore)store;

        lookupStore.GetTenantByIdentifierAsync("acme", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Success(new TenantInfo(AlphaTenantId, "acme"))));

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("acme.myapp.com");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new HostNameTenantResolutionStrategy(accessor, provider);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_LookupStoreFails_ReturnsFailure()
    {
        var store = Substitute.For<ITenantStore, ITenantLookupStore>();
        var lookupStore = (ITenantLookupStore)store;

        lookupStore.GetTenantByIdentifierAsync("unknown", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Failure(TenantErrors.NotFound(AlphaTenantId))));

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("unknown.myapp.com");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new HostNameTenantResolutionStrategy(accessor, provider);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ValidGuidSubdomain_StoreDoesNotSupportLookup_ReturnsTenantId()
    {
        var store = Substitute.For<ITenantStore>();
        store.GetTenantAsync(AlphaTenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Success(new TenantInfo(AlphaTenantId, "Alpha"))));

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString($"{AlphaGuid}.myapp.com");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new HostNameTenantResolutionStrategy(accessor, provider);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_InvalidGuidSubdomain_StoreDoesNotSupportLookup_ReturnsFailure()
    {
        var store = Substitute.For<ITenantStore>();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("not-a-guid.myapp.com");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new HostNameTenantResolutionStrategy(accessor, provider);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ValidGuidSubdomain_StoreFails_ReturnsFailure()
    {
        var store = Substitute.For<ITenantStore>();
        store.GetTenantAsync(AlphaTenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Failure(TenantErrors.NotFound(AlphaTenantId))));

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString($"{AlphaGuid}.myapp.com");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new HostNameTenantResolutionStrategy(accessor, provider);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_LocalhostSubdomain_ResolvesCorrectly()
    {
        var tenant = new TenantInfo { Id = AlphaTenantId, Name = "acme" };
        var lookupStore = Substitute.For<ITenantStore, ITenantLookupStore>();
        ((ITenantLookupStore)lookupStore).GetTenantByIdentifierAsync("acme", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Success(tenant)));

        var services = new ServiceCollection();
        services.AddSingleton((ITenantStore)lookupStore);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("acme.localhost:5000");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new HostNameTenantResolutionStrategy(accessor, provider);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("192.168.1.100")]
    [InlineData("10.0.0.1")]
    public async Task ResolveTenantIdAsync_IpAddressHost_ReturnsFailure(string ipHost)
    {
        var services = new ServiceCollection().BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(ipHost);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new HostNameTenantResolutionStrategy(accessor, services);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ResolutionFailed");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_WithBaseDomain_MultiLevelSubdomain_ResolvesCorrectly()
    {
        var tenant = new TenantInfo { Id = AlphaTenantId, Name = "acme" };
        var lookupStore = Substitute.For<ITenantStore, ITenantLookupStore>();
        ((ITenantLookupStore)lookupStore).GetTenantByIdentifierAsync("acme", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Success(tenant)));

        var services = new ServiceCollection();
        services.AddSingleton((ITenantStore)lookupStore);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("api.acme.platform.com:443");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var options = Microsoft.Extensions.Options.Options.Create(new EricksonLopez.MultiTenancy.AspNetCore.Options.HostNameTenantResolutionStrategyOptions
        {
            BaseDomain = "platform.com"
        });

        var strategy = new HostNameTenantResolutionStrategy(accessor, provider, options);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_WithExtractionPattern_ResolvesCorrectly()
    {
        var tenant = new TenantInfo { Id = AlphaTenantId, Name = "acme" };
        var lookupStore = Substitute.For<ITenantStore, ITenantLookupStore>();
        ((ITenantLookupStore)lookupStore).GetTenantByIdentifierAsync("acme", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Success(tenant)));

        var services = new ServiceCollection();
        services.AddSingleton((ITenantStore)lookupStore);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("service-acme-eu.cloud.io");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var options = Microsoft.Extensions.Options.Options.Create(new EricksonLopez.MultiTenancy.AspNetCore.Options.HostNameTenantResolutionStrategyOptions
        {
            ExtractionPattern = @"^service-(?<tenant>[a-z0-9]+)-eu\.cloud\.io$"
        });

        var strategy = new HostNameTenantResolutionStrategy(accessor, provider, options);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }
}
