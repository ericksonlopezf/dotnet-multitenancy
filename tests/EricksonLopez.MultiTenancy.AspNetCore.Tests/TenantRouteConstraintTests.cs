// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class TenantRouteConstraintTests
{
    private readonly TenantRouteConstraint _constraint = new();

    [Fact]
    public void Match_ValidGuidString_ReturnsTrue()
    {
        var validId = Guid.NewGuid().ToString();
        var values = new RouteValueDictionary { { "tenant", validId } };

        var result = _constraint.Match(null, null, "tenant", values, RouteDirection.IncomingRequest);

        result.Should().BeTrue();
    }

    [Fact]
    public void Match_InvalidString_ReturnsFalse()
    {
        var values = new RouteValueDictionary { { "tenant", "not-a-guid" } };

        var result = _constraint.Match(null, null, "tenant", values, RouteDirection.IncomingRequest);

        result.Should().BeFalse();
    }

    [Fact]
    public void Match_NullValue_ReturnsFalse()
    {
        var values = new RouteValueDictionary { { "tenant", null } };

        var result = _constraint.Match(null, null, "tenant", values, RouteDirection.IncomingRequest);

        result.Should().BeFalse();
    }

    [Fact]
    public void Match_MissingRouteKey_ReturnsFalse()
    {
        var values = new RouteValueDictionary { { "other", "value" } };

        var result = _constraint.Match(null, null, "tenant", values, RouteDirection.IncomingRequest);

        result.Should().BeFalse();
    }
}
