// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.AspNetCore;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class TenantResolutionConflictExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessageAndEmptyTenantIds()
    {
        var ex = new TenantResolutionConflictException("Conflict detected");

        ex.Message.Should().Be("Conflict detected");
        ex.FirstTenantId.Should().Be(TenantId.Empty);
        ex.FirstStrategy.Should().BeNull();
        ex.SecondTenantId.Should().Be(TenantId.Empty);
        ex.SecondStrategy.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsProperties()
    {
        var inner = new InvalidOperationException("Inner error");
        var ex = new TenantResolutionConflictException("Conflict detected", inner);

        ex.Message.Should().Be("Conflict detected");
        ex.InnerException.Should().BeSameAs(inner);
        ex.FirstTenantId.Should().Be(TenantId.Empty);
        ex.SecondTenantId.Should().Be(TenantId.Empty);
    }

    [Fact]
    public void Constructor_WithFullConflictDetails_SetsAllProperties()
    {
        var firstId = TenantId.NewId();
        var secondId = TenantId.NewId();

        var ex = new TenantResolutionConflictException(
            "Conflict between Claim and Header",
            firstId,
            "ClaimTenantResolutionStrategy",
            secondId,
            "HeaderTenantResolutionStrategy");

        ex.Message.Should().Be("Conflict between Claim and Header");
        ex.FirstTenantId.Should().Be(firstId);
        ex.FirstStrategy.Should().Be("ClaimTenantResolutionStrategy");
        ex.SecondTenantId.Should().Be(secondId);
        ex.SecondStrategy.Should().Be("HeaderTenantResolutionStrategy");
    }
}
