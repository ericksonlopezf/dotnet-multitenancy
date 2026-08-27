# Performance Guide & Native AOT Compatibility — EricksonLopez.MultiTenancy

This guide details the architectural optimizations, zero-allocation design principles, and Native AOT compilation guarantees implemented across `EricksonLopez.MultiTenancy`.

---

## 1. Zero-Allocation Design Principles

### Readonly Record Struct `TenantId`
- Backed by an immutable 128-bit `Guid` (16 bytes).
- Implements `IEquatable<TenantId>` and `IComparable<TenantId>` to prevent boxing overhead during hashing and equality comparisons.
- Safe parsing via `TenantId.TryCreate()` avoids expensive exception stack trace allocations in the request hot path.

### Scoped Write-Once Accessor
- `ScopedTenantContextAccessor` holds direct instance references without utilizing thread-local dictionaries or unbounded synchronization locks.
- Replaces heavy reflection-based context resolvers with direct interface dispatch.

---

## 2. Native AOT & Trimming Guarantees

All packages in the ecosystem are compiled and verified with:
- `<IsAotCompatible>true</IsAotCompatible>`
- `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`

### Architectural Rules for AOT Safety:
1. **Zero Dynamic Reflection:** No calls to `Type.GetType()`, `Activator.CreateInstance()`, or `Assembly.GetTypes()` in the runtime request path.
2. **Explicit DI Registrations:** All strategies and services are registered via explicit typed delegates rather than dynamic open-generic assembly scanning.
3. **AOT-Safe JSON Serialization:** `TenantIdJsonConverter` utilizes explicit UTF-8 byte span parsing and formatting without reflection-based serializer fallbacks.

---

## 3. High-Throughput Caching Strategies

### `CachedTenantStore<TTenant>`
To prevent frequent database roundtrips for tenant metadata lookups during HTTP request resolution, wrap your database store with `CachedTenantStore`:

```csharp
builder.Services.AddPostgreSqlTenantStore<TenantInfo>(connectionString);
builder.Services.AddCachedTenantStore<TenantInfo>(options =>
{
    options.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
    options.SlidingExpiration = TimeSpan.FromMinutes(15);
});
```

- **Thread Safety:** Implemented using `IMemoryCache` with concurrent read locks.
- **Cache Eviction:** Entries expire **only via TTL** (`AbsoluteExpirationRelativeToNow` or `SlidingExpiration`). There is no active invalidation triggered by tenant status changes in the backing store. If you deactivate a tenant, it will remain cached until the TTL elapses — plan your expiration windows accordingly.
