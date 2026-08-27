// Copyright © Erickson Lopez. MIT License.
using System;
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
}

