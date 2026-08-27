// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class ExceptionsTests
{
    [Fact]
    public void TenantNotFoundException_DefaultConstructor_SetsDefaultMessage()
    {
        var ex = new TenantNotFoundException();
        ex.Message.Should().Be("The required tenant could not be resolved from the ambient execution context.");
    }

    [Fact]
    public void TenantNotFoundException_MessageConstructor_SetsMessage()
    {
        var ex = new TenantNotFoundException("Custom message");
        ex.Message.Should().Be("Custom message");
    }

    [Fact]
    public void TenantNotFoundException_MessageAndInnerConstructor_SetsProperties()
    {
        var inner = new InvalidOperationException("Inner");
        var ex = new TenantNotFoundException("Custom message", inner);
        ex.Message.Should().Be("Custom message");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void TenantInactiveException_TenantIdConstructor_SetsProperties()
    {
        var tenantId = TenantId.NewId();
        var ex = new TenantInactiveException(tenantId);
        ex.TenantId.Should().Be(tenantId);
        ex.Message.Should().Be($"Tenant '{tenantId}' is marked as inactive or suspended.");
    }

    [Fact]
    public void TenantInactiveException_MessageConstructor_SetsProperties()
    {
        var tenantId = TenantId.NewId();
        var ex = new TenantInactiveException(tenantId, "Custom message");
        ex.TenantId.Should().Be(tenantId);
        ex.Message.Should().Be("Custom message");
    }
}

public class ITenantContextDefaultInterfaceMethodsTests
{
    private class TestContextUnresolved : ITenantContext
    {
        public ITenantInfo? Tenant => null;
        public bool IsResolved => false;
        public TenantResolutionSource Source => TenantResolutionSource.None;
    }

    private class TestContextResolvedNullTenant : ITenantContext
    {
        public ITenantInfo? Tenant => null;
        public bool IsResolved => true;
        public TenantResolutionSource Source => TenantResolutionSource.None;
    }

    private class TestContextResolvedValidTenant : ITenantContext
    {
        public ITenantInfo? Tenant { get; } = new TenantInfo(TenantId.NewId(), "Valid");
        public bool IsResolved => true;
        public TenantResolutionSource Source => TenantResolutionSource.None;
    }

    [Fact]
    public void RequiredTenant_WhenNotResolved_ThrowsTenantNotFoundException()
    {
        ITenantContext context = new TestContextUnresolved();
        var act = () => _ = context.RequiredTenant;
        act.Should().Throw<TenantNotFoundException>()
           .WithMessage("No tenant has been resolved in the current ambient execution context.");
    }

    [Fact]
    public void RequiredTenant_WhenResolvedButTenantIsNull_ThrowsTenantNotFoundException()
    {
        ITenantContext context = new TestContextResolvedNullTenant();
        var act = () => _ = context.RequiredTenant;
        act.Should().Throw<TenantNotFoundException>()
           .WithMessage("No tenant has been resolved in the current ambient execution context.");
    }

    [Fact]
    public void RequiredTenant_WhenResolvedAndTenantExists_ReturnsTenant()
    {
        ITenantContext context = new TestContextResolvedValidTenant();
        var tenant = context.RequiredTenant;
        tenant.Should().NotBeNull();
        tenant.Name.Should().Be("Valid");
    }
}

public class TenantInfoTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var info = new TenantInfo();
        info.Id.Should().Be(TenantId.Empty);
        info.Name.Should().Be(string.Empty);
        info.ConnectionString.Should().BeNull();
        info.IsActive.Should().BeTrue();
        info.Properties.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ParameterizedConstructor_WithNullName_ThrowsArgumentNullException()
    {
        var act = () => new TenantInfo(TenantId.NewId(), null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void ParameterizedConstructor_SetsValues()
    {
        var id = TenantId.NewId();
        var info = new TenantInfo(id, "Test", "conn", false);
        info.Id.Should().Be(id);
        info.Name.Should().Be("Test");
        info.ConnectionString.Should().Be("conn");
        info.IsActive.Should().BeFalse();
    }
}

public class TenantErrorsTests
{
    [Fact]
    public void NotFound_WithTenantId_CreatesNotFoundError()
    {
        var id = TenantId.NewId();
        var error = TenantErrors.NotFound(id);
        error.Code.Should().Be("Tenant.NotFound");
        error.Description.Should().Contain(id.ToString());
    }

    [Fact]
    public void Unresolved_HasExpectedCodeAndDescription()
    {
        var error = TenantErrors.Unresolved;
        error.Code.Should().Be("Tenant.Unresolved");
        error.Description.Should().Be("The required tenant could not be resolved from the ambient execution context.");
    }

    [Fact]
    public void Inactive_WithTenantId_CreatesValidationError()
    {
        var id = TenantId.NewId();
        var error = TenantErrors.Inactive(id);
        error.Code.Should().Be("Tenant.Inactive");
        error.Description.Should().Contain(id.ToString());
    }

    [Fact]
    public void InvalidId_WithNullOrValue_CreatesValidationError()
    {
        var error1 = TenantErrors.InvalidId("bad-guid");
        error1.Code.Should().Be("Tenant.InvalidId");
        error1.Description.Should().Be("'bad-guid' is not a valid tenant identifier.");

        var error2 = TenantErrors.InvalidId(null);
        error2.Code.Should().Be("Tenant.InvalidId");
        error2.Description.Should().Be("'' is not a valid tenant identifier.");
    }

    [Fact]
    public void StrategyFailed_WithOrWithoutReason_CreatesFailureError()
    {
        var error1 = TenantErrors.StrategyFailed("CustomHeader", "Token expired");
        error1.Code.Should().Be("Tenant.ResolutionFailed");
        error1.Description.Should().Be("Resolution strategy 'CustomHeader' failed: Token expired");

        var error2 = TenantErrors.StrategyFailed("CustomHeader", null);
        error2.Code.Should().Be("Tenant.ResolutionFailed");
        error2.Description.Should().Be("Resolution strategy 'CustomHeader' failed to resolve a tenant.");

        var error3 = TenantErrors.StrategyFailed("CustomHeader", "   ");
        error3.Code.Should().Be("Tenant.ResolutionFailed");
        error3.Description.Should().Be("Resolution strategy 'CustomHeader' failed to resolve a tenant.");
    }
}
