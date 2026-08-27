// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using AwesomeAssertions;
using Dapper;
using EricksonLopez.MultiTenancy.Dapper;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class TenantDapperExtensionsTests
{
    private static readonly Guid ExpectedGuid = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly TenantId ExpectedTenantId = new TenantId(ExpectedGuid);

    [Fact]
    public void WithTenant_NullParameters_ThrowsArgumentNullException()
    {
        DynamicParameters parameters = null!;
        var context = Substitute.For<ITenantContext>();

        var act = () => parameters.WithTenant(context);
        act.Should().Throw<ArgumentNullException>().WithParameterName("parameters");
    }

    [Fact]
    public void WithTenant_NullContext_ThrowsArgumentNullException()
    {
        var parameters = new DynamicParameters();

        var act = () => parameters.WithTenant(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tenantContext");
    }

    [Fact]
    public void WithTenant_ValidContext_AddsParameterWithDefaultNameAndReturnsSelf()
    {
        var parameters = new DynamicParameters();
        var context = CreateTenantContext();

        var result = parameters.WithTenant(context);

        result.Should().BeSameAs(parameters);

        // Assert param was added correctly
        var paramValue = result.Get<Guid>("TenantId");
        paramValue.Should().Be(ExpectedGuid);
    }

    [Fact]
    public void WithTenant_ValidContext_AddsParameterWithCustomName()
    {
        var parameters = new DynamicParameters();
        var context = CreateTenantContext();

        var result = parameters.WithTenant(context, "CustomParam");

        var paramValue = result.Get<Guid>("CustomParam");
        paramValue.Should().Be(ExpectedGuid);
    }

    [Fact]
    public void CreateTenantParameters_NullContext_ThrowsArgumentNullException()
    {
        ITenantContext context = null!;

        var act = () => context.CreateTenantParameters();
        act.Should().Throw<ArgumentNullException>().WithParameterName("tenantContext");
    }

    [Fact]
    public void CreateTenantParameters_ValidContext_ReturnsNewParametersWithDefaultName()
    {
        var context = CreateTenantContext();

        var result = context.CreateTenantParameters();

        result.Should().NotBeNull();
        var paramValue = result.Get<Guid>("TenantId");
        paramValue.Should().Be(ExpectedGuid);
    }

    [Fact]
    public void CreateTenantParameters_ValidContext_ReturnsNewParametersWithCustomName()
    {
        var context = CreateTenantContext();

        var result = context.CreateTenantParameters("CustomParam");

        result.Should().NotBeNull();
        var paramValue = result.Get<Guid>("CustomParam");
        paramValue.Should().Be(ExpectedGuid);
    }

    private static ITenantContext CreateTenantContext()
    {
        var tenantInfo = new TenantInfo(ExpectedTenantId, "DapperTenant");
        var context = Substitute.For<ITenantContext>();
        context.RequiredTenant.Returns(tenantInfo);
        return context;
    }
}
