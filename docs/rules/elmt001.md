# ELMT001: Do Not Store Tenant Context in Static Fields

| Property | Value |
|---|---|
| **Rule ID** | `ELMT001` |
| **Category** | `Safety` |
| **Severity** | `Warning` |
| **Enabled by Default** | `true` |
| **Applies to** | Static fields storing `ITenantContext`, `ITenantContextAccessor`, or concrete context implementations |

---

## 🎯 Rule Description

In multi-tenant cloud applications, tenant resolution is strictly scoped to the active execution context (e.g., HTTP request scope, message bus consumer scope, background job scope). `ITenantContext` and `ITenantContextAccessor` are scoped abstractions designed to represent the tenant of the current processing scope.

Declaring a `static` field that references an `ITenantContext` or `ITenantContextAccessor` instance causes concurrent request threads and asynchronous tasks to share and overwrite ambient tenant state across disparate tenant boundaries. This creates catastrophic cross-tenant data contamination and silent security breaches.

`ELMT001` is emitted at compile time whenever a static field is declared with any type referencing tenant context.

---

## ❌ Violation Example

```csharp
public class OrderProcessingService
{
    // Violation ELMT001: Static field creates cross-tenant context leakage
    private static ITenantContext? _currentContext;

    public void Process(Order order)
    {
        _currentContext = ResolveContext();
        // ...
    }
}
```

**Compiler Warning:**
> `warning ELMT001: Field '_currentContext' is static and stores tenant context type 'ITenantContext', which can cause cross-tenant context leakage`

---

## ✅ Compliant Example (Scoped Dependency Injection)

```csharp
public sealed class OrderProcessingService
{
    private readonly ITenantContext _tenantContext;

    // Compliant: Scoped instance injection preserves tenant isolation per request
    public OrderProcessingService(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public void Process(Order order)
    {
        var currentTenantId = _tenantContext.Id;
        // Process order strictly within scoped tenant boundary
    }
}
```

---

## 🛠️ Remediation Strategy

1. **Remove the `static` modifier** from the field.
2. Register the service with **Scoped** lifetime in Microsoft Dependency Injection (`services.AddScoped<T>()`).
3. Inject `ITenantContext` or `ITenantContextAccessor` directly via the class constructor.
