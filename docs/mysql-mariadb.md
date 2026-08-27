# MySQL & MariaDB Multi-Tenancy Guide — EricksonLopez.MultiTenancy

> **Packages:** `EricksonLopez.MultiTenancy.MySql`, `EricksonLopez.MultiTenancy.MariaDb`  
> **Driver:** `MySqlConnector`

---

## 1. Isolation Mechanism

MySQL and MariaDB do not provide native Row Level Security (RLS) policies like PostgreSQL or Oracle VPD. Instead, multi-tenant isolation is achieved by setting user-defined session variables (`@app_tenant_id`) within explicit transactions and pairing them with tenant-scoped SQL views or stored routines.

```sql
-- Step 1: Set session variable within active transaction
SET @app_tenant_id = '11111111-1111-1111-1111-111111111111';

-- Step 2: Query views filtered by session variable
CREATE VIEW v_invoices AS
SELECT * FROM invoices
WHERE tenant_id = @app_tenant_id;
```

---

## 2. Code Implementation

```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.MySql; // Or MariaDb
using MySqlConnector;

public class MySqlInvoiceService
{
    private readonly MySqlConnection _connection;
    private readonly ITenantContext _tenantContext;

    public MySqlInvoiceService(MySqlConnection connection, ITenantContext tenantContext)
    {
        _connection = connection;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesAsync()
    {
        // Executes SET @app_tenant_id = @TenantId within an explicit transaction
        await using var transaction = await _connection.BeginTenantTransactionAsync(_tenantContext);

        var invoices = await _connection.QueryAsync<Invoice>(
            "SELECT * FROM invoices WHERE tenant_id = @app_tenant_id",
            transaction: transaction);

        await transaction.CommitAsync();
        return invoices.ToList();
    }
}
```

---

## 3. Connection Pooling Safety

When using `MySqlConnector`, session variables persist on physical pooled connections unless explicitly reset or closed. The transaction wrapper guarantees that upon transaction disposal, `@app_tenant_id` is cleared to prevent cross-tenant state leakage.
