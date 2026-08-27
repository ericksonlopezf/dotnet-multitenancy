// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text.Json;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Serialization;
using EricksonLopez.Result;

namespace EricksonLopez.MultiTenancy.Showcase.Levels.Level00_Conceptual;

/// <summary>
/// Level 0 — Conceptual: Core primitives, strongly-typed identity, and invariants.
/// </summary>
public static class ConceptualOverview
{
    public static void RunConceptualExamples()
    {
        Console.WriteLine("=== Level 0: Conceptual Overview ===");

        // 1. Strongly-typed TenantId backed by Guid
        TenantId newId = TenantId.NewId();
        TenantId parsedFromGuid = TenantId.Create(Guid.NewGuid());
        TenantId parsedFromString = TenantId.Create("11111111-1111-1111-1111-111111111111");

        Console.WriteLine($"Generated TenantId: {newId}");
        Console.WriteLine($"Parsed TenantId: {parsedFromString}");

        // 2. Safe parsing via Result<TenantId>
        Result<TenantId> validResult = TenantId.From(Guid.NewGuid());
        Result<TenantId> invalidResult = TenantId.From("invalid-guid-string");

        Console.WriteLine($"Valid Result isSuccess: {validResult.IsSuccess}");
        Console.WriteLine($"Invalid Result isFailure: {invalidResult.IsFailure}, Error: {invalidResult.Error.Description}");

        // 3. TryCreate pattern
        if (TenantId.TryCreate("22222222-2222-2222-2222-222222222222", out TenantId resultId))
        {
            Console.WriteLine($"Successfully parsed TenantId: {resultId}");
        }

        // 4. Comparison and Equality
        TenantId idA = TenantId.Create("11111111-1111-1111-1111-111111111111");
        TenantId idB = TenantId.Create("11111111-1111-1111-1111-111111111111");
        Console.WriteLine($"Equality check: {idA == idB}");

        // 5. TenantInfo metadata record
        var tenantInfo = new TenantInfo(
            id: idA,
            name: "acme-corp",
            connectionString: "Host=db.acme.internal;Database=acme;",
            isActive: true);

        Console.WriteLine($"TenantInfo created: Name={tenantInfo.Name}, Active={tenantInfo.IsActive}");

        // 6. TenantId.Empty — the sentinel value for an unresolved/null tenant
        TenantId emptyId = TenantId.Empty;
        Console.WriteLine($"TenantId.Empty.IsEmpty: {emptyId.IsEmpty}");
        Console.WriteLine($"TenantId.Empty == default: {emptyId == default}");

        // 7. Domain Errors
        Error notFoundErr = TenantErrors.NotFound(idA);
        Error inactiveErr = TenantErrors.Inactive(idA);
        Error unresolvedErr = TenantErrors.Unresolved;
        Console.WriteLine($"Domain Error: Code={notFoundErr.Code}, Message={notFoundErr.Description}");

        // 8. TenantIdJsonConverter — serialize TenantId as GUID string in System.Text.Json
        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new TenantIdJsonConverter());

        string serialized = JsonSerializer.Serialize(idA, jsonOptions);
        TenantId deserialized = JsonSerializer.Deserialize<TenantId>(serialized, jsonOptions);
        Console.WriteLine($"Serialized TenantId: {serialized}");
        Console.WriteLine($"Deserialized TenantId equals original: {deserialized == idA}");
    }
}
