# Best Practices & Roslyn Analyzers — EricksonLopez.MultiTenancy

This guide outlines the architectural invariants, security rules, and Roslyn static analyzer guidelines enforced across the `EricksonLopez.MultiTenancy` ecosystem.

---

## 1. Roslyn Static Analyzers

The `EricksonLopez.MultiTenancy.Analyzers` package provides compile-time diagnostic rules to catch multi-tenancy anti-patterns before code reaches production.

### `ELMT001`: Static TenantContext Field Analyzer
- **Severity:** Error
- **Rule:** `ITenantContext` and `ITenantContextAccessor` instances must never be stored in `static` fields.
- **Why:** Storing request-scoped tenant state in a static field creates severe ambient context leakage across concurrent threads.

```csharp
// ❌ BAD: Static field retains tenant context across all requests
public class InvoiceService
{
    private static ITenantContext _staticContext; // Trigger ELMT001 Error!
}

// ✅ GOOD: Scoped constructor injection
public class InvoiceService
{
    private readonly ITenantContext _tenantContext;

    public InvoiceService(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }
}
```

---

### `ELMT002`: TenantContext in Singleton Service Analyzer
- **Severity:** Error
- **Rule:** `ITenantContext` or `ITenantContextAccessor` must never be injected into services registered with `Singleton` lifetime.
- **Why:** A singleton service resolves its dependencies only once at application startup. Injecting a scoped context creates a *captive dependency*, freezing the tenant context of the first request for the entire lifetime of the process.

```csharp
// ❌ BAD: Singleton service capturing Scoped tenant context
builder.Services.AddSingleton<GlobalReportingEngine>(); // Captures ITenantContext!

// ✅ GOOD: Register as Scoped or inject IServiceProvider / ITenantScopeFactory
builder.Services.AddScoped<GlobalReportingEngine>();
```

---

### `ELMT003`: Dapper Query Without Tenant Parameter Analyzer
- **Severity:** Warning
- **Rule:** Executing Dapper queries (`QueryAsync`, `ExecuteAsync`) without including tenant parameter helpers (`WithTenant` or `CreateTenantParameters`) triggers a warning.
- **Why:** Enforces Layer 1 Defense-in-Depth so that developer oversight does not omit application-level filtering.

```csharp
// ❌ BAD: Querying without explicit tenant parameter
await connection.QueryAsync<Invoice>("SELECT * FROM invoices WHERE id = @Id", new { Id = invoiceId });

// ✅ GOOD: Querying with explicit tenant parameter injection
var parameters = tenantContext.CreateTenantParameters(new { Id = invoiceId });
await connection.QueryAsync<Invoice>("SELECT * FROM invoices WHERE id = @Id AND tenant_id = @TenantId", parameters);
```

---

## 2. Security Best Practices

### Rule 1: Always Implement Defense-in-Depth
Never rely exclusively on application filters or exclusively on database RLS. Use both:
- **Application Level:** Explicit `WHERE tenant_id = @TenantId` parameterization in every repository method.
- **Database Level:** PostgreSQL `FORCE ROW LEVEL SECURITY` with `SET LOCAL app.current_tenant_id = :tenantId` within transactions.

### Rule 2: Claims Always Trump Headers
Never allow unauthenticated HTTP headers (`X-Tenant-ID`) to override cryptographically verified JWT claims. If an incoming request contains mismatched claim and header tenant identifiers, fail closed immediately with HTTP 400 Bad Request.

### Rule 3: Use Strongly-Typed `TenantId`
Avoid stringly-typed identifiers (`string tenantId`). Always use the immutable `TenantId` readonly struct to ensure structural equality, null-safety, and validation.

### Rule 4: Explicit Background Scopes
For non-HTTP jobs (Hangfire, RabbitMQ, hosted services), always create an explicit scope using `ITenantScopeFactory.CreateScope(tenantInfo)`. Never process multiple tenants inside the same DI scope.
