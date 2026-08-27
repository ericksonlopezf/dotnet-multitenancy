# SQLite Multi-Tenancy Guide — EricksonLopez.MultiTenancy

> **Package:** `EricksonLopez.MultiTenancy.Sqlite`  
> **Driver:** `Microsoft.Data.Sqlite`

This guide explains how to implement multi-tenancy with SQLite using either the **Database-per-Tenant** model or the **Single Database with Temp Table Context** model.

---

## 1. Database-Per-Tenant Model (Recommended for SQLite)

Because SQLite is an embedded, file-based database, the most secure isolation model is to provision a separate `.db` file for each tenant.

### `ISqliteTenantConnectionFactory`
The `SqliteTenantConnectionFactory` dynamically constructs connection strings routing queries to tenant-specific database files:

```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Sqlite;
using Microsoft.Data.Sqlite;

public class SqliteOrderRepository
{
    private readonly ISqliteTenantConnectionFactory _connectionFactory;
    private readonly ITenantContext _tenantContext;

    public SqliteOrderRepository(ISqliteTenantConnectionFactory connectionFactory, ITenantContext tenantContext)
    {
        _connectionFactory = connectionFactory;
        _tenantContext = tenantContext;
    }

    public async Task<IEnumerable<Order>> GetOrdersAsync()
    {
        // Resolves or creates connection to 'Data Source=tenants/{tenantId}.db'
        await using var connection = _connectionFactory.CreateConnection(_tenantContext.TenantId);
        await connection.OpenAsync();

        return await connection.QueryAsync<Order>("SELECT * FROM orders");
    }
}
```

---

## 2. Single Database Model (Temporary Table Context)

If your architecture uses a shared SQLite database file, `EricksonLopez.MultiTenancy.Sqlite` provides session context helpers using connection-scoped temporary tables:

```csharp
using EricksonLopez.MultiTenancy.Sqlite;

await using var transaction = await connection.BeginTenantTransactionAsync(tenantContext);

// Injects tenant identifier into temp._current_tenant session table
var orders = await connection.QueryAsync<Order>(
    "SELECT * FROM orders WHERE tenant_id = (SELECT tenant_id FROM temp._current_tenant)",
    transaction: transaction);

await transaction.CommitAsync();
```
