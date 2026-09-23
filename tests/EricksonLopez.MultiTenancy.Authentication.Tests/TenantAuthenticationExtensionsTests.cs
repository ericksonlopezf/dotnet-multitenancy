// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using EricksonLopez.MultiTenancy.AspNetCore.Options;
using EricksonLopez.MultiTenancy.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using AwesomeAssertions;
using NSubstitute;

namespace EricksonLopez.MultiTenancy.Authentication.UnitTests;

public class TenantAuthenticationExtensionsTests
{
    private class DummyOptions { }

    [Fact]
    public void AddPerTenantAuthentication_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddPerTenantAuthentication<TenantInfo>();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddPerTenantAuthentication_RegistersCoreAndCookieCaches_AndReturnsServices()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        var returnedServices = services.AddPerTenantAuthentication<TenantInfo>();
        returnedServices.Should().BeSameAs(services);

        var authCache = services.FirstOrDefault(d => d.ServiceType == typeof(IOptionsMonitorCache<AuthenticationOptions>));
        authCache.Should().NotBeNull();
        authCache!.ImplementationType.Should().Be<TenantOptionsCache<AuthenticationOptions, TenantInfo>>();

        var cookieCache = services.FirstOrDefault(d => d.ServiceType == typeof(IOptionsMonitorCache<CookieAuthenticationOptions>));
        cookieCache.Should().NotBeNull();
        cookieCache!.ImplementationType.Should().Be<TenantOptionsCache<CookieAuthenticationOptions, TenantInfo>>();
    }

    [Fact]
    public void AddPerTenantAuthentication_WithOptions_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddPerTenantAuthentication<TenantInfo>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    [Fact]
    public void AddPerTenantAuthentication_WithOptions_RegistersServicesAndConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        var returnedServices = services.AddPerTenantAuthentication<TenantInfo>(options =>
        {
            options.DefaultSchemeSelector = tenant => tenant.Name == "Acme" ? "AcmeScheme" : "DefaultBearer";
        });

        returnedServices.Should().BeSameAs(services);

        var authCache = services.FirstOrDefault(d => d.ServiceType == typeof(IOptionsMonitorCache<AuthenticationOptions>));
        authCache.Should().NotBeNull();

        var cookieCache = services.FirstOrDefault(d => d.ServiceType == typeof(IOptionsMonitorCache<CookieAuthenticationOptions>));
        cookieCache.Should().NotBeNull();
    }

    [Fact]
    public void AddPerTenantAuthentication_WithOptions_NullSelector_FallsBackToDefaultRegistration()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        var returnedServices = services.AddPerTenantAuthentication<TenantInfo>(options =>
        {
            options.DefaultSchemeSelector = null;
        });

        returnedServices.Should().BeSameAs(services);

        var authCache = services.FirstOrDefault(d => d.ServiceType == typeof(IOptionsMonitorCache<AuthenticationOptions>));
        authCache.Should().NotBeNull();
    }

    [Fact]
    public void AddPerTenantAuthentication_WithOptions_ExecutesSchemeSelector_SetsDefaultScheme_WhenNonNull()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        var tenant = new TenantInfo(TenantId.NewId(), "Acme");
        var context = Substitute.For<ITenantContext>();
        context.Tenant.Returns(tenant);

        var accessor = Substitute.For<ITenantContextAccessor>();
        accessor.TenantContext.Returns(context);
        services.AddSingleton(accessor);

        services.AddPerTenantAuthentication<TenantInfo>(options =>
        {
            options.DefaultSchemeSelector = t => t.Name == "Acme" ? "AcmeScheme" : "FallbackScheme";
        });

        using var sp = services.BuildServiceProvider();
        var configure = sp.GetRequiredService<IConfigureOptions<AuthenticationOptions>>();
        var authOptions = new AuthenticationOptions();
        configure.Configure(authOptions);

        authOptions.DefaultScheme.Should().Be("AcmeScheme");
    }

    [Fact]
    public void AddPerTenantAuthentication_WithOptions_ExecutesSchemeSelector_LeavesDefaultScheme_WhenNullOrEmpty()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        var tenant = new TenantInfo(TenantId.NewId(), "Other");
        var context = Substitute.For<ITenantContext>();
        context.Tenant.Returns(tenant);

        var accessor = Substitute.For<ITenantContextAccessor>();
        accessor.TenantContext.Returns(context);
        services.AddSingleton(accessor);

        services.AddPerTenantAuthentication<TenantInfo>(options =>
        {
            options.DefaultSchemeSelector = _ => string.Empty;
        });

        using var sp = services.BuildServiceProvider();
        var configure = sp.GetRequiredService<IConfigureOptions<AuthenticationOptions>>();
        var authOptions = new AuthenticationOptions();
        configure.Configure(authOptions);

        authOptions.DefaultScheme.Should().BeNull();
    }
}
