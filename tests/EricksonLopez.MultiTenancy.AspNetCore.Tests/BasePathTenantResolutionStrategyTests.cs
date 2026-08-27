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

public class BasePathTenantResolutionStrategyTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly TenantId AlphaTenantId = new TenantId(AlphaGuid);

    [Fact]
    public void Constructor_NullHttpContextAccessor_ThrowsArgumentNullException()
    {
        var act = () => new BasePathTenantResolutionStrategy(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpContextAccessor");
    }

    [Fact]
    public void Constructor_NegativeSegmentIndex_ThrowsArgumentOutOfRangeException()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var act = () => new BasePathTenantResolutionStrategy(accessor, null, -1);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("segmentIndex")
            .WithMessage("*Segment index must be non-negative.*");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_NullHttpContext_ReturnsFailure()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var strategy = new BasePathTenantResolutionStrategy(accessor);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_EmptyPath_ReturnsFailure()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_SingleSlashPath_ReturnsFailure()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("/");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor, segmentIndex: 0);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Resolution strategy 'BasePath' failed: Base path segment at index 0 is missing or invalid.");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ValidIdentifier_WithLookupStore_ReturnsTenantId()
    {
        var store = Substitute.For<ITenantStore, ITenantLookupStore>();
        var lookupStore = (ITenantLookupStore)store;

        lookupStore.GetTenantByIdentifierAsync("tenant-alpha", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Success(new TenantInfo(AlphaTenantId, "tenant-alpha"))));

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("/tenant-alpha/api/v1/orders");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor, provider, segmentIndex: 0);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_CustomSegmentIndex_ReturnsTenantId()
    {
        var store = Substitute.For<ITenantStore, ITenantLookupStore>();
        var lookupStore = (ITenantLookupStore)store;

        lookupStore.GetTenantByIdentifierAsync("tenant-alpha", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Success(new TenantInfo(AlphaTenantId, "tenant-alpha"))));

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("/api/tenants/tenant-alpha/orders");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        // Segment 0 = "api", Segment 1 = "tenants", Segment 2 = "tenant-alpha"
        var strategy = new BasePathTenantResolutionStrategy(accessor, provider, segmentIndex: 2);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ValidGuidPath_WithoutLookupStore_ReturnsTenantId()
    {
        var store = Substitute.For<ITenantStore>();
        store.GetTenantAsync(AlphaTenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Success(new TenantInfo(AlphaTenantId, "Alpha"))));

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Path = new PathString($"/{AlphaGuid}/api/v1/data");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor, provider, segmentIndex: 0);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ValidGuid_NoStoreRegistered_ReturnsTenantId()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = new PathString($"/{AlphaGuid}/api/v1/data");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor, null, segmentIndex: 0);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }

    [Fact]
    public void StrategyName_ReturnsBasePath()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var strategy = new BasePathTenantResolutionStrategy(accessor);
        strategy.StrategyName.Should().Be("BasePath");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_WhitespacePath_ReturnsFailure()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("/   ");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_SegmentIndexOutOfBounds_ReturnsFailure()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("/only-one");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor, segmentIndex: 3);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Resolution strategy 'BasePath' failed: Base path segment at index 3 is missing or invalid.");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_NonLookupStore_InvalidGuid_ReturnsInvalidIdFailure()
    {
        var store = Substitute.For<ITenantStore>();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("/invalid-guid-path/api");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor, provider, segmentIndex: 0);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_NonLookupStore_ValidGuidStoreFails_ReturnsInvalidIdFailure()
    {
        var store = Substitute.For<ITenantStore>();
        store.GetTenantAsync(AlphaTenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Failure(TenantErrors.NotFound(AlphaTenantId))));

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Path = new PathString($"/{AlphaGuid}/api");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor, provider, segmentIndex: 0);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_NullStore_InvalidGuid_ReturnsInvalidIdFailure()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("/not-a-guid/api");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor, null, segmentIndex: 0);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_LookupStoreReturnsFailure_ReturnsFailure()
    {
        var store = Substitute.For<ITenantStore, ITenantLookupStore>();
        var lookupStore = (ITenantLookupStore)store;

        lookupStore.GetTenantByIdentifierAsync("unknown-tenant", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Result<ITenantInfo>.Failure(TenantErrors.NotFound(AlphaTenantId))));

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("/unknown-tenant/api");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new BasePathTenantResolutionStrategy(accessor, provider, segmentIndex: 0);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
    }
}
