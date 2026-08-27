# PostgreSQL Row Level Security (RLS) Guide — EricksonLopez.MultiTenancy

> **Package:** `EricksonLopez.MultiTenancy.PostgreSql`  
> **Driver:** `Npgsql`

This guide explains the architectural design, database policy patterns, and connection pooling safety invariants for enforcing tenant isolation via PostgreSQL Row Level Security (RLS).

---

## 1. RLS Database Policy Patterns

All tenant-partitioned tables must enable and force Row Level Security. Executing `FORCE ROW LEVEL SECURITY` ensures that even the table owner (which the application role often is) is strictly bound by RLS policies.

```sql
-- Step 1: Enable and FORCE RLS on all tenant tables
ALTER TABLE invoices ENABLE ROW LEVEL SECURITY;
ALTER TABLE invoices FORCE ROW LEVEL SECURITY;

-- Step 2: Create RESTRICTIVE policy for the application role
CREATE POLICY tenant_isolation ON invoices
    AS RESTRICTIVE
    FOR ALL
    TO app_user
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
```

### Policy Invariants:
1. **`AS RESTRICTIVE`:** Guarantees that this policy must be satisfied in conjunction with any additional permissive policies.
2. **`USING` & `WITH CHECK`:** The `USING` clause restricts row visibility for `SELECT`, `UPDATE`, and `DELETE`. The `WITH CHECK` clause validates data written during `INSERT` and `UPDATE`. Both must match.
3. **Missing Setting Safety:** `current_setting('app.current_tenant_id', true)` uses the `missing_ok = true` parameter. If the setting is unset (NULL), the expression evaluates to `NULL` (blocking all row access) rather than throwing an unhandled database exception.

---

## 2. Connection Pooling Safety Analysis

In high-concurrency .NET applications, database connections are managed via a physical connection pool.

### The Session State Leakage Threat (ADR-009)
If an application sets the tenant ID using session-level state (`SET app.current_tenant_id = '...'`), the setting persists on that physical connection. When the request finishes and returns the connection to the pool, the next request borrowing that connection will run under the previous tenant's identity, resulting in severe data leakage.

```
Request A (Tenant A)
  ├──► Open Connection
  ├──► SET app.current_tenant_id = 'tenant-A' (Session Level)
  ├──► Query Database
  └──► Return Connection to Pool (Setting persists!)

Request B (Tenant B)
  ├──► Get SAME Connection from Pool
  ├──► Error occurs before setting tenant_id
  └──► Query Database ──► LEAKS Tenant A's data!
```

### The Solution: Transaction-Scoped `SET LOCAL`
To eliminate session leakage, `EricksonLopez.MultiTenancy.PostgreSql` sets context exclusively using `SET LOCAL` within a transaction:

```sql
BEGIN;
SET LOCAL app.current_tenant_id = '11111111-1111-1111-1111-111111111111';
-- All queries inside this transaction are strictly isolated
COMMIT; -- Context is automatically wiped by PostgreSQL
```

`SET LOCAL` binds the variable strictly to the lifecycle of the transaction. Upon `COMMIT` or `ROLLBACK`, PostgreSQL automatically clears the variable, returning the connection to the pool in a clean state.

---

## 3. C# Code Implementation

```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.PostgreSql;
using Npgsql;

public class InvoiceRepository
{
    private readonly NpgsqlConnection _connection;
    private readonly ITenantContext _tenantContext;

    public InvoiceRepository(NpgsqlConnection connection, ITenantContext tenantContext)
    {
        _connection = connection;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesAsync()
    {
        // Atomically opens connection, begins transaction, and sets SET LOCAL app.current_tenant_id
        await using var transaction = await _connection.BeginTenantTransactionAsync(_tenantContext);

        var invoices = await _connection.QueryAsync<Invoice>(
            "SELECT * FROM invoices",
            transaction: transaction);

        await transaction.CommitAsync();
        return invoices.ToList();
    }
}
```

---

## 4. Critical Security Warnings

> [!CAUTION]
> **No Superuser Roles:** Never connect your application to PostgreSQL using a superuser account (such as `postgres`). Superusers automatically bypass all Row Level Security policies by default, completely disabling database-level isolation. Always create an unprivileged role (e.g. `app_user`).
