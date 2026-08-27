// Copyright © Erickson Lopez. MIT License.
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.AspNetCore.Strategies;
using EricksonLopez.Result;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class HttpResolutionStrategiesTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly string AlphaGuidStr = AlphaGuid.ToString("D");

    // ─────────────────────────────────────────────────────────
    // HeaderTenantResolutionStrategy
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void HeaderStrategy_NullAccessor_ThrowsArgumentNullException()
    {
        var act = () => new HeaderTenantResolutionStrategy(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpContextAccessor");
    }

    [Fact]
    public async Task HeaderStrategy_ExtractsHeaderSuccessfully()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-ID"] = AlphaGuidStr;
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new HeaderTenantResolutionStrategy(httpContextAccessor);
        strategy.StrategyName.Should().Be("Header");

        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public async Task HeaderStrategy_MissingHeader_ReturnsFailure()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(new DefaultHttpContext());

        var strategy = new HeaderTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ResolutionFailed");
        result.Error.Description.Should().Be("Resolution strategy 'Header' failed: Header 'X-Tenant-ID' is missing.");
    }

    [Fact]
    public async Task HeaderStrategy_NullHttpContext_ReturnsFailure()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext)null!);

        var strategy = new HeaderTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HeaderStrategy_InvalidGuid_ReturnsFailureInvalidId()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-ID"] = "invalid-guid-here";
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new HeaderTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
    }

    [Fact]
    public async Task HeaderStrategy_CustomHeaderName_Works()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Custom-Header"] = AlphaGuidStr;
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new HeaderTenantResolutionStrategy(httpContextAccessor, "Custom-Header");
        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public async Task HeaderStrategy_NullCustomHeaderName_FallsBackToDefault()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-ID"] = AlphaGuidStr;
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new HeaderTenantResolutionStrategy(httpContextAccessor, "   ");
        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }


    // ─────────────────────────────────────────────────────────
    // ClaimTenantResolutionStrategy
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void ClaimStrategy_NullAccessor_ThrowsArgumentNullException()
    {
        var act = () => new ClaimTenantResolutionStrategy(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpContextAccessor");
    }

    [Fact]
    public async Task ClaimStrategy_ExtractsPrimaryClaimSuccessfully()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();

        var identity = new ClaimsIdentity(new[] { new Claim("tenant_id", AlphaGuidStr) }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        strategy.StrategyName.Should().Be("Claims");

        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public async Task ClaimStrategy_ExtractsFallbackTidClaimSuccessfully()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();

        var identity = new ClaimsIdentity(new[] { new Claim("tid", AlphaGuidStr) }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public async Task ClaimStrategy_ExtractsFallbackTenantClaimSuccessfully()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();

        var identity = new ClaimsIdentity(new[] { new Claim("tenant", AlphaGuidStr) }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public async Task ClaimStrategy_CustomClaimName_Works()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();

        var identity = new ClaimsIdentity(new[] { new Claim("my_custom_tenant_claim", AlphaGuidStr) }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor, "my_custom_tenant_claim");
        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public async Task ClaimStrategy_CustomClaimNameNull_FallsBackToDefault()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();

        var identity = new ClaimsIdentity(new[] { new Claim("tenant_id", AlphaGuidStr) }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor, "   ");
        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public async Task ClaimStrategy_UserWithNullIdentity_ReturnsFailure()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        var principal = Substitute.For<ClaimsPrincipal>();
        principal.Identity.Returns((System.Security.Principal.IIdentity?)null);
        httpContext.User = principal;
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ResolutionFailed");
        result.Error.Description.Should().Be("Resolution strategy 'Claims' failed: User is not authenticated or tenant claim is missing.");
    }

    [Fact]
    public async Task ClaimStrategy_NotAuthenticated_ReturnsFailure()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();

        var identity = new ClaimsIdentity(new[] { new Claim("tenant_id", AlphaGuidStr) }); // Unauthenticated
        httpContext.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Resolution strategy 'Claims' failed: User is not authenticated or tenant claim is missing.");
    }

    [Fact]
    public async Task ClaimStrategy_NullUser_ReturnsFailure()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.User = null!;
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimStrategy_NullHttpContext_ReturnsFailure()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext)null!);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimStrategy_MissingClaims_ReturnsFailure()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();

        var identity = new ClaimsIdentity("TestAuth"); // Authenticated but empty
        httpContext.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Resolution strategy 'Claims' failed: User is not authenticated or tenant claim is missing.");
    }

    [Fact]
    public async Task ClaimStrategy_InvalidGuid_ReturnsFailureInvalidId()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();

        var identity = new ClaimsIdentity(new[] { new Claim("tenant_id", "invalid-guid") }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
    }


    [Fact]
    public async Task ClaimStrategy_Precedence_PrimaryOverFallbackTid()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();

        var identity = new ClaimsIdentity(new[]
        {
            new Claim("tenant_id", AlphaGuidStr),
            new Claim("tid", Guid.NewGuid().ToString("D"))
        }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public async Task ClaimStrategy_Precedence_TidOverFallbackTenant()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();

        var identity = new ClaimsIdentity(new[]
        {
            new Claim("tid", AlphaGuidStr),
            new Claim("tenant", Guid.NewGuid().ToString("D"))
        }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = new ClaimTenantResolutionStrategy(httpContextAccessor);
        var result = await strategy.ResolveTenantIdAsync();
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }
}
