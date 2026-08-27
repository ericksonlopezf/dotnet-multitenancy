# ELMT002: Do Not Reference Scoped Tenant Context in Singleton Classes

| Property | Value |
|---|---|
| **Rule ID** | `ELMT002` |
| **Category** | `Safety` |
| **Severity** | `Warning` |
| **Enabled by Default** | `true` |
| **Applies to** | Classes ending in `Singleton`, `Cache`, or `MemoryStore` holding tenant context fields |

---

## 🎯 Rule Description

Services registered with **Singleton** lifetime or designed as in-memory global caches live for the entire lifetime of the application host.

Injecting or storing a scoped `ITenantContext` or `ITenantContextAccessor` inside a Singleton class results in a **Captive Dependency**. The Singleton instance captures the tenant context of the very first request that instantiates it, and subsequently reuses that stale tenant identity for all subsequent requests from all other tenants throughout the application lifecycle.

`ELMT002` is emitted at compile time whenever a singleton or cache type holds an instance field referencing tenant context abstractions.

---

## ❌ Violation Example

```csharp
// Violation ELMT002: Singleton/Cache type capturing scoped tenant context
public sealed class TenantProductCache
{
    private readonly ITenantContext _tenantContext;
    private readonly Dictionary<string, Product> _cache = new();

    public TenantProductCache(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Product? GetProduct(string id)
    {
        // Stale tenant context captured from the first request
        var tenantId = _tenantContext.Id;
        return _cache.TryGetValue($"{tenantId}:{id}", out var p) ? p : null;
    }
}
```

**Compiler Warning:**
> `warning ELMT002: Type 'TenantProductCache' appears to be a singleton or cache and references scoped tenant context 'ITenantContext', which causes captive dependency leaks`

---

## ✅ Compliant Example (Parameter Passing at Method Boundary)

```csharp
// Compliant: Singleton cache accepts TenantId explicitly on each invocation
public sealed class GlobalProductCache
{
    private readonly ConcurrentDictionary<string, Product> _cache = new();

    public Product? GetProduct(TenantId tenantId, string id)
    {
        return _cache.TryGetValue($"{tenantId.Value}:{id}", out var p) ? p : null;
    }

    public void SetProduct(TenantId tenantId, string id, Product product)
    {
        _cache[$"{tenantId.Value}:{id}"] = product;
    }
}
```

---

## 🛠️ Remediation Strategy

1. **Do not inject `ITenantContext`** into Singleton or Cache classes.
2. Pass `TenantId` or `ITenantContext` as a method argument from caller services that reside within a scoped lifecycle.
3. If per-tenant caching is required, use a scoped wrapper or tenant-partitioned cache keys (`tenantId:key`).
