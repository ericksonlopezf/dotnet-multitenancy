# Allocation & Memory Footprint Analysis — EricksonLopez.MultiTenancy

This document details the memory layout, heap allocation guarantees, and zero-allocation techniques implemented across the `EricksonLopez.MultiTenancy` ecosystem.

---

## 1. Struct-Based Identifier Layout (`TenantId`)

`TenantId` is defined as an immutable **`readonly record struct`** wrapping a 16-byte `System.Guid`. Because value types reside on the stack or inline within containing objects (and in CPU registers during JIT compilation), passing, comparing, and hashing tenant identifiers incurs **0 heap allocations**.

```
+-------------------------------------------------------------+
| TenantId Struct Memory Layout (Stack / Register)            |
+--------------------------+----------------------------------+
| Field                    | Type / Size                      |
+--------------------------+----------------------------------+
| Value                    | System.Guid (16 Bytes / 128 bit) |
+--------------------------+----------------------------------+
```

### Memory Allocation Matrix

| Execution Scenario | Envelope Allocation | Heap Allocation |
|---|---|---|
| `TenantId.Create(guid)` | **0 bytes** (Stack) | **0 bytes** |
| `TenantId.TryCreate(guid, out id)` | **0 bytes** (Stack) | **0 bytes** |
| `id1 == id2` (Equality) | **0 bytes** | **0 bytes** (Inlined SIMD / register comparison) |
| `id.ToString()` | **0 bytes** | 36 characters string allocated on heap |
| `TenantId.Empty` | **0 bytes** (Static readonly) | **0 bytes** |

---

## 2. Scoped Accessor vs AsyncLocal Memory Profile

Traditional multi-tenancy frameworks store ambient tenant context in `AsyncLocal<T>`, which suffers from several memory and lifecycle pitfalls:

1. **AsyncLocal Context Flow Overhead:** `AsyncLocal<T>` allocates `ExecutionContext` nodes on every `await` boundary if values change across async flows.
2. **Memory Retention (Captive Leaks):** If an uncompleted background task references `AsyncLocal`, the entire `TenantInfo` object graph is retained in memory.

`EricksonLopez.MultiTenancy` rejects static `AsyncLocal` context mutation (ADR-002) in favor of **`ScopedTenantContextAccessor`** tied to ASP.NET Core `IServiceScope`:

- The accessor is instantiated once per HTTP request or background job.
- When the request finishes, the `IServiceScope` is disposed, immediately freeing the `ITenantContext` and tenant metadata for Gen0 Garbage Collection.
- No `ExecutionContext` mutation or copy-on-write dictionaries are allocated during async method invocation.

---

## 3. Zero-Allocation Strategy Resolution & Fail-Closed Validation

In high-throughput microservices handling 50,000+ RPS, string allocations during tenant resolution become a major source of GC pressure.

`EricksonLopez.MultiTenancy` optimizes resolution pathways:

- **Header / Claim Parsing:** Uses `StringSegment` and `ReadOnlySpan<char>` slicing before calling `TenantId.TryCreate(ReadOnlySpan<char>)`.
- **Cached Store Lookups:** `CachedTenantStore` caches deserialized `ITenantInfo` instances in memory, avoiding JSON/DB allocations per request.
- **Fail-Closed Strategy Conflict:** Conflict detection executes without allocating intermediate list collections by using stack-allocated state checks.

---

## 4. Dapper Parameter Extensions Efficiency

The `TenantDapperExtensions` provides zero-reflection parameter injection:

```csharp
// Appends 16-byte raw GUID directly to Dapper DynamicParameters
parameters.WithTenant(tenantContext);
```

Because `TenantId.Value` is a raw `Guid` passed directly by value, Dapper treats it as a primitive `DbType.Guid` without reflection-based parameter mapper invocation or boxing conversions.
