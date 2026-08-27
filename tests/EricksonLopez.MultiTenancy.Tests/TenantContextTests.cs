// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class TenantContextTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public void Empty_IsNotResolved()
    {
        var context = TenantContext.Empty;
        context.IsResolved.Should().BeFalse();
        context.Tenant.Should().BeNull();
        context.Source.Should().Be(TenantResolutionSource.None);
    }

    [Fact]
    public void Create_WithActiveTenant_IsResolved()
    {
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "Tenant One", isActive: true);
        var context = TenantContext.Create(tenant);

        context.IsResolved.Should().BeTrue();
        context.Tenant.Should().BeSameAs(tenant);
    }

    [Fact]
    public void Create_WithInactiveTenant_IsNotResolved()
    {
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "Inactive", isActive: false);
        var context = TenantContext.Create(tenant);

        context.IsResolved.Should().BeFalse();
        context.Tenant.Should().BeSameAs(tenant);
    }

    [Fact]
    public void Create_WithEmptyTenantId_IsNotResolved()
    {
        var tenant = new TenantInfo(TenantId.Empty, "Empty Id", isActive: true);
        var context = TenantContext.Create(tenant);

        context.IsResolved.Should().BeFalse();
        context.Tenant.Should().BeSameAs(tenant);
    }

    [Fact]
    public void Create_WithSource_PropagatesSource()
    {
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "T1");
        var context = TenantContext.Create(tenant, TenantResolutionSource.JwtClaim);

        context.Source.Should().Be(TenantResolutionSource.JwtClaim);
    }

    [Fact]
    public void TypedTenantContext_Empty_PropertiesCorrect()
    {
        var context = TenantContext<TenantInfo>.Empty;
        context.IsResolved.Should().BeFalse();
        context.Tenant.Should().BeNull();
        ((ITenantContext)context).Tenant.Should().BeNull();
        context.Source.Should().Be(TenantResolutionSource.None);
    }

    [Fact]
    public void TypedTenantContext_WithActiveTenant_PropertiesCorrect()
    {
        var tenant = new TenantInfo(new TenantId(AlphaGuid), "Tenant One", isActive: true);
        var context = new TenantContext<TenantInfo>(tenant, TenantResolutionSource.Header);

        context.IsResolved.Should().BeTrue();
        context.Tenant.Should().BeSameAs(tenant);
        ((ITenantContext)context).Tenant.Should().BeSameAs(tenant);
        context.Source.Should().Be(TenantResolutionSource.Header);
    }

    [Fact]
    public void TypedTenantContext_WithNullTenant_SourceIsNone()
    {
        var context = new TenantContext<TenantInfo>(null, TenantResolutionSource.JwtClaim);

        context.IsResolved.Should().BeFalse();
        context.Tenant.Should().BeNull();
        ((ITenantContext)context).Tenant.Should().BeNull();
        context.Source.Should().Be(TenantResolutionSource.None);
    }
}

