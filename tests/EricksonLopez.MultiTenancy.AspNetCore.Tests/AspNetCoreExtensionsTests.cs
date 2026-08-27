// Copyright © Erickson Lopez. MIT License.
#pragma warning disable CA2012, IL2026, IL3050 // ValueTask instances should be awaited (Safe to ignore in NSubstitute configurations) and AOT/Trimming warnings are safe in tests
using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.AspNetCore;
using EricksonLopez.MultiTenancy.AspNetCore.Strategies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class AspNetCoreExtensionsTests
{
    // ─────────────────────────────────────────────────────────
    // RequireTenantFilter
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RequireTenantFilter_NoTenantContext_ReturnsBadRequest()
    {
        var filter = new RequireTenantFilter();
        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        httpContext.RequestServices = services.BuildServiceProvider();

        var invocationContext = Substitute.For<EndpointFilterInvocationContext>();
        invocationContext.HttpContext.Returns(httpContext);

        var next = Substitute.For<EndpointFilterDelegate>();

        var result = await filter.InvokeAsync(invocationContext, next);

        // Results.Problem creates an IResult that returns HTTP 400.
        var problemResult = result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>().Subject;
        problemResult.ProblemDetails.Title.Should().Be("Tenant Required");
        problemResult.ProblemDetails.Detail.Should().Be("A valid tenant identifier is required to access this resource.");

        // Assert we didn't call the next delegate
        _ = await next.DidNotReceive().Invoke(Arg.Any<EndpointFilterInvocationContext>());
    }

    [Fact]
    public async Task RequireTenantFilter_TenantContextNotResolved_ReturnsBadRequest()
    {
        var filter = new RequireTenantFilter();
        var httpContext = new DefaultHttpContext();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(false);

        var services = new ServiceCollection();
        services.AddSingleton(tenantContext);
        httpContext.RequestServices = services.BuildServiceProvider();

        var invocationContext = Substitute.For<EndpointFilterInvocationContext>();
        invocationContext.HttpContext.Returns(httpContext);

        var next = Substitute.For<EndpointFilterDelegate>();

        var result = await filter.InvokeAsync(invocationContext, next);

        var problemResult = result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>().Subject;
        problemResult.ProblemDetails.Title.Should().Be("Tenant Required");
        problemResult.ProblemDetails.Detail.Should().Be("A valid tenant identifier is required to access this resource.");
        _ = await next.DidNotReceive().Invoke(Arg.Any<EndpointFilterInvocationContext>());
    }

    [Fact]
    public async Task RequireTenantFilter_TenantContextResolved_CallsNext()
    {
        var filter = new RequireTenantFilter();
        var httpContext = new DefaultHttpContext();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(true);

        var services = new ServiceCollection();
        services.AddSingleton(tenantContext);
        httpContext.RequestServices = services.BuildServiceProvider();

        var invocationContext = Substitute.For<EndpointFilterInvocationContext>();
        invocationContext.HttpContext.Returns(httpContext);

        var next = Substitute.For<EndpointFilterDelegate>();
        var expectedResult = new object();
        next.Invoke(invocationContext).Returns(new ValueTask<object?>(expectedResult));

        var result = await filter.InvokeAsync(invocationContext, next);

        result.Should().Be(expectedResult);
    }

    // ─────────────────────────────────────────────────────────
    // AspNetCoreMultiTenancyExtensions
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void AddAspNetCoreMultiTenancy_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddAspNetCoreMultiTenancy();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddAspNetCoreMultiTenancy_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddAspNetCoreMultiTenancy();

        services.Should().Contain(sd => sd.ServiceType == typeof(IHttpContextAccessor));
        services.Should().Contain(sd => sd.ServiceType == typeof(ITenantContextAccessor));

        var strategies = services.Where(sd => sd.ServiceType == typeof(ITenantResolutionStrategy)).ToList();
        strategies.Should().HaveCount(1);
        strategies.Should().Contain(sd => sd.ImplementationType == typeof(ClaimTenantResolutionStrategy));
    }

    [Fact]
    public void AddInternalHeaderTenantResolution_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddInternalHeaderTenantResolution();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddInternalHeaderTenantResolution_RegistersHeaderStrategy()
    {
        var services = new ServiceCollection();
        services.AddInternalHeaderTenantResolution();

        var strategies = services.Where(sd => sd.ServiceType == typeof(ITenantResolutionStrategy)).ToList();
        strategies.Should().HaveCount(1);
        strategies.Should().Contain(sd => sd.ImplementationType == typeof(HeaderTenantResolutionStrategy));
    }

    [Fact]
    public void AddDelegateTenantStrategy_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddDelegateTenantStrategy(_ => ValueTask.FromResult(Result.Result<TenantId>.Success(TenantId.NewId())));
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddDelegateTenantStrategy_NullResolver_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddDelegateTenantStrategy(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("resolver");
    }

    [Fact]
    public async Task AddDelegateTenantStrategy_RegistersAndResolvesStrategy()
    {
        var expectedId = TenantId.NewId();
        var services = new ServiceCollection();
        services.AddDelegateTenantStrategy(ct => ValueTask.FromResult(Result.Result<TenantId>.Success(expectedId)));

        using var sp = services.BuildServiceProvider();
        var strategy = sp.GetRequiredService<ITenantResolutionStrategy>();
        strategy.Should().BeOfType<DelegateTenantResolutionStrategy>();
        strategy.StrategyName.Should().Be("Delegate");

        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedId);
    }

    [Fact]
    public void AddStaticTenantStrategy_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddStaticTenantStrategy(TenantId.NewId());
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public async Task AddStaticTenantStrategy_RegistersAndResolvesStrategy()
    {
        var expectedId = TenantId.NewId();
        var services = new ServiceCollection();
        services.AddStaticTenantStrategy(expectedId);

        using var sp = services.BuildServiceProvider();
        var strategy = sp.GetRequiredService<ITenantResolutionStrategy>();
        strategy.Should().BeOfType<StaticTenantResolutionStrategy>();
        strategy.StrategyName.Should().Be("Static");

        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedId);
    }

    [Fact]
    public async Task StaticTenantResolutionStrategy_EmptyTenantId_ReturnsFailure()
    {
        var strategy = new StaticTenantResolutionStrategy(TenantId.Empty);
        strategy.StrategyName.Should().Be("Static");

        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ResolutionFailed");
        result.Error.Description.Should().Be("Resolution strategy 'Static' failed: Static tenant ID is empty.");
    }

    [Fact]
    public void DelegateTenantResolutionStrategy_NullResolver_ThrowsArgumentNullException()
    {
        var act = () => new DelegateTenantResolutionStrategy(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("resolver");
    }

    [Fact]
    public void AddRouteTenantStrategy_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddRouteTenantStrategy();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddRouteTenantStrategy_RegistersStrategy()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddRouteTenantStrategy("customTenantParam");

        using var sp = services.BuildServiceProvider();
        var strategies = sp.GetServices<ITenantResolutionStrategy>().ToList();
        strategies.Should().ContainSingle(s => s is RouteTenantResolutionStrategy);
    }

    [Fact]
    public void AddHostNameTenantStrategy_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddHostNameTenantStrategy();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddHostNameTenantStrategy_RegistersStrategy()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddHostNameTenantStrategy();

        using var sp = services.BuildServiceProvider();
        var strategies = sp.GetServices<ITenantResolutionStrategy>().ToList();
        strategies.Should().ContainSingle(s => s is HostNameTenantResolutionStrategy);
    }

    [Fact]
    public void AddBasePathStrategy_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddBasePathStrategy();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddBasePathStrategy_RegistersStrategy()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddBasePathStrategy(segmentIndex: 1);

        using var sp = services.BuildServiceProvider();
        var strategies = sp.GetServices<ITenantResolutionStrategy>().ToList();
        strategies.Should().ContainSingle(s => s is BasePathTenantResolutionStrategy);
    }

    [Fact]
    public void UseMultiTenancy_NullApp_ThrowsArgumentNullException()
    {
        IApplicationBuilder app = null!;
        var act = () => app.UseMultiTenancy();
        act.Should().Throw<ArgumentNullException>().WithParameterName("app");
    }

    [Fact]
    public void UseMultiTenancy_ValidApp_RegistersMiddleware()
    {
        var app = Substitute.For<IApplicationBuilder>();
        app.ApplicationServices.Returns(new ServiceCollection().BuildServiceProvider());
        app.Use(Arg.Any<Func<RequestDelegate, RequestDelegate>>()).Returns(app);

        var result = app.UseMultiTenancy();
        result.Should().BeSameAs(app);
        app.Received(1).Use(Arg.Any<Func<RequestDelegate, RequestDelegate>>());
    }

    [Fact]
    public void RequireTenant_RouteHandlerBuilder_NullBuilder_ThrowsArgumentNullException()
    {
        RouteHandlerBuilder builder = null!;
        var act = () => builder.RequireTenant();
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void RequireTenant_RouteGroupBuilder_NullBuilder_ThrowsArgumentNullException()
    {
        RouteGroupBuilder builder = null!;
        var act = () => builder.RequireTenant();
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void RequireTenant_MinimalApiExtensions_ReturnsBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        var routeGroup = app.MapGroup("/api");
        routeGroup.RequireTenant().Should().NotBeNull();

        var routeHandler = app.MapGet("/test", () => "OK");
        routeHandler.RequireTenant().Should().NotBeNull();
    }
}
