// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class ConfigurationMultiTenancyBuilderExtensionsTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly TenantId AlphaTenantId = new(AlphaGuid);

    [Fact]
    public void AddConfigurationTenantStore_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddConfigurationTenantStore<TenantInfo>(opt => { });
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddConfigurationTenantStore_NullConfigureOptions_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddConfigurationTenantStore<TenantInfo>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configureOptions");
    }

    [Fact]
    public async Task AddConfigurationTenantStore_RegistersServicesAndConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        var returnedServices = services.AddConfigurationTenantStore<TenantInfo>(options =>
        {
            options.Tenants = new List<TenantInfo>
            {
                new(AlphaTenantId, "Alpha")
            };
        });

        returnedServices.Should().BeSameAs(services);

        var serviceProvider = services.BuildServiceProvider();

        var tenantStore = serviceProvider.GetService<ITenantStore<TenantInfo>>();
        tenantStore.Should().NotBeNull();
        tenantStore.Should().BeOfType<ConfigurationTenantStore<TenantInfo>>();

        var lookupStore = serviceProvider.GetService<ITenantLookupStore<TenantInfo>>();
        lookupStore.Should().NotBeNull();
        lookupStore.Should().BeOfType<ConfigurationTenantStore<TenantInfo>>();

        // Verify the store can resolve the configured tenant
        var result = await tenantStore!.GetTenantAsync(AlphaTenantId);
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(AlphaTenantId);
        result.Value.Name.Should().Be("Alpha");
    }
}
