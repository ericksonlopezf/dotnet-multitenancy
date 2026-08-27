# Tenant Domain Model & Invariants — EricksonLopez.MultiTenancy

This document describes the domain modeling principles, invariants, and boundaries for tenant representation in the `EricksonLopez.MultiTenancy` ecosystem.

---

## 1. Domain Concept Taxonomy

| Concept | Owner / Layer | Description |
| :--- | :--- | :--- |
| **Tenant** | `EricksonLopez.MultiTenancy` | The top-level customer, subscriber, or organization boundary. Represented by `TenantId`. |
| **TenantId** | `EricksonLopez.MultiTenancy` | Strongly-typed, immutable readonly record struct wrapping a 128-bit `Guid`. |
| **Company** | Application Domain | A sub-organizational unit within a tenant (e.g. branch, subsidiary). Multi-tenancy does not manage companies; this is pure application business domain. |
| **User** | Identity Layer | An authenticated human actor or service principal (`ClaimsPrincipal`). |
| **Membership** | Application / Auth | The relationship mapping users to tenants with assigned roles and permissions. |
| **Tenant Context** | `EricksonLopez.MultiTenancy` | An immutable, request-scoped record (`ITenantContext`) containing active tenant information. |

---

## 2. Invariants of `TenantId`

1. **Non-Empty Validation:** Active tenant resolution requires a valid, non-empty GUID. Attempting to call `TenantId.Create(Guid.Empty)` throws `ArgumentException`.
2. **Structural Equality:** `TenantId` implements `IEquatable<TenantId>` and `IComparable<TenantId>` for efficient hashing in dictionaries and zero heap allocations.
3. **Deterministic Formatting:** `ToString()` outputs standard 36-character hyphenated UUID format (`D` format).
4. **JSON Interoperability:** Custom `TenantIdJsonConverter` provides high-performance UTF-8 byte span parsing for `System.Text.Json` without intermediate string allocations.

---

## 3. Extending the Tenant Model

Applications can extend `ITenantInfo` with custom domain properties:

```csharp
using EricksonLopez.MultiTenancy;

public class CustomTenantInfo : ITenantInfo
{
    public TenantId Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public string? ConnectionString { get; init; }
    
    // Custom domain-specific metadata
    public string PlanTier { get; init; } = "Standard";
    public int MaxAllowedUsers { get; init; } = 100;
    public string CustomDomain { get; init; } = string.Empty;
}

// In Program.cs:
builder.Services.AddMultiTenancy<CustomTenantInfo>();
```
