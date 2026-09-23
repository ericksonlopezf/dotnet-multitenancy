// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Dapper;
using Xunit;

namespace EricksonLopez.MultiTenancy.Dapper.Tests;

public sealed class OrganizationDapperExtensionsTests
{
    private sealed class FakeOrgContext : IOrganizationContext
    {
        public ITenantInfo? Tenant { get; set; }
        public bool IsResolved => Tenant is not null && Tenant.Id.Value != Guid.Empty && Tenant.IsActive;
        public TenantResolutionSource Source => TenantResolutionSource.ExplicitScope;
        public Guid? CompanyId { get; set; }
        public Guid? BranchId { get; set; }
        public IReadOnlyList<Guid> AllowedBranchIds { get; set; } = [];
        public bool AllBranchesAllowed { get; set; }
    }

    [Fact]
    public void WithCompany_ShouldAddCompanyIdParameter()
    {
        var companyId = Guid.NewGuid();
        var context = new FakeOrgContext { CompanyId = companyId };
        var parameters = new DynamicParameters();

        parameters.WithCompany(context);

        parameters.Get<Guid>("CompanyId").Should().Be(companyId);
    }

    [Fact]
    public void WithBranch_ShouldAddBranchIdParameter()
    {
        var branchId = Guid.NewGuid();
        var context = new FakeOrgContext { BranchId = branchId };
        var parameters = new DynamicParameters();

        parameters.WithBranch(context);

        parameters.Get<Guid>("BranchId").Should().Be(branchId);
    }

    [Fact]
    public void WithOrganization_ShouldAddAllParameters()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var context = new FakeOrgContext
        {
            Tenant = new TenantInfo(new TenantId(tenantId), "Test Tenant"),
            CompanyId = companyId,
            BranchId = branchId
        };

        var parameters = new DynamicParameters();
        parameters.WithOrganization(context);

        parameters.Get<Guid>("TenantId").Should().Be(tenantId);
        parameters.Get<Guid>("CompanyId").Should().Be(companyId);
        parameters.Get<Guid>("BranchId").Should().Be(branchId);
    }

    [Fact]
    public void WithOrganization_WhenTenantUnresolved_ShouldThrowTenantNotFoundException()
    {
        var context = new FakeOrgContext
        {
            Tenant = null,
            CompanyId = Guid.NewGuid(),
            BranchId = Guid.NewGuid()
        };

        var parameters = new DynamicParameters();
        var act = () => parameters.WithOrganization(context);

        act.Should().Throw<TenantNotFoundException>();
    }

    [Fact]
    public void WithCompany_NullParameters_ThrowsArgumentNullException()
    {
        DynamicParameters parameters = null!;
        var context = new FakeOrgContext();
        var act = () => parameters.WithCompany(context);
        act.Should().Throw<ArgumentNullException>().WithParameterName("parameters");
    }

    [Fact]
    public void WithCompany_NullContext_ThrowsArgumentNullException()
    {
        var parameters = new DynamicParameters();
        var act = () => parameters.WithCompany(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("companyContext");
    }

    [Fact]
    public void WithBranch_NullParameters_ThrowsArgumentNullException()
    {
        DynamicParameters parameters = null!;
        var context = new FakeOrgContext();
        var act = () => parameters.WithBranch(context);
        act.Should().Throw<ArgumentNullException>().WithParameterName("parameters");
    }

    [Fact]
    public void WithBranch_NullContext_ThrowsArgumentNullException()
    {
        var parameters = new DynamicParameters();
        var act = () => parameters.WithBranch(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("branchContext");
    }

    [Fact]
    public void WithOrganization_NullParameters_ThrowsArgumentNullException()
    {
        DynamicParameters parameters = null!;
        var context = new FakeOrgContext();
        var act = () => parameters.WithOrganization(context);
        act.Should().Throw<ArgumentNullException>().WithParameterName("parameters");
    }

    [Fact]
    public void WithOrganization_NullContext_ThrowsArgumentNullException()
    {
        var parameters = new DynamicParameters();
        var act = () => parameters.WithOrganization(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }
}

