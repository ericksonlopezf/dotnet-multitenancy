// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EricksonLopez.MultiTenancy.Serialization;

/// <summary>
/// Converts a <see cref="TenantId"/> value to or from JSON representation.
/// </summary>
public sealed class TenantIdJsonConverter : JsonConverter<TenantId>
{
    /// <inheritdoc />
    public override TenantId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return TenantId.Empty;
            }

            if (Guid.TryParse(stringValue, out var guid))
            {
                return new TenantId(guid);
            }
        }
        else if (reader.TokenType == JsonTokenType.Null)
        {
            return TenantId.Empty;
        }

        throw new JsonException($"Unable to parse '{reader.GetString()}' as a valid TenantId.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TenantId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value.ToString());
    }
}
