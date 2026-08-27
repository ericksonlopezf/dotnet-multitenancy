// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.Serialization;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class TenantIdJsonConverterTests
{
    private static readonly Guid SampleGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private readonly JsonSerializerOptions _options = new();

    private sealed class DtoWithTenantId
    {
        public TenantId TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Serialize_ValidTenantId_WritesGuidString()
    {
        var tenantId = TenantId.Create(SampleGuid);
        var json = JsonSerializer.Serialize(tenantId, _options);

        json.Should().Be($"\"{SampleGuid}\"");
    }

    [Fact]
    public void Serialize_EmptyTenantId_WritesEmptyGuidString()
    {
        var tenantId = TenantId.Empty;
        var json = JsonSerializer.Serialize(tenantId, _options);

        json.Should().Be("\"00000000-0000-0000-0000-000000000000\"");
    }

    [Fact]
    public void Serialize_DtoWithTenantId_SerializesCorrectly()
    {
        var dto = new DtoWithTenantId
        {
            TenantId = TenantId.Create(SampleGuid),
            Name = "Acme Corp"
        };

        var json = JsonSerializer.Serialize(dto, _options);
        json.Should().Contain($"\"TenantId\":\"{SampleGuid}\"");
        json.Should().Contain("\"Name\":\"Acme Corp\"");
    }

    [Fact]
    public void Deserialize_ValidGuidString_ReturnsTenantId()
    {
        var json = $"\"{SampleGuid}\"";
        var result = JsonSerializer.Deserialize<TenantId>(json, _options);

        result.Value.Should().Be(SampleGuid);
        result.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_EmptyString_ReturnsTenantIdEmpty()
    {
        var json = "\"\"";
        var result = JsonSerializer.Deserialize<TenantId>(json, _options);

        result.IsEmpty.Should().BeTrue();
        result.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Deserialize_WhitespaceString_ReturnsTenantIdEmpty()
    {
        var json = "\"   \"";
        var result = JsonSerializer.Deserialize<TenantId>(json, _options);

        result.IsEmpty.Should().BeTrue();
        result.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Deserialize_NullJsonLiteral_ReturnsTenantIdEmpty()
    {
        var json = "null";
        var result = JsonSerializer.Deserialize<TenantId>(json, _options);

        result.IsEmpty.Should().BeTrue();
        result.Value.Should().Be(Guid.Empty);
    }

    [Theory]
    [InlineData("\"not-a-valid-guid\"")]
    [InlineData("\"12345\"")]
    [InlineData("\"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx\"")]
    public void Deserialize_InvalidGuidString_ThrowsJsonException(string json)
    {
        var act = () => JsonSerializer.Deserialize<TenantId>(json, _options);

        act.Should().Throw<JsonException>()
           .WithMessage("*Unable to parse*as a valid TenantId*");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Deserialize_NonStringTokens_ThrowsJsonException(string json)
    {
        var act = () => JsonSerializer.Deserialize<TenantId>(json, _options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_DtoWithTenantId_DeserializesCorrectly()
    {
        var json = $"{{\"TenantId\":\"{SampleGuid}\",\"Name\":\"Acme Corp\"}}";
        var dto = JsonSerializer.Deserialize<DtoWithTenantId>(json, _options);

        dto.Should().NotBeNull();
        dto!.TenantId.Value.Should().Be(SampleGuid);
        dto.Name.Should().Be("Acme Corp");
    }

    [Fact]
    public void Write_NullWriter_ThrowsArgumentNullException()
    {
        var converter = new TenantIdJsonConverter();
        var act = () => converter.Write(null!, TenantId.Empty, _options);

        act.Should().Throw<ArgumentNullException>().WithParameterName("writer");
    }

    [Fact]
    public void Roundtrip_MaintainsValueEquivalence()
    {
        var original = TenantId.NewId();
        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<TenantId>(json, _options);

        deserialized.Should().Be(original);
        deserialized.Value.Should().Be(original.Value);
    }
}
