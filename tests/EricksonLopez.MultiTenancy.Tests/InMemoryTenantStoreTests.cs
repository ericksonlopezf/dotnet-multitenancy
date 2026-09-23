// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Result;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class InMemoryTenantStoreTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid BetaGuid = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    [Fact]
    public void Constructor_NullTenants_ThrowsArgumentNullException()
    {
        var act = () => new InMemoryTenantStore<TenantInfo>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tenants");
    }

    [Fact]
    public void NonGenericConstructor_NullTenants_ThrowsArgumentNullException()
    {
        var act = () => new InMemoryTenantStore(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tenants");
    }

    [Fact]
    public void Constructor_WithMaxCapacity_NullTenants_ThrowsArgumentNullException()
    {
        var act1 = () => new InMemoryTenantStore<TenantInfo>(null!, 10);
        act1.Should().Throw<ArgumentNullException>().WithParameterName("tenants");

        var act2 = () => new InMemoryTenantStore(null!, 10);
        act2.Should().Throw<ArgumentNullException>().WithParameterName("tenants");
    }

    [Fact]
    public void TryAdd_NullTenant_ThrowsArgumentNullException()
    {
        var store = new InMemoryTenantStore<TenantInfo>();
        var act = () => store.TryAdd(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tenant");
    }

    [Fact]
    public async Task Constructor_WithInitialTenants_PopulatesStore()
    {
        var tenants = new[]
        {
            new TenantInfo(new TenantId(AlphaGuid), "Alpha"),
            new TenantInfo(new TenantId(BetaGuid), "Beta")
        };

        var store = new InMemoryTenantStore(tenants);

        var resultAlpha = await store.GetTenantAsync(new TenantId(AlphaGuid));
        resultAlpha.IsSuccess.Should().BeTrue();
        resultAlpha.Value.Name.Should().Be("Alpha");

        var resultBeta = await store.GetTenantAsync(new TenantId(BetaGuid));
        resultBeta.IsSuccess.Should().BeTrue();
        resultBeta.Value.Name.Should().Be("Beta");
    }

    [Fact]
    public void AddOrUpdate_NullTenant_ThrowsArgumentNullException()
    {
        var store = new InMemoryTenantStore();
        var act = () => store.AddOrUpdate(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tenant");
    }

    [Fact]
    public async Task AddOrUpdate_ReplacesExistingTenant()
    {
        var store = new InMemoryTenantStore();
        var id = new TenantId(AlphaGuid);

        store.AddOrUpdate(new TenantInfo(id, "Original"));
        var original = await store.GetTenantAsync(id);
        original.IsSuccess.Should().BeTrue();
        original.Value.Name.Should().Be("Original");

        store.AddOrUpdate(new TenantInfo(id, "Updated"));
        var updated = await store.GetTenantAsync(id);
        updated.IsSuccess.Should().BeTrue();
        updated.Value.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task GetTenantAsync_NonExistentTenant_ReturnsFailureNotFound()
    {
        var store = new InMemoryTenantStore();
        var result = await store.GetTenantAsync(new TenantId(AlphaGuid));
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task GetTenantAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var store = new InMemoryTenantStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => store.GetTenantAsync(new TenantId(AlphaGuid), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ITenantStoreGetTenantAsync_ExplicitInterface_ReturnsTenantResult()
    {
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "Alpha");
        var store = new InMemoryTenantStore(new[] { tenant });

        ITenantStore interfaceStore = store;
        var result = await interfaceStore.GetTenantAsync(new TenantId(AlphaGuid));

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Value.Should().Be(AlphaGuid);

        var failResult = await interfaceStore.GetTenantAsync(new TenantId(BetaGuid));
        failResult.IsFailure.Should().BeTrue();
        failResult.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_FindsByNameCaseInsensitive()
    {
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "AlphaCorp");
        var store = new InMemoryTenantStore(new[] { tenant });

        var result = await store.GetTenantByIdentifierAsync("alphacorp");
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("AlphaCorp");
        result.Value.Id.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_FindsByGuidString()
    {
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "AlphaCorp");
        var store = new InMemoryTenantStore(new[] { tenant });

        var result = await store.GetTenantByIdentifierAsync(AlphaGuid.ToString("D"));
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("AlphaCorp");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_NonExistent_ReturnsFailureNotFound()
    {
        var store = new InMemoryTenantStore();

        var result = await store.GetTenantByIdentifierAsync("non-existent");
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
        result.Error.Description.Should().Contain("non-existent");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var store = new InMemoryTenantStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => store.GetTenantByIdentifierAsync("alpha", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ITenantLookupStore_ExplicitInterface_ReturnsExpectedResults()
    {
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "AlphaCorp");
        var store = new InMemoryTenantStore(new[] { tenant });
        ITenantLookupStore lookupStore = store;

        var successResult = await lookupStore.GetTenantByIdentifierAsync("AlphaCorp");
        successResult.IsSuccess.Should().BeTrue();
        successResult.Value.Name.Should().Be("AlphaCorp");

        var failResult = await lookupStore.GetTenantByIdentifierAsync("missing");
        failResult.IsFailure.Should().BeTrue();
        failResult.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_InvalidCapacity_ThrowsArgumentOutOfRangeException(int capacity)
    {
        var act = () => new InMemoryTenantStore(capacity);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Maximum capacity must be greater than zero.*");
    }

    [Fact]
    public void AddOrUpdate_ExceedingMaxCapacity_ThrowsInvalidOperationException()
    {
        var store = new InMemoryTenantStore(maxCapacity: 2);
        store.AddOrUpdate(new TenantInfo(new TenantId(Guid.NewGuid()), "Tenant1"));
        store.AddOrUpdate(new TenantInfo(new TenantId(Guid.NewGuid()), "Tenant2"));

        var act = () => store.AddOrUpdate(new TenantInfo(new TenantId(Guid.NewGuid()), "Tenant3"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*capacity*exceeded*");
    }

    [Fact]
    public void AddOrUpdate_ExistingTenant_DoesNotExceedCapacity()
    {
        var store = new InMemoryTenantStore(maxCapacity: 2);
        var tenant1 = new TenantInfo(new TenantId(Guid.NewGuid()), "Tenant1");
        store.AddOrUpdate(tenant1);
        store.AddOrUpdate(new TenantInfo(new TenantId(Guid.NewGuid()), "Tenant2"));

        // Updating existing should not throw even if at capacity
        var act = () => store.AddOrUpdate(tenant1);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetAllStreamAsync_YieldsAllTenants()
    {
        var tenants = new[]
        {
            new TenantInfo(new TenantId(AlphaGuid), "Alpha"),
            new TenantInfo(new TenantId(BetaGuid), "Beta")
        };

        var store = new InMemoryTenantStore(tenants);

        var list = new System.Collections.Generic.List<TenantInfo>();
        await foreach (var tenant in store.GetAllStreamAsync())
        {
            list.Add(tenant);
        }

        list.Should().HaveCount(2);
        list.Select(t => t.Name).Should().Contain(new[] { "Alpha", "Beta" });
    }

    [Fact]
    public async Task GetAllStreamAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var tenants = new[]
        {
            new TenantInfo(new TenantId(AlphaGuid), "Alpha"),
            new TenantInfo(new TenantId(BetaGuid), "Beta")
        };
        var store = new InMemoryTenantStore(tenants);

        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();

        var act = async () =>
        {
            await foreach (var _ in store.GetAllStreamAsync(cts.Token))
            {
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetAllStreamAsync_ViaInterface_YieldsAllTenants()
    {
        var tenants = new[]
        {
            new TenantInfo(new TenantId(AlphaGuid), "Alpha"),
            new TenantInfo(new TenantId(BetaGuid), "Beta")
        };

        ITenantStore store = new InMemoryTenantStore(tenants);

        var list = new System.Collections.Generic.List<ITenantInfo>();
        await foreach (var tenant in store.GetAllStreamAsync())
        {
            list.Add(tenant);
        }

        list.Should().HaveCount(2);
        list.Select(t => t.Name).Should().Contain(new[] { "Alpha", "Beta" });
    }

    [Fact]
    public async Task Constructor_WithTenantsAndMaxCapacity_PopulatesStoreAndEnforcesCapacity()
    {
        var tenants = new[]
        {
            new TenantInfo(new TenantId(AlphaGuid), "Alpha")
        };

        var genericStore = new InMemoryTenantStore<TenantInfo>(tenants, 2);
        var resultGeneric = await genericStore.GetTenantAsync(new TenantId(AlphaGuid));
        resultGeneric.IsSuccess.Should().BeTrue();

        var nonGenericStore = new InMemoryTenantStore(tenants, 2);
        var resultNonGeneric = await nonGenericStore.GetTenantAsync(new TenantId(AlphaGuid));
        resultNonGeneric.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TryAdd_BehavesCorrectlyRespectingCapacity()
    {
        var store = new InMemoryTenantStore(maxCapacity: 1);
        var t1 = new TenantInfo(new TenantId(AlphaGuid), "Alpha");
        var t2 = new TenantInfo(new TenantId(BetaGuid), "Beta");

        store.TryAdd(t1).Should().BeTrue();
        store.TryAdd(t1).Should().BeFalse();
        store.TryAdd(t2).Should().BeFalse();
    }

    [Fact]
    public void TryRemove_RemovesTenantCorrectly()
    {
        var store = new InMemoryTenantStore();
        var id = new TenantId(AlphaGuid);
        var t1 = new TenantInfo(id, "Alpha");

        store.AddOrUpdate(t1);
        store.TryRemove(id).Should().BeTrue();
        store.TryRemove(id).Should().BeFalse();
    }

    [Fact]
    public async Task Clear_EmptiesAllTenants()
    {
        var store = new InMemoryTenantStore();
        var id = new TenantId(AlphaGuid);
        store.AddOrUpdate(new TenantInfo(id, "Alpha"));

        store.Clear();

        var result = await store.GetTenantAsync(id);
        result.IsFailure.Should().BeTrue();
    }
}


