// Copyright © Erickson Lopez. MIT License.
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class PlatformAdminContextTests
{
    [Fact]
    public void None_ReturnsDisabledContextWithNullAuditReason()
    {
        var context = PlatformAdminContext.None;

        context.IsPlatformAdmin.Should().BeFalse();
        context.AuditReason.Should().BeNull();
    }

    [Fact]
    public void Constructor_DefaultArguments_EnablesAdminWithNullAuditReason()
    {
        var context = new PlatformAdminContext();

        context.IsPlatformAdmin.Should().BeTrue();
        context.AuditReason.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithExplicitArguments_SetsPropertiesCorrectly()
    {
        var context = new PlatformAdminContext(isPlatformAdmin: true, auditReason: "SOC2 Compliance Audit");

        context.IsPlatformAdmin.Should().BeTrue();
        context.AuditReason.Should().Be("SOC2 Compliance Audit");
    }

    [Fact]
    public void Constructor_WithDisabledAdminAndAuditReason_SetsPropertiesCorrectly()
    {
        var context = new PlatformAdminContext(isPlatformAdmin: false, auditReason: "Read-only Observation");

        context.IsPlatformAdmin.Should().BeFalse();
        context.AuditReason.Should().Be("Read-only Observation");
    }
}
