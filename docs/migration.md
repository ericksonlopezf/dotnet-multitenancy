# Migration Guide — EricksonLopez.MultiTenancy

This guide details how to migrate existing applications to the modern `EricksonLopez.MultiTenancy` architecture.

---

## 1. Migration Overview

| Old Pattern / Package | New Architecture Pattern | Architectural Rationale |
| :--- | :--- | :--- |
| `string TenantId` | Strongly-typed `TenantId` readonly struct | Prevents empty/null bugs, standardizes UUID format, zero heap allocations. |
| Static `AsyncLocalTenantContextAccessor` | `ScopedTenantContextAccessor` | Eliminates thread-pool ambient context leakage across requests. |
| `EricksonLopez.MultiTenancy.EntityFrameworkCore` | `EricksonLopez.MultiTenancy.Dapper` + PostgreSQL RLS | Guarantees Native AOT compatibility and enforces true database-level security (ADR-001). |
| Automatic SQL Rewriting / Global Query Filters | Explicit `WHERE tenant_id = @TenantId` + RLS | Eliminates dynamic SQL parsing overhead and guarantees auditable queries (ADR-007). |
| Header-First Resolution | Claims-First Resolution (JWT) | Eliminates tenant spoofing vulnerability (ADR-003, ADR-008). |

---

## 2. Breaking Changes & Code Updates

### A. Updating `TenantId` References

```csharp
// ❌ OLD: String-based identifier
string tenantId = "11111111-1111-1111-1111-111111111111";

// ✅ NEW: Strongly-typed struct
TenantId tenantId = TenantId.Create("11111111-1111-1111-1111-111111111111");
```

---

### B. Updating Dependency Injection Registration

```csharp
// ❌ OLD: Unsafe static AsyncLocal accessor
builder.Services.AddSingleton<ITenantContextAccessor, AsyncLocalTenantContextAccessor>();

// ✅ NEW: Modern DI-scoped setup
builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();
```

---

### C. Migrating from Entity Framework to Dapper + RLS

```csharp
// ❌ OLD: Relying on EF Core global query filters
public class LegacyDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>().HasQueryFilter(i => i.TenantId == _currentTenantId);
    }
}

// ✅ NEW: Explicit Dapper parameterization + PostgreSQL RLS
public class InvoiceRepository
{
    public async Task<IEnumerable<Invoice>> GetInvoicesAsync(ITenantContext context, DbConnection conn)
    {
        var parameters = context.CreateTenantParameters();
        return await conn.QueryAsync<Invoice>(
            "SELECT * FROM invoices WHERE tenant_id = @TenantId",
            parameters);
    }
}
```

---

## 3. Migration Checklist

- [ ] Replace all `string TenantId` properties with `TenantId` struct.
- [ ] Remove references to `EricksonLopez.MultiTenancy.EntityFrameworkCore`.
- [ ] Ensure `ITenantContextAccessor` is registered with `Scoped` lifetime.
- [ ] Verify that SQL queries include explicit `WHERE tenant_id = @TenantId`.
- [ ] Enable PostgreSQL RLS on all tenant tables (`ALTER TABLE ... FORCE ROW LEVEL SECURITY`).
- [ ] Update background workers to use `ITenantScopeFactory.CreateScope()`.
