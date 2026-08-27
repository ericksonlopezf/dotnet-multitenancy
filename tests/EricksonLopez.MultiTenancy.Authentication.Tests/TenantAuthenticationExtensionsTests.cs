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
}
