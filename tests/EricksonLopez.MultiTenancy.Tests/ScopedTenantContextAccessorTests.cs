// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class ScopedTenantContextAccessorTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid BetaGuid = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    [Fact]
    public void TenantContext_StartsAsNull()
    {
        var accessor = new ScopedTenantContextAccessor();
        accessor.TenantContext.Should().BeNull();
    }

    [Fact]
    public void TenantContext_CanBeSetOnce()
    {
        var accessor = new ScopedTenantContextAccessor();
        var tenantA = new TenantInfo(new TenantId(AlphaGuid), "Tenant A");
        var context = TenantContext.Create(tenantA);

        accessor.TenantContext = context;

        accessor.TenantContext.Should().BeSameAs(context);
        accessor.TenantContext!.RequiredTenant.Id.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public void TenantContext_CannotBeReassigned_PreventsTenantSwitching()
    {
        var accessor = new ScopedTenantContextAccessor();
        var tenantA = new TenantInfo(new TenantId(AlphaGuid), "Tenant A");
        var tenantB = new TenantInfo(new TenantId(BetaGuid), "Tenant B");

        accessor.TenantContext = TenantContext.Create(tenantA);

        var act = () => accessor.TenantContext = TenantContext.Create(tenantB);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("The tenant context has already been set for this scope. ITenantContext is immutable after assignment. Each scope may only have one tenant context.");

        accessor.TenantContext!.RequiredTenant.Id.Value.Should().Be(AlphaGuid);
    }
}
