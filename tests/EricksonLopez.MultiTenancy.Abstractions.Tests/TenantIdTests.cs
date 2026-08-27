// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class TenantIdTests
{
    private static readonly Guid AlphaGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid BetaGuid = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    [Fact]
    public void Empty_HasEmptyGuidValue()
    {
        var tenantId = TenantId.Empty;

        tenantId.IsEmpty.Should().BeTrue();
        tenantId.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Constructor_SetsValue()
    {
        var tenantId = new TenantId(AlphaGuid);
        tenantId.Value.Should().Be(AlphaGuid);
        tenantId.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Create_FromValidGuid_ReturnsCorrectStruct()
    {
        var tenantId = TenantId.Create(AlphaGuid);

        tenantId.Value.Should().Be(AlphaGuid);
        tenantId.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Create_FromEmptyGuid_ThrowsArgumentException()
    {
        var act = () => TenantId.Create(Guid.Empty);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*cannot be an empty GUID*")
           .WithParameterName("value");
    }

    [Fact]
    public void Create_FromValidGuidString_ReturnsCorrectStruct()
    {
        var guidStr = AlphaGuid.ToString("D");
        var tenantId = TenantId.Create(guidStr);

        tenantId.Value.Should().Be(AlphaGuid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_FromWhitespaceString_ThrowsArgumentException(string value)
    {
        var act = () => TenantId.Create(value);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*cannot be null or whitespace*")
           .WithParameterName(nameof(value));
    }

    [Fact]
    public void Create_FromNullString_ThrowsArgumentException()
    {
        var act = () => TenantId.Create(null!);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*cannot be null or whitespace*")
           .WithParameterName("value");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")] // Guid.Empty
    public void Create_FromInvalidOrEmptyString_ThrowsArgumentException(string value)
    {
        var act = () => TenantId.Create(value);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*not a valid tenant identifier*")
           .WithParameterName(nameof(value));
    }

    [Fact]
    public void NewId_CreatesUniqueNonEmptyId()
    {
        var id1 = TenantId.NewId();
        var id2 = TenantId.NewId();

        id1.IsEmpty.Should().BeFalse();
        id2.IsEmpty.Should().BeFalse();
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void TryCreate_ValidGuidString_ReturnsTrueAndResult()
    {
        var result = TenantId.TryCreate(AlphaGuid.ToString("D"), out var tenantId);

        result.Should().BeTrue();
        tenantId.Value.Should().Be(AlphaGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void TryCreate_InvalidStringValue_ReturnsFalseAndEmpty(string? value)
    {
        var result = TenantId.TryCreate(value, out var tenantId);

        result.Should().BeFalse();
        tenantId.IsEmpty.Should().BeTrue();
        tenantId.Should().Be(TenantId.Empty);
    }

    [Fact]
    public void TryCreate_ValidGuid_ReturnsTrueAndResult()
    {
        var result = TenantId.TryCreate(AlphaGuid, out var tenantId);

        result.Should().BeTrue();
        tenantId.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public void TryCreate_EmptyGuid_ReturnsFalseAndEmpty()
    {
        var result = TenantId.TryCreate(Guid.Empty, out var tenantId);

        result.Should().BeFalse();
        tenantId.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void CompareTo_TenantId_ReturnsCorrectComparison()
    {
        var id1 = new TenantId(AlphaGuid);
        var id2 = new TenantId(BetaGuid); // aaaaa... < bbbbb...

        id1.CompareTo(id2).Should().BeNegative();
        id2.CompareTo(id1).Should().BePositive();
        id1.CompareTo(id1).Should().Be(0);
    }

    [Fact]
    public void CompareTo_Object_WithNull_ReturnsPositive()
    {
        var id = new TenantId(AlphaGuid);
        id.CompareTo((object?)null).Should().Be(1);
    }

    [Fact]
    public void CompareTo_Object_WithTenantId_ReturnsCorrectComparison()
    {
        var id1 = new TenantId(AlphaGuid);
        object id2 = new TenantId(BetaGuid);

        id1.CompareTo(id2).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_Object_WithOtherType_ThrowsArgumentException()
    {
        var id = new TenantId(AlphaGuid);
        object other = "not a tenant id";

        var act = () => id.CompareTo(other);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*Object must be of type TenantId*")
           .WithParameterName("obj");
    }

    [Fact]
    public void Operators_Comparison_WorkCorrectly()
    {
        var id1 = new TenantId(AlphaGuid);
        var id2 = new TenantId(BetaGuid);
        var id1Copy = new TenantId(AlphaGuid);

        (id1 < id2).Should().BeTrue();
        (id1 < id1Copy).Should().BeFalse();
        (id2 < id1).Should().BeFalse();

        (id1 <= id2).Should().BeTrue();
        (id1 <= id1Copy).Should().BeTrue();
        (id2 <= id1).Should().BeFalse();

        (id2 > id1).Should().BeTrue();
        (id1 > id1Copy).Should().BeFalse();
        (id1 > id2).Should().BeFalse();

        (id2 >= id1).Should().BeTrue();
        (id1 >= id1Copy).Should().BeTrue();
        (id1 >= id2).Should().BeFalse();
    }

    [Fact]
    public void ToString_ReturnsDFormatGuid()
    {
        var id = new TenantId(AlphaGuid);
        id.ToString().Should().Be(AlphaGuid.ToString("D"));
    }

    [Fact]
    public void ImplicitConversion_ToGuid_Works()
    {
        var id = new TenantId(AlphaGuid);
        Guid g = id; // implicit

        g.Should().Be(AlphaGuid);
    }

    [Fact]
    public void From_ValidGuid_ReturnsSuccessResult()
    {
        var result = TenantId.From(AlphaGuid);
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }

    [Fact]
    public void From_EmptyGuid_ReturnsFailureInvalidId()
    {
        var result = TenantId.From(Guid.Empty);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
        result.Error.Description.Should().Be("'00000000-0000-0000-0000-000000000000' is not a valid tenant identifier.");
    }

    [Fact]
    public void From_ValidGuidString_ReturnsSuccessResult()
    {
        var result = TenantId.From(AlphaGuid.ToString("D"));
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(AlphaGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void From_InvalidString_ReturnsFailureInvalidId(string? value)
    {
        var result = TenantId.From(value);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidId");
    }

    [Fact]
    public void ExplicitConversion_FromGuid_Works()
    {
        var id = (TenantId)AlphaGuid;
        id.Value.Should().Be(AlphaGuid);
    }
}
