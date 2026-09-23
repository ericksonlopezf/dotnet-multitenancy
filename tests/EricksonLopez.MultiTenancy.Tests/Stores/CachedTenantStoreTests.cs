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
    public async Task GetTenantAsync_InnerStoreFails_WithNegativeCachingDisabled_DoesNotCache()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new CachedTenantStoreOptions
        {
            EnableNegativeCaching = false
        });
        var store = new CachedTenantStore<TenantInfo>(_innerStore, _cache, options);

        var error = TenantErrors.NotFound(TestId);
        _innerStore.GetTenantAsync(TestId, default).Returns(Result<TenantInfo>.Failure(error));

        var result = await store.GetTenantAsync(TestId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);

        _cache.TryGetValue($"TenantStore_Id_{TestId.Value:N}", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetTenantAsync_InnerStoreFails_WithNegativeCachingEnabled_CachesFailureAndPreventsStampede()
    {
        var error = TenantErrors.NotFound(TestId);
        _innerStore.GetTenantAsync(TestId, default).Returns(Result<TenantInfo>.Failure(error));

        var result1 = await _cachedStore.GetTenantAsync(TestId);

        result1.IsFailure.Should().BeTrue();
        result1.Error.Should().Be(error);

        // Subsequent lookup should be served from negative cache without hitting innerStore again
        var result2 = await _cachedStore.GetTenantAsync(TestId);
        result2.IsFailure.Should().BeTrue();
        result2.Error.Should().Be(error);

        await _innerStore.Received(1).GetTenantAsync(TestId, default);
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetTenantByIdentifierAsync_NullOrWhitespace_ReturnsInvalidId(string? identifier)
    {
        var result = await _cachedStore.GetTenantByIdentifierAsync(identifier!);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_InnerStoreNotLookupStore_ReturnsStrategyFailed()
    {
        // _innerStore only implements ITenantStore<TenantInfo>, not ITenantLookupStore<TenantInfo>
        var result = await _cachedStore.GetTenantByIdentifierAsync("acme");
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ResolutionFailed");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_CacheMiss_CallsInnerLookupStoreAndCaches()
    {
        var innerLookup = Substitute.For<ITenantStore<TenantInfo>, ITenantLookupStore<TenantInfo>>();
        var store = new CachedTenantStore<TenantInfo>(innerLookup, _cache, _options);

        ((ITenantLookupStore<TenantInfo>)innerLookup)
            .GetTenantByIdentifierAsync("acme", Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<TenantInfo>.Success(_testTenant));

        var result = await store.GetTenantByIdentifierAsync("acme");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(_testTenant);

        // Check that it cached by identifier and by ID
        _cache.TryGetValue("TenantStore_Ident_acme", out TenantInfo? cachedByIdent).Should().BeTrue();
        cachedByIdent.Should().BeSameAs(_testTenant);

        _cache.TryGetValue($"TenantStore_Id_{TestId.Value:N}", out TenantInfo? cachedById).Should().BeTrue();
        cachedById.Should().BeSameAs(_testTenant);
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_CacheHit_ReturnsFromCache()
    {
        var innerLookup = Substitute.For<ITenantStore<TenantInfo>, ITenantLookupStore<TenantInfo>>();
        var store = new CachedTenantStore<TenantInfo>(innerLookup, _cache, _options);

        _cache.Set("TenantStore_Ident_acme", _testTenant);

        var result = await store.GetTenantByIdentifierAsync("acme");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(_testTenant);

        await ((ITenantLookupStore<TenantInfo>)innerLookup)
            .DidNotReceive()
            .GetTenantByIdentifierAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_NegativeCaching_CachesFailure()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new CachedTenantStoreOptions
        {
            EnableNegativeCaching = true,
            NegativeCacheExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        });

        var innerLookup = Substitute.For<ITenantStore<TenantInfo>, ITenantLookupStore<TenantInfo>>();
        var store = new CachedTenantStore<TenantInfo>(innerLookup, _cache, options);

        var error = TenantErrors.NotFound(TestId);
        ((ITenantLookupStore<TenantInfo>)innerLookup)
            .GetTenantByIdentifierAsync("missing", Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<TenantInfo>.Failure(error));

        var result = await store.GetTenantByIdentifierAsync("missing");
        result.IsFailure.Should().BeTrue();

        // Second call should hit negative cache
        var result2 = await store.GetTenantByIdentifierAsync("missing");
        result2.IsFailure.Should().BeTrue();

        await ((ITenantLookupStore<TenantInfo>)innerLookup)
            .Received(1)
            .GetTenantByIdentifierAsync("missing", Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async Task ITenantLookupStore_ExplicitInterface_DelegatesCorrectly()
    {
        var innerLookup = Substitute.For<ITenantStore<TenantInfo>, ITenantLookupStore<TenantInfo>>();
        var store = new CachedTenantStore<TenantInfo>(innerLookup, _cache, _options);

        ((ITenantLookupStore<TenantInfo>)innerLookup)
            .GetTenantByIdentifierAsync("globex", Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<TenantInfo>.Success(_testTenant));

        ITenantLookupStore untyped = store;
        var result = await untyped.GetTenantByIdentifierAsync("globex");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(TestId);
    }

    [Fact]
    public async Task ITenantLookupStore_ExplicitInterface_Failure_ReturnsFailure()
    {
        var innerLookup = Substitute.For<ITenantStore<TenantInfo>, ITenantLookupStore<TenantInfo>>();
        var store = new CachedTenantStore<TenantInfo>(innerLookup, _cache, _options);

        ((ITenantLookupStore<TenantInfo>)innerLookup)
            .GetTenantByIdentifierAsync("missing", Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<TenantInfo>.Failure(TenantErrors.NotFound(TestId)));

        ITenantLookupStore untyped = store;
        var result = await untyped.GetTenantByIdentifierAsync("missing");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetTenantAsync_WhenCacheContainsResultDirectly_ReturnsResult()
    {
        var cacheKey = $"TenantStore_Id_{TestId.Value:N}";
        _cache.Set(cacheKey, Result<TenantInfo>.Success(_testTenant));

        var result = await _cachedStore.GetTenantAsync(TestId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(TestId);
    }

    [Fact]
    public void AddCachedTenantStore_RegistersAndResolvesTypedAndUntypedLookupStore()
    {
        var innerLookup = Substitute.For<ITenantStore<TenantInfo>, ITenantLookupStore<TenantInfo>, ITenantLookupStore>();
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<ITenantStore<TenantInfo>>(innerLookup);
        services.AddSingleton<ITenantLookupStore<TenantInfo>>((ITenantLookupStore<TenantInfo>)innerLookup);
        services.AddSingleton<ITenantLookupStore>((ITenantLookupStore)innerLookup);

        services.AddCachedTenantStore<TenantInfo>();
        var sp = services.BuildServiceProvider();

        var typedLookup = sp.GetRequiredService<ITenantLookupStore<TenantInfo>>();
        typedLookup.Should().NotBeNull();

        var untypedLookup = sp.GetRequiredService<ITenantLookupStore>();
        untypedLookup.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTenantAsync_ConcurrentRequests_SecondCallerHitsLockCacheCheck()
    {
        var tcsFirstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcsUnblock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _innerStore.GetTenantAsync(TestId, Arg.Any<System.Threading.CancellationToken>())
            .Returns(async _ =>
            {
                tcsFirstStarted.SetResult(true);
                await tcsUnblock.Task;
                return Result<TenantInfo>.Success(_testTenant);
            });

        var task1 = Task.Run(() => _cachedStore.GetTenantAsync(TestId));
        await tcsFirstStarted.Task;

        var task2 = Task.Run(() => _cachedStore.GetTenantAsync(TestId));
        await Task.Delay(50);
        tcsUnblock.SetResult(true);

        var results = await Task.WhenAll(task1, task2);
        results[0].IsSuccess.Should().BeTrue();
        results[1].IsSuccess.Should().BeTrue();
        await _innerStore.Received(1).GetTenantAsync(TestId, Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_ConcurrentRequests_SecondCallerHitsLockCacheCheck()
    {
        var innerLookup = Substitute.For<ITenantStore<TenantInfo>, ITenantLookupStore<TenantInfo>>();
        var store = new CachedTenantStore<TenantInfo>(innerLookup, _cache, _options);

        var tcsFirstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcsUnblock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        ((ITenantLookupStore<TenantInfo>)innerLookup)
            .GetTenantByIdentifierAsync(TestIdentifier, Arg.Any<System.Threading.CancellationToken>())
            .Returns(async _ =>
            {
                tcsFirstStarted.SetResult(true);
                await tcsUnblock.Task;
                return Result<TenantInfo>.Success(_testTenant);
            });

        var task1 = Task.Run(() => store.GetTenantByIdentifierAsync(TestIdentifier));
        await tcsFirstStarted.Task;

        var task2 = Task.Run(() => store.GetTenantByIdentifierAsync(TestIdentifier));
        await Task.Delay(50);
        tcsUnblock.SetResult(true);

        var results = await Task.WhenAll(task1, task2);
        results[0].IsSuccess.Should().BeTrue();
        results[1].IsSuccess.Should().BeTrue();
        await ((ITenantLookupStore<TenantInfo>)innerLookup).Received(1).GetTenantByIdentifierAsync(TestIdentifier, Arg.Any<System.Threading.CancellationToken>());
    }
}



