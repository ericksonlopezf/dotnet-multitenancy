// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;
using Opt = Microsoft.Extensions.Options.Options;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class HealthCheckTests
{
    // ─────────────────────────────────────────────────────────
    // MultiTenancyHealthCheck
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_NullTenantStore_ReturnsDegraded()
    {
        var healthCheck = new MultiTenancyHealthCheck(null);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Tenant store is not registered in the service provider.");
        result.Data["store_registered"].Should().Be(false);
        result.Data["store_type"].Should().Be("None");
        result.Data["supports_lookup"].Should().Be(false);
    }

    [Fact]
    public async Task CheckHealthAsync_CustomFailureStatus_ReturnsConfiguredStatus()
    {
        var options = Opt.Create(new MultiTenancyHealthCheckOptions
        {
            FailureStatus = HealthStatus.Unhealthy
        });
        var healthCheck = new MultiTenancyHealthCheck(null, options);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data["store_type"].Should().Be("None");
    }

    [Fact]
    public async Task CheckHealthAsync_WithTenantStore_ReturnsHealthyWithDiagnostics()
    {
        var store = new InMemoryTenantStore();
        var healthCheck = new MultiTenancyHealthCheck(store);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Multi-tenancy subsystem and tenant store are active and ready.");
        result.Data["store_registered"].Should().Be(true);
        result.Data["store_type"].Should().Be("InMemoryTenantStore");
        result.Data["supports_lookup"].Should().Be(true);
    }

    [Fact]
    public async Task CheckHealthAsync_ProbeSuccess_ReturnsHealthy()
    {
        var store = new InMemoryTenantStore();
        var options = Opt.Create(new MultiTenancyHealthCheckOptions
        {
            StoreProbe = (s, ct) => Task.FromResult(true)
        });
        var healthCheck = new MultiTenancyHealthCheck(store, options);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ProbeFailure_ReturnsDegraded()
    {
        var store = new InMemoryTenantStore();
        var options = Opt.Create(new MultiTenancyHealthCheckOptions
        {
            StoreProbe = (s, ct) => Task.FromResult(false)
        });
        var healthCheck = new MultiTenancyHealthCheck(store, options);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Tenant store probe check failed.");
    }

    [Fact]
    public async Task CheckHealthAsync_ProbeThrowsException_ReturnsUnhealthy()
    {
        var store = new InMemoryTenantStore();
        var options = Opt.Create(new MultiTenancyHealthCheckOptions
        {
            StoreProbe = (s, ct) => throw new InvalidOperationException("Store connection timed out.")
        });
        var healthCheck = new MultiTenancyHealthCheck(store, options);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Store connection timed out.");
    }

    // ─────────────────────────────────────────────────────────
    // MultiTenancyHealthCheckExtensions
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void AddMultiTenancyHealthCheck_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddMultiTenancyHealthCheck();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");

        var actWithConfig = () => services.AddMultiTenancyHealthCheck(opt => opt.FailureStatus = HealthStatus.Unhealthy);
        actWithConfig.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddMultiTenancyHealthCheck_RegistersHealthCheckAsTransient()
    {
        var services = new ServiceCollection();
        services.AddMultiTenancyHealthCheck(options =>
        {
            options.FailureStatus = HealthStatus.Unhealthy;
        });

        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IHealthCheck));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<MultiTenancyHealthCheck>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Transient);

        // Verify resolvable and options applied
        using var provider = services.BuildServiceProvider();
        var healthCheck = provider.GetRequiredService<IHealthCheck>();
        healthCheck.Should().BeOfType<MultiTenancyHealthCheck>();

        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MultiTenancyHealthCheckOptions>>().Value;
        options.FailureStatus.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void AddMultiTenancyHealthCheck_WithoutConfigure_RegistersSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddMultiTenancyHealthCheck();

        using var provider = services.BuildServiceProvider();
        var healthCheck = provider.GetRequiredService<IHealthCheck>();
        healthCheck.Should().BeOfType<MultiTenancyHealthCheck>();

        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<MultiTenancyHealthCheckOptions>>();
        options.Should().BeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_IncludeDiagnosticDataFalse_DataIsNull()
    {
        var options = Opt.Create(new MultiTenancyHealthCheckOptions
        {
            IncludeDiagnosticData = false
        });
        var healthCheck = new MultiTenancyHealthCheck(null, options);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);
        result.Data.Should().BeEmpty();

        var store = new InMemoryTenantStore();
        var healthCheckWithStore = new MultiTenancyHealthCheck(store, options);
        var resultWithStore = await healthCheckWithStore.CheckHealthAsync(context);
        resultWithStore.Data.Should().BeEmpty();
    }
}

