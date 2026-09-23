// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.MultiTenancy.Abstractions.Tests;

public sealed class OrganizationContextTests
{
    private sealed class FakeOrganizationContext : IOrganizationContext
    {
        public ITenantInfo? Tenant { get; set; }
        public bool IsResolved => Tenant is not null && Tenant.Id.Value != Guid.Empty && Tenant.IsActive;
        public TenantResolutionSource Source => TenantResolutionSource.Header;
        public Guid? CompanyId { get; set; }
        public Guid? BranchId { get; set; }
        public IReadOnlyList<Guid> AllowedBranchIds { get; set; } = [];
        public bool AllBranchesAllowed { get; set; }
    }

    [Fact]
    public void HasCompanyContext_ShouldReturnTrue_WhenCompanyIdIsNotEmpty()
    {
        ICompanyContext context = new FakeOrganizationContext { CompanyId = Guid.NewGuid() };
        context.HasCompanyContext.Should().BeTrue();
    }

    [Fact]
    public void HasCompanyContext_ShouldReturnFalse_WhenCompanyIdIsNull()
    {
        ICompanyContext context = new FakeOrganizationContext { CompanyId = null };
        context.HasCompanyContext.Should().BeFalse();
    }

    [Fact]
    public void HasCompanyContext_ShouldReturnFalse_WhenCompanyIdIsEmpty()
    {
        ICompanyContext context = new FakeOrganizationContext { CompanyId = Guid.Empty };
        context.HasCompanyContext.Should().BeFalse();
    }

    [Fact]
    public void OrganizationContext_ShouldHoldHierarchyValues()
    {
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var allowedBranches = new List<Guid> { branchId, Guid.NewGuid() };

        var context = new FakeOrganizationContext
        {
            CompanyId = companyId,
            BranchId = branchId,
            AllowedBranchIds = allowedBranches,
            AllBranchesAllowed = false
        };

        context.CompanyId.Should().Be(companyId);
        context.BranchId.Should().Be(branchId);
        context.AllowedBranchIds.Should().HaveCount(2);
        context.AllBranchesAllowed.Should().BeFalse();
    }
}
