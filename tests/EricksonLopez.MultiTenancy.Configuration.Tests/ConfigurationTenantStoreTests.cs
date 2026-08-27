// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class ConfigurationTenantStoreTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly TenantId AlphaTenantId = new(AlphaGuid);
    private static readonly Guid BetaGuid = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly TenantId BetaTenantId = new(BetaGuid);
    private static readonly Guid MissingGuid = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly TenantId MissingTenantId = new(MissingGuid);

    [Fact]
    public void MultiTenancyOptions_DefaultConstructor_HasEmptyTenantsList()
    {
        var options = new MultiTenancyOptions<TenantInfo>();
        options.Tenants.Should().NotBeNull();
        options.Tenants.Should().BeEmpty();

        var customList = new List<TenantInfo> { new(AlphaTenantId, "Alpha") };
        options.Tenants = customList;
        options.Tenants.Should().BeSameAs(customList);
    }

    [Fact]
    public void Constructor_NullOptionsMonitor_ThrowsArgumentNullException()
    {
        var act = () => new ConfigurationTenantStore<TenantInfo>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("optionsMonitor");
    }

    [Fact]
    public async Task GetTenantAsync_ExistingTenantId_ReturnsSuccess()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<MultiTenancyOptions<TenantInfo>>>();
        optionsMonitor.CurrentValue.Returns(new MultiTenancyOptions<TenantInfo>
        {
            Tenants = new List<TenantInfo>
            {
                new(AlphaTenantId, "Alpha"),
                new(BetaTenantId, "Beta")
            }
        });

        var store = new ConfigurationTenantStore<TenantInfo>(optionsMonitor);
        var result = await store.GetTenantAsync(AlphaTenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(AlphaTenantId);
        result.Value.Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task GetTenantAsync_NonExistingTenantId_ReturnsFailure()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<MultiTenancyOptions<TenantInfo>>>();
        optionsMonitor.CurrentValue.Returns(new MultiTenancyOptions<TenantInfo>
        {
            Tenants = new List<TenantInfo>
            {
                new(AlphaTenantId, "Alpha")
            }
        });

        var store = new ConfigurationTenantStore<TenantInfo>(optionsMonitor);
        var result = await store.GetTenantAsync(MissingTenantId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(TenantErrors.NotFound(MissingTenantId).Code);
        result.Error.Description.Should().Be(TenantErrors.NotFound(MissingTenantId).Description);
    }

    [Fact]
    public async Task GetTenantAsync_NullTenantsList_ReturnsFailure()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<MultiTenancyOptions<TenantInfo>>>();
        optionsMonitor.CurrentValue.Returns(new MultiTenancyOptions<TenantInfo>
        {
            Tenants = null!
        });

        var store = new ConfigurationTenantStore<TenantInfo>(optionsMonitor);
        var result = await store.GetTenantAsync(AlphaTenantId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(TenantErrors.NotFound(AlphaTenantId).Code);
    }

    [Fact]
    public async Task ExplicitITenantStore_GetTenantAsync_SuccessAndFailure()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<MultiTenancyOptions<TenantInfo>>>();
        optionsMonitor.CurrentValue.Returns(new MultiTenancyOptions<TenantInfo>
        {
            Tenants = new List<TenantInfo>
            {
                new(AlphaTenantId, "Alpha")
            }
        });

        ITenantStore store = new ConfigurationTenantStore<TenantInfo>(optionsMonitor);

        // Success
        var successResult = await store.GetTenantAsync(AlphaTenantId, CancellationToken.None);
        successResult.IsSuccess.Should().BeTrue();
        successResult.Value.Id.Should().Be(AlphaTenantId);
        successResult.Value.Name.Should().Be("Alpha");

        // Failure
        var failureResult = await store.GetTenantAsync(MissingTenantId, CancellationToken.None);
        failureResult.IsSuccess.Should().BeFalse();
        failureResult.Error.Code.Should().Be(TenantErrors.NotFound(MissingTenantId).Code);
        failureResult.Error.Description.Should().Be(TenantErrors.NotFound(MissingTenantId).Description);
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_ExistingTenantName_CaseInsensitive_ReturnsSuccess()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<MultiTenancyOptions<TenantInfo>>>();
        optionsMonitor.CurrentValue.Returns(new MultiTenancyOptions<TenantInfo>
        {
            Tenants = new List<TenantInfo>
            {
                new(AlphaTenantId, "Alpha")
            }
        });

        var store = new ConfigurationTenantStore<TenantInfo>(optionsMonitor);
        var result = await store.GetTenantByIdentifierAsync("ALPHA");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(AlphaTenantId);
        result.Value.Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_ValidGuidString_MatchesTenantId_ReturnsSuccess()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<MultiTenancyOptions<TenantInfo>>>();
        optionsMonitor.CurrentValue.Returns(new MultiTenancyOptions<TenantInfo>
        {
            Tenants = new List<TenantInfo>
            {
                new(BetaTenantId, "Beta")
            }
        });

        var store = new ConfigurationTenantStore<TenantInfo>(optionsMonitor);
        var result = await store.GetTenantByIdentifierAsync(BetaGuid.ToString());

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(BetaTenantId);
        result.Value.Name.Should().Be("Beta");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_ValidGuidString_NonExistent_ReturnsFailure()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<MultiTenancyOptions<TenantInfo>>>();
        optionsMonitor.CurrentValue.Returns(new MultiTenancyOptions<TenantInfo>
        {
            Tenants = new List<TenantInfo>
            {
                new(BetaTenantId, "Beta")
            }
        });

        var store = new ConfigurationTenantStore<TenantInfo>(optionsMonitor);
        var result = await store.GetTenantByIdentifierAsync(MissingGuid.ToString());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(TenantErrors.NotFound(MissingTenantId).Code);
        result.Error.Description.Should().Be(TenantErrors.NotFound(MissingTenantId).Description);
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_NonGuid_NonExistentName_ReturnsNotFoundFailure()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<MultiTenancyOptions<TenantInfo>>>();
        optionsMonitor.CurrentValue.Returns(new MultiTenancyOptions<TenantInfo>
        {
            Tenants = new List<TenantInfo>
            {
                new(AlphaTenantId, "Alpha")
            }
        });

        var store = new ConfigurationTenantStore<TenantInfo>(optionsMonitor);
        var result = await store.GetTenantByIdentifierAsync("non-existent-tenant");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Tenant.NotFound");
        result.Error.Description.Should().Be("Tenant with identifier 'non-existent-tenant' was not found in configuration.");
    }

    [Fact]
    public async Task GetTenantByIdentifierAsync_NullTenantsList_NonGuid_ReturnsNotFoundFailure()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<MultiTenancyOptions<TenantInfo>>>();
        optionsMonitor.CurrentValue.Returns(new MultiTenancyOptions<TenantInfo>
        {
            Tenants = null!
        });

        var store = new ConfigurationTenantStore<TenantInfo>(optionsMonitor);
        var result = await store.GetTenantByIdentifierAsync("non-existent-tenant");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Tenant.NotFound");
        result.Error.Description.Should().Be("Tenant with identifier 'non-existent-tenant' was not found in configuration.");
    }

    [Fact]
    public async Task ExplicitITenantLookupStore_GetTenantByIdentifierAsync_SuccessAndFailure()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<MultiTenancyOptions<TenantInfo>>>();
        optionsMonitor.CurrentValue.Returns(new MultiTenancyOptions<TenantInfo>
        {
            Tenants = new List<TenantInfo>
            {
                new(AlphaTenantId, "Alpha")
            }
        });

        ITenantLookupStore store = new ConfigurationTenantStore<TenantInfo>(optionsMonitor);

        // Success by Name
        var successResult = await store.GetTenantByIdentifierAsync("Alpha", CancellationToken.None);
        successResult.IsSuccess.Should().BeTrue();
        successResult.Value.Id.Should().Be(AlphaTenantId);
        successResult.Value.Name.Should().Be("Alpha");

        // Failure
        var failureResult = await store.GetTenantByIdentifierAsync("Unknown", CancellationToken.None);
        failureResult.IsSuccess.Should().BeFalse();
        failureResult.Error.Code.Should().Be("Tenant.NotFound");
        failureResult.Error.Description.Should().Be("Tenant with identifier 'Unknown' was not found in configuration.");
    }
}
