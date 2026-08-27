// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.Stores;
using EricksonLopez.Result;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests.Stores;

public class CachedTenantStoreTests
{
    private static readonly TenantId TestId = TenantId.NewId();
    private const string TestIdentifier = "test-tenant";

    private readonly ITenantStore<TenantInfo> _innerStore;
    private readonly IMemoryCache _cache;
    private readonly IOptions<CachedTenantStoreOptions> _options;
    private readonly CachedTenantStore<TenantInfo> _cachedStore;
    private readonly TenantInfo _testTenant;

    public CachedTenantStoreTests()
    {
        _innerStore = Substitute.For<ITenantStore<TenantInfo>>();

        var services = new ServiceCollection();
        services.AddMemoryCache();
        var sp = services.BuildServiceProvider();
        _cache = sp.GetRequiredService<IMemoryCache>();

        _options = Microsoft.Extensions.Options.Options.Create(new CachedTenantStoreOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });

        _cachedStore = new CachedTenantStore<TenantInfo>(_innerStore, _cache, _options);

        _testTenant = new TenantInfo { Id = TestId, Name = "Test Tenant" };
    }

    [Fact]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        var act1 = () => new CachedTenantStore<TenantInfo>(null!, _cache, _options);
        act1.Should().Throw<ArgumentNullException>().WithParameterName("innerStore");

        var act2 = () => new CachedTenantStore<TenantInfo>(_innerStore, null!, _options);
        act2.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public async Task GetTenantAsync_CacheMiss_CallsInnerStoreAndCaches()
    {
        _innerStore.GetTenantAsync(TestId, default).Returns(Result<TenantInfo>.Success(_testTenant));

        var result = await _cachedStore.GetTenantAsync(TestId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(_testTenant);

        await _innerStore.Received(1).GetTenantAsync(TestId, default);

        _cache.TryGetValue($"TenantStore_Id_{TestId.Value:N}", out TenantInfo? cachedById).Should().BeTrue();
        cachedById.Should().BeSameAs(_testTenant);
    }

    [Fact]
    public async Task GetTenantAsync_CacheHit_ReturnsFromCache()
    {
        _cache.Set($"TenantStore_Id_{TestId.Value:N}", _testTenant);

        var result = await _cachedStore.GetTenantAsync(TestId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(_testTenant);

        await _innerStore.DidNotReceive().GetTenantAsync(Arg.Any<TenantId>(), default);
    }

    [Fact]
    public async Task GetTenantAsync_InnerStoreFails_DoesNotCache()
    {
        var error = TenantErrors.NotFound(TestId);
        _innerStore.GetTenantAsync(TestId, default).Returns(Result<TenantInfo>.Failure(error));

        var result = await _cachedStore.GetTenantAsync(TestId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);

        _cache.TryGetValue($"TenantStore_Id_{TestId.Value:N}", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ITenantStoreGetTenantAsync_ExplicitInterface_SuccessAndFailure()
    {
        _innerStore.GetTenantAsync(TestId, default).Returns(Result<TenantInfo>.Success(_testTenant));

        ITenantStore interfaceStore = _cachedStore;
        var successResult = await interfaceStore.GetTenantAsync(TestId);
        successResult.IsSuccess.Should().BeTrue();
        successResult.Value.Should().BeSameAs(_testTenant);

        var failId = TenantId.NewId();
        var error = TenantErrors.NotFound(failId);
        _innerStore.GetTenantAsync(failId, default).Returns(Result<TenantInfo>.Failure(error));

        var failResult = await interfaceStore.GetTenantAsync(failId);
        failResult.IsFailure.Should().BeTrue();
        failResult.Error.Should().Be(error);
    }

    [Fact]
    public async Task Constructor_NullOptions_UsesDefaultOptions()
    {
        var store = new CachedTenantStore<TenantInfo>(_innerStore, _cache, null!);
        _innerStore.GetTenantAsync(TestId, default).Returns(Result<TenantInfo>.Success(_testTenant));

        var result = await store.GetTenantAsync(TestId);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetTenantAsync_WithSlidingExpiration_CachesCorrectly()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new CachedTenantStoreOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(2)
        });
        var store = new CachedTenantStore<TenantInfo>(_innerStore, _cache, options);
        _innerStore.GetTenantAsync(TestId, default).Returns(Result<TenantInfo>.Success(_testTenant));

        var result = await store.GetTenantAsync(TestId);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetTenantAsync_PassesConfiguredExpirationsToCache()
    {
        var mockCache = Substitute.For<IMemoryCache>();
        var mockEntry = Substitute.For<ICacheEntry>();
        mockCache.CreateEntry(Arg.Any<object>()).Returns(mockEntry);

        object? nullValue = null;
        mockCache.TryGetValue(Arg.Any<object>(), out nullValue).Returns(false);

        var options = Microsoft.Extensions.Options.Options.Create(new CachedTenantStoreOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(42),
            SlidingExpiration = TimeSpan.FromMinutes(17)
        });

        var store = new CachedTenantStore<TenantInfo>(_innerStore, mockCache, options);
        _innerStore.GetTenantAsync(TestId, default).Returns(Result<TenantInfo>.Success(_testTenant));

        var result = await store.GetTenantAsync(TestId);
        result.IsSuccess.Should().BeTrue();

        mockEntry.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromMinutes(42));
        mockEntry.SlidingExpiration.Should().Be(TimeSpan.FromMinutes(17));
    }
}


