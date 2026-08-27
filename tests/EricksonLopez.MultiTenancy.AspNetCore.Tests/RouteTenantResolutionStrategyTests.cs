// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.AspNetCore.Strategies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class RouteTenantResolutionStrategyTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly TenantId AlphaTenantId = new TenantId(AlphaGuid);

    [Fact]
    public void Constructor_NullHttpContextAccessor_ThrowsArgumentNullException()
    {
        var act = () => new RouteTenantResolutionStrategy(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpContextAccessor");
    }

    [Fact]
    public async Task Constructor_NullOrWhitespaceRouteParam_DefaultsToTenantId()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues = new RouteValueDictionary
        {
            { "tenantId", AlphaGuid.ToString() }
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new RouteTenantResolutionStrategy(accessor, "   ");
        strategy.StrategyName.Should().Be("Route");

        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_NullHttpContext_ReturnsFailure()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var strategy = new RouteTenantResolutionStrategy(accessor);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ResolutionFailed");
        result.Error.Description.Should().Be("Resolution strategy 'Route' failed: Route parameter 'tenantId' is missing or invalid.");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_CustomRouteParam_Missing_ReturnsExactErrorDescription()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues = new RouteValueDictionary
        {
            { "tenantId", AlphaGuid.ToString() } // "tenantId" exists, but strategy expects "customTenant"
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new RouteTenantResolutionStrategy(accessor, "customTenant");
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Resolution strategy 'Route' failed: Route parameter 'customTenant' is missing or invalid.");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_RouteParamMissing_ReturnsFailure()
    {
        var context = new DefaultHttpContext();
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new RouteTenantResolutionStrategy(accessor);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Resolution strategy 'Route' failed: Route parameter 'tenantId' is missing or invalid.");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_NonStringRouteValue_ReturnsFailure()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues = new RouteValueDictionary
        {
            { "tenantId", 12345 }
        };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new RouteTenantResolutionStrategy(accessor);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_InvalidStringRouteValue_ReturnsInvalidIdFailure()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues = new RouteValueDictionary
        {
            { "tenantId", "not-a-valid-guid" }
        };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new RouteTenantResolutionStrategy(accessor);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_WhitespaceRouteValue_ReturnsResolutionFailed()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues = new RouteValueDictionary
        {
            { "tenantId", "   " }
        };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new RouteTenantResolutionStrategy(accessor);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ResolutionFailed");
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ValidRouteValue_ReturnsTenantId()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues = new RouteValueDictionary
        {
            { "tenantId", AlphaGuid.ToString() }
        };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new RouteTenantResolutionStrategy(accessor);
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_CustomRouteParam_ReturnsTenantId()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues = new RouteValueDictionary
        {
            { "customTenant", AlphaGuid.ToString() }
        };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var strategy = new RouteTenantResolutionStrategy(accessor, "customTenant");
        var result = await strategy.ResolveTenantIdAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AlphaTenantId);
    }
}
