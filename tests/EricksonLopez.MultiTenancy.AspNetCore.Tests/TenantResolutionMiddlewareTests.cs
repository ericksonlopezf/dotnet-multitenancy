// Copyright © Erickson Lopez. MIT License.
#pragma warning disable CA2012 // ValueTask instances should be awaited (Safe to ignore in NSubstitute configurations)
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.AspNetCore;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class TenantResolutionMiddlewareTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly TenantId AlphaTenantId = new TenantId(AlphaGuid);

    [Fact]
    public void Constructor_NullNext_ThrowsArgumentNullException()
    {
        var logger = Substitute.For<ILogger<TenantResolutionMiddleware>>();
        var act = () => new TenantResolutionMiddleware(null!, logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("next");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var next = new RequestDelegate(_ => Task.CompletedTask);
        var act = () => new TenantResolutionMiddleware(next, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task InvokeAsync_NullContext_ThrowsArgumentNullException()
    {
        var middleware = CreateMiddleware();
        var act = () => middleware.InvokeAsync(null!, [], Substitute.For<ITenantStore>(), Substitute.For<ITenantContextAccessor>());
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task InvokeAsync_NullStrategies_ThrowsArgumentNullException()
    {
        var middleware = CreateMiddleware();
        var act = () => middleware.InvokeAsync(new DefaultHttpContext(), null!, Substitute.For<ITenantStore>(), Substitute.For<ITenantContextAccessor>());
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("strategies");
    }

    [Fact]
    public async Task InvokeAsync_NullStore_ThrowsArgumentNullException()
    {
        var middleware = CreateMiddleware();
        var act = () => middleware.InvokeAsync(new DefaultHttpContext(), [], null!, Substitute.For<ITenantContextAccessor>());
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("tenantStore");
    }

    [Fact]
    public async Task InvokeAsync_NullAccessor_ThrowsArgumentNullException()
    {
        var middleware = CreateMiddleware();
        var act = () => middleware.InvokeAsync(new DefaultHttpContext(), [], Substitute.For<ITenantStore>(), null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("accessor");
    }

    [Fact]
    public async Task InvokeAsync_NoStrategyResolves_DoesNotSetContextAndCallsNext()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var strategy = Substitute.For<ITenantResolutionStrategy>();
        strategy.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.Unresolved)));

        var accessor = Substitute.For<ITenantContextAccessor>();
        var store = Substitute.For<ITenantStore>();

        await middleware.InvokeAsync(new DefaultHttpContext(), new[] { strategy }, store, accessor);

        accessor.DidNotReceive().TenantContext = Arg.Any<ITenantContext>();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_StrategyResolvesEmptyTenantId_DoesNotSetContextAndCallsNext()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var strategy = Substitute.For<ITenantResolutionStrategy>();
        strategy.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(TenantId.Empty)));

        var accessor = Substitute.For<ITenantContextAccessor>();
        var store = Substitute.For<ITenantStore>();

        await middleware.InvokeAsync(new DefaultHttpContext(), new[] { strategy }, store, accessor);

        accessor.DidNotReceive().TenantContext = Arg.Any<ITenantContext>();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_StrategyResolvesButStoreReturnsFailure_DoesNotSetContextAndCallsNext()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var strategy = Substitute.For<ITenantResolutionStrategy>();
        strategy.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(AlphaTenantId)));

        var store = Substitute.For<ITenantStore>();
        store.GetTenantAsync(AlphaTenantId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result<ITenantInfo>.Failure(TenantErrors.NotFound(AlphaTenantId))));

        var accessor = Substitute.For<ITenantContextAccessor>();

        await middleware.InvokeAsync(new DefaultHttpContext(), new[] { strategy }, store, accessor);

        accessor.DidNotReceive().TenantContext = Arg.Any<ITenantContext>();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MultipleStrategiesResolveSameTenant_SetsContext()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var strategy1 = Substitute.For<ITenantResolutionStrategy>();
        strategy1.StrategyName.Returns("Strategy1");
        strategy1.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(AlphaTenantId)));

        var strategy2 = Substitute.For<ITenantResolutionStrategy>();
        strategy2.StrategyName.Returns("Strategy2");
        strategy2.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(AlphaTenantId)));

        var tenantInfo = new TenantInfo(AlphaTenantId, "Alpha");
        var store = Substitute.For<ITenantStore>();
        store.GetTenantAsync(AlphaTenantId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result<ITenantInfo>.Success(tenantInfo)));

        var accessor = Substitute.For<ITenantContextAccessor>();

        await middleware.InvokeAsync(new DefaultHttpContext(), new[] { strategy1, strategy2 }, store, accessor);

        // Verify both were evaluated
        _ = await strategy2.Received(1).ResolveTenantIdAsync(Arg.Any<CancellationToken>());

        accessor.Received(1).TenantContext = Arg.Is<ITenantContext>(ctx => ctx.RequiredTenant.Id == AlphaTenantId);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MultipleStrategiesResolveDifferentTenants_WithLoggerEnabled_LogsAndThrowsInvalidOperationException()
    {
        var logger = Substitute.For<ILogger<TenantResolutionMiddleware>>();
        logger.IsEnabled(LogLevel.Warning).Returns(true);
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, logger);

        var strategy1 = Substitute.For<ITenantResolutionStrategy>();
        strategy1.StrategyName.Returns("Strategy1");
        strategy1.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(AlphaTenantId)));

        var betaTenantId = new TenantId(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));
        var strategy2 = Substitute.For<ITenantResolutionStrategy>();
        strategy2.StrategyName.Returns("Strategy2");
        strategy2.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(betaTenantId)));

        var store = Substitute.For<ITenantStore>();
        var accessor = Substitute.For<ITenantContextAccessor>();

        var act = () => middleware.InvokeAsync(new DefaultHttpContext(), new[] { strategy1, strategy2 }, store, accessor);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tenant resolution conflict detected*");
        ex.Which.Message.Should().Contain($"First: '{AlphaTenantId}' (by Strategy1), Second: '{betaTenantId}' (by Strategy2)");

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Is<EventId>(e => e.Id == 1),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeAsync_ThreeStrategies_TwoSameOneDifferent_PreservesFirstStrategyNameInConflict()
    {
        var middleware = CreateMiddleware();

        var strategy1 = Substitute.For<ITenantResolutionStrategy>();
        strategy1.StrategyName.Returns("Strategy1");
        strategy1.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(AlphaTenantId)));

        var strategy2 = Substitute.For<ITenantResolutionStrategy>();
        strategy2.StrategyName.Returns("Strategy2");
        strategy2.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(AlphaTenantId)));

        var betaTenantId = new TenantId(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));
        var strategy3 = Substitute.For<ITenantResolutionStrategy>();
        strategy3.StrategyName.Returns("Strategy3");
        strategy3.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(betaTenantId)));

        var store = Substitute.For<ITenantStore>();
        var accessor = Substitute.For<ITenantContextAccessor>();

        var act = () => middleware.InvokeAsync(new DefaultHttpContext(), new[] { strategy1, strategy2, strategy3 }, store, accessor);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain($"First: '{AlphaTenantId}' (by Strategy1), Second: '{betaTenantId}' (by Strategy3)");
    }

    [Fact]
    public async Task InvokeAsync_MultipleStrategiesResolveDifferentTenants_WithLoggerDisabled_ThrowsInvalidOperationException()
    {
        var logger = Substitute.For<ILogger<TenantResolutionMiddleware>>();
        logger.IsEnabled(LogLevel.Warning).Returns(false);
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, logger);

        var strategy1 = Substitute.For<ITenantResolutionStrategy>();
        strategy1.StrategyName.Returns("Strategy1");
        strategy1.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(AlphaTenantId)));

        var betaTenantId = new TenantId(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));
        var strategy2 = Substitute.For<ITenantResolutionStrategy>();
        strategy2.StrategyName.Returns("Strategy2");
        strategy2.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(betaTenantId)));

        var store = Substitute.For<ITenantStore>();
        var accessor = Substitute.For<ITenantContextAccessor>();

        var act = () => middleware.InvokeAsync(new DefaultHttpContext(), new[] { strategy1, strategy2 }, store, accessor);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tenant resolution conflict detected*");
    }

    [Fact]
    public async Task InvokeAsync_FirstFailsSecondResolves_SetsContext()
    {
        var middleware = CreateMiddleware();

        var strategy1 = Substitute.For<ITenantResolutionStrategy>();
        strategy1.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Failure(TenantErrors.Unresolved)));

        var strategy2 = Substitute.For<ITenantResolutionStrategy>();
        strategy2.ResolveTenantIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<TenantId>.Success(AlphaTenantId)));

        var tenantInfo = new TenantInfo(AlphaTenantId, "Alpha");
        var store = Substitute.For<ITenantStore>();
        store.GetTenantAsync(AlphaTenantId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result<ITenantInfo>.Success(tenantInfo)));

        var accessor = Substitute.For<ITenantContextAccessor>();

        await middleware.InvokeAsync(new DefaultHttpContext(), new[] { strategy1, strategy2 }, store, accessor);

        accessor.Received(1).TenantContext = Arg.Is<ITenantContext>(ctx => ctx.RequiredTenant.Id == AlphaTenantId);
    }

    private static TenantResolutionMiddleware CreateMiddleware(RequestDelegate? next = null, ILogger<TenantResolutionMiddleware>? logger = null)
    {
        var defaultLogger = Substitute.For<ILogger<TenantResolutionMiddleware>>();
        defaultLogger.IsEnabled(LogLevel.Warning).Returns(true);
        return new TenantResolutionMiddleware(
            next ?? (_ => Task.CompletedTask),
            logger ?? defaultLogger);
    }
}
