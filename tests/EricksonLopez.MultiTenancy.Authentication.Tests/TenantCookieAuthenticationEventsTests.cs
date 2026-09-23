// Copyright © Erickson Lopez. MIT License.
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using AwesomeAssertions;

namespace EricksonLopez.MultiTenancy.Authentication.UnitTests;

public class TenantCookieAuthenticationEventsTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly TenantId AlphaTenantId = new(AlphaGuid);
    private static readonly Guid BetaGuid = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly TenantId BetaTenantId = new(BetaGuid);

    [Fact]
    public void Constructor_NullClaimType_ThrowsArgumentNullException()
    {
        var act = () => new TenantCookieAuthenticationEvents<TenantInfo>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tenantClaimType");
    }

    [Fact]
    public void Constructor_Default_SetsTenantIdClaimType()
    {
        var events = new TenantCookieAuthenticationEvents<TenantInfo>();
        events.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidatePrincipal_NullContext_ThrowsArgumentNullException()
    {
        var events = new TenantCookieAuthenticationEvents<TenantInfo>();
        var act = () => events.ValidatePrincipal(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task ValidatePrincipal_NoActiveTenant_DoesNotRejectPrincipal()
    {
        // Arrange
        var accessor = Substitute.For<ITenantContextAccessor>();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.Tenant.Returns((ITenantInfo?)null);
        accessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var principal = CreatePrincipalWithTenantClaim("tenant_id", AlphaGuid.ToString());
        var context = CreateCookieContext(httpContext, principal);

        var events = new TenantCookieAuthenticationEvents<TenantInfo>();

        // Act
        await events.ValidatePrincipal(context);

        // Assert
        context.Principal.Should().NotBeNull();
        context.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePrincipal_NullTenantContext_DoesNotRejectPrincipal()
    {
        // Arrange
        var accessor = Substitute.For<ITenantContextAccessor>();
        accessor.TenantContext.Returns((ITenantContext?)null);

        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var principal = CreatePrincipalWithTenantClaim("tenant_id", AlphaGuid.ToString());
        var context = CreateCookieContext(httpContext, principal);

        var events = new TenantCookieAuthenticationEvents<TenantInfo>();

        // Act
        await events.ValidatePrincipal(context);

        // Assert
        context.Principal.Should().NotBeNull();
        context.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePrincipal_NullPrincipal_DoesNotReject()
    {
        // Arrange
        var tenant = new TenantInfo(AlphaTenantId, "Alpha");
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.Tenant.Returns(tenant);

        var accessor = Substitute.For<ITenantContextAccessor>();
        accessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var context = CreateCookieContext(httpContext, principal: null);

        var events = new TenantCookieAuthenticationEvents<TenantInfo>();

        // Act
        await events.ValidatePrincipal(context);

        // Assert
        context.Principal.Should().BeNull();
    }

    [Fact]
    public async Task ValidatePrincipal_PrincipalWithoutTenantClaim_RejectsPrincipalByDefault()
    {
        // Arrange
        var tenant = new TenantInfo(AlphaTenantId, "Alpha");
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.Tenant.Returns(tenant);

        var accessor = Substitute.For<ITenantContextAccessor>();
        accessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Alice") }, "CookieAuth");
        var principal = new ClaimsPrincipal(identity);
        var context = CreateCookieContext(httpContext, principal);

        var events = new TenantCookieAuthenticationEvents<TenantInfo>();

        // Act
        await events.ValidatePrincipal(context);

        // Assert: By default, fail-closed enforces rejection when tenant claim is missing
        context.Principal.Should().BeNull();
    }

    [Fact]
    public async Task ValidatePrincipal_PrincipalWithoutTenantClaim_WhenRequireClaimFalse_DoesNotReject()
    {
        // Arrange
        var tenant = new TenantInfo(AlphaTenantId, "Alpha");
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.Tenant.Returns(tenant);

        var accessor = Substitute.For<ITenantContextAccessor>();
        accessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Alice") }, "CookieAuth");
        var principal = new ClaimsPrincipal(identity);
        var context = CreateCookieContext(httpContext, principal);

        var events = new TenantCookieAuthenticationEvents<TenantInfo>("tenant_id", requireTenantClaim: false);

        // Act
        await events.ValidatePrincipal(context);

        // Assert
        context.Principal.Should().NotBeNull();
        context.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePrincipal_MatchingTenantClaim_DoesNotReject()
    {
        // Arrange
        var tenant = new TenantInfo(AlphaTenantId, "Alpha");
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.Tenant.Returns(tenant);

        var accessor = Substitute.For<ITenantContextAccessor>();
        accessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var principal = CreatePrincipalWithTenantClaim("tenant_id", AlphaGuid.ToString().ToUpperInvariant());
        var context = CreateCookieContext(httpContext, principal);

        var events = new TenantCookieAuthenticationEvents<TenantInfo>();

        // Act
        await events.ValidatePrincipal(context);

        // Assert
        context.Principal.Should().NotBeNull();
        context.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePrincipal_MismatchedTenantClaim_RejectsPrincipal()
    {
        // Arrange
        var tenant = new TenantInfo(AlphaTenantId, "Alpha");
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.Tenant.Returns(tenant);

        var accessor = Substitute.For<ITenantContextAccessor>();
        accessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var principal = CreatePrincipalWithTenantClaim("tenant_id", BetaGuid.ToString());
        var context = CreateCookieContext(httpContext, principal);

        var events = new TenantCookieAuthenticationEvents<TenantInfo>();

        // Act
        await events.ValidatePrincipal(context);

        // Assert
        context.Principal.Should().BeNull(); // RejectPrincipal sets Principal to null
    }

    [Fact]
    public async Task ValidatePrincipal_CustomTenantClaimType_Matching_DoesNotReject()
    {
        // Arrange
        var tenant = new TenantInfo(AlphaTenantId, "Alpha");
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.Tenant.Returns(tenant);

        var accessor = Substitute.For<ITenantContextAccessor>();
        accessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var principal = CreatePrincipalWithTenantClaim("my_org_claim", AlphaGuid.ToString());
        var context = CreateCookieContext(httpContext, principal);

        var events = new TenantCookieAuthenticationEvents<TenantInfo>("my_org_claim");

        // Act
        await events.ValidatePrincipal(context);

        // Assert
        context.Principal.Should().NotBeNull();
        context.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePrincipal_CustomTenantClaimType_Mismatched_RejectsPrincipal()
    {
        // Arrange
        var tenant = new TenantInfo(AlphaTenantId, "Alpha");
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.Tenant.Returns(tenant);

        var accessor = Substitute.For<ITenantContextAccessor>();
        accessor.TenantContext.Returns(tenantContext);

        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var principal = CreatePrincipalWithTenantClaim("my_org_claim", BetaGuid.ToString());
        var context = CreateCookieContext(httpContext, principal);

        var events = new TenantCookieAuthenticationEvents<TenantInfo>("my_org_claim");

        // Act
        await events.ValidatePrincipal(context);

        // Assert
        context.Principal.Should().BeNull();
    }

    private static ClaimsPrincipal CreatePrincipalWithTenantClaim(string claimType, string claimValue)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "Alice"),
            new Claim(claimType, claimValue)
        }, "CookieAuth");
        return new ClaimsPrincipal(identity);
    }

    private static CookieValidatePrincipalContext CreateCookieContext(HttpContext httpContext, ClaimsPrincipal? principal)
    {
        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var options = new CookieAuthenticationOptions();
        var ticket = new AuthenticationTicket(principal ?? new ClaimsPrincipal(), new AuthenticationProperties(), CookieAuthenticationDefaults.AuthenticationScheme);
        var context = new CookieValidatePrincipalContext(httpContext, scheme, options, ticket);
        if (principal == null)
        {
            context.Principal = null;
        }
        return context;
    }
}
