# ELMT003: Dapper Query Executed Without Tenant Parameter

| Property | Value |
|---|---|
| **Rule ID** | `ELMT003` |
| **Category** | `Security` |
| **Severity** | `Warning` |
| **Enabled by Default** | `true` |
| **Applies to** | Dapper invocation methods (`QueryAsync`, `ExecuteAsync`, etc.) in tenant-aware classes |

---

## 🎯 Rule Description

In multi-tenant relational persistence architectures, SQL queries executed in services or repositories possessing an active `ITenantContext` must explicitly bind the tenant identity to the SQL command parameters (Layer 1 of Defense-in-Depth).

Executing Dapper query or execution methods without parameters or with an anonymous object that omits the `tenant_id` / `TenantId` parameter risks returning unpartitioned cross-tenant data or updating rows belonging to other tenants.

`ELMT003` is emitted at compile time whenever a Dapper query method is invoked in a tenant-aware class without passing tenant parameters or using the `WithTenant()` / `CreateTenantParameters()` extensions.

---

## ❌ Violation Example

```csharp
public sealed class OrderRepository
{
    private readonly IDbConnection _connection;
    private readonly ITenantContext _tenantContext;

    public OrderRepository(IDbConnection connection, ITenantContext tenantContext)
    {
        _connection = connection;
        _tenantContext = tenantContext;
    }

    public async Task<IEnumerable<Order>> GetOrdersAsync()
    {
        // Violation ELMT003: Query executed without tenant parameter in tenant-aware repository
        return await _connection.QueryAsync<Order>(
            "SELECT * FROM orders WHERE status = @Status",
            new { Status = "Pending" });
    }
}
```

**Compiler Warning:**
> `warning ELMT003: Dapper invocation 'QueryAsync' in tenant-aware context 'OrderRepository' does not provide tenant parameters via WithTenant() or CreateTenantParameters()`

---

## ✅ Compliant Example (Option A: Fluent `WithTenant()` Extension)

```csharp
using EricksonLopez.MultiTenancy.Dapper;

public sealed class OrderRepository
{
    private readonly IDbConnection _connection;
    private readonly ITenantContext _tenantContext;

    public OrderRepository(IDbConnection connection, ITenantContext tenantContext)
    {
        _connection = connection;
        _tenantContext = tenantContext;
    }

    public async Task<IEnumerable<Order>> GetOrdersAsync()
    {
        var parameters = new DynamicParameters();
        parameters.Add("Status", "Pending");

        // Compliant: WithTenant automatically binds the scoped TenantId to @TenantId
        return await _connection.QueryAsync<Order>(
            "SELECT * FROM orders WHERE tenant_id = @TenantId AND status = @Status",
            parameters.WithTenant(_tenantContext));
    }
}
```

---

## ✅ Compliant Example (Option B: `CreateTenantParameters()` Helper)

```csharp
using EricksonLopez.MultiTenancy.Dapper;

public async Task<IEnumerable<Order>> GetAllOrdersAsync()
{
    // Compliant: CreateTenantParameters creates a parameter bag pre-populated with @TenantId
    return await _connection.QueryAsync<Order>(
        "SELECT * FROM orders WHERE tenant_id = @TenantId",
        _tenantContext.CreateTenantParameters());
}
```

---

## 🛠️ Remediation Strategy

1. Use `_tenantContext.CreateTenantParameters()` for queries filtering solely by `TenantId`.
2. Use `parameters.WithTenant(_tenantContext)` when chaining tenant parameters with additional query filters.
3. Explicitly include `TenantId = _tenantContext.Id.Value` when passing anonymous object parameters.
