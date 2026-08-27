# Level 04: Shared Database Isolation via PostgreSQL RLS & SET LOCAL

Learn how to implement high-density multi-tenancy using PostgreSQL Row-Level Security (RLS) and transaction-scoped session variables.

## 1. PostgreSQL Schema & RLS Policy

```sql
-- Enable RLS on the orders table
ALTER TABLE orders ENABLE ROW LEVEL SECURITY;

-- Create tenant isolation policy
CREATE POLICY tenant_isolation_policy ON orders
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
```

## 2. C# Transaction-Scoped Session Setting (`SET LOCAL`)

To prevent connection-pool leakage in ADO.NET, tenant session variables must always be applied via `SET LOCAL` inside an active database transaction (ADR-009):

```csharp
using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

using var transaction = await connection.BeginTransactionAsync();

// Apply tenant context to the current transaction scope
await connection.SetTenantContextAsync(tenantContext.RequiredTenant.Id, transaction);

// Queries automatically filter to the current tenant!
var orders = await connection.QueryAsync<Order>(
    "SELECT * FROM orders WHERE status = @Status",
    new { Status = "Pending" },
    transaction);

await transaction.CommitAsync();
```
