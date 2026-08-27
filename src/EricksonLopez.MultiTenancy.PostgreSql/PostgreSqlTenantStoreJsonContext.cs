// Copyright © Erickson Lopez. MIT License.
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EricksonLopez.MultiTenancy.PostgreSql;

[JsonSerializable(typeof(Dictionary<string, string>))]
[ExcludeFromCodeCoverage]
internal sealed partial class PostgreSqlTenantStoreJsonContext : JsonSerializerContext
{
}
