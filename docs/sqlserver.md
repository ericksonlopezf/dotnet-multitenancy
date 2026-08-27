# Microsoft SQL Server Multi-Tenancy Guide — EricksonLopez.MultiTenancy

> **Package:** `EricksonLopez.MultiTenancy.SqlServer`  
> **Driver:** `Microsoft.Data.SqlClient`

This guide explains how to implement tenant isolation in Microsoft SQL Server using `SESSION_CONTEXT` and native SQL Server Row-Level Security (RLS) security policies.

---

## 1. SQL Server `SESSION_CONTEXT` & Security Policies

SQL Server provides `SESSION_CONTEXT` as a thread-safe, session-scoped key-value store. When paired with inline table-valued security predicate functions and security policies, SQL Server enforces row-level security automatically.

```sql
-- Step 1: Create inline security predicate function
CREATE SCHEMA sec;
GO

CREATE FUNCTION sec.fn_tenant_security_predicate(@TenantId UNIQUEIDENTIFIER)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN (
    SELECT 1 AS fn_access
    WHERE @TenantId = CAST(SESSION_CONTEXT(N'tenant_id') AS UNIQUEIDENTIFIER)
);
GO

-- Step 2: Create and enable Security Policy
CREATE SECURITY POLICY sec.TenantSecurityPolicy
ADD FILTER PREDICATE sec.fn_tenant_security_predicate(tenant_id) ON dbo.invoices,
ADD BLOCK PREDICATE sec.fn_tenant_security_predicate(tenant_id) ON dbo.invoices AFTER INSERT,
ADD BLOCK PREDICATE sec.fn_tenant_security_predicate(tenant_id) ON dbo.invoices AFTER UPDATE
WITH (STATE = ON);
GO
```

---

## 2. C# Code Implementation

`EricksonLopez.MultiTenancy.SqlServer` provides transaction helpers to atomically set `SESSION_CONTEXT` before executing queries:

```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.SqlServer;
using Microsoft.Data.SqlClient;

public class SqlServerInvoiceRepository
{
    private readonly SqlConnection _connection;
    private readonly ITenantContext _tenantContext;

    public SqlServerInvoiceRepository(SqlConnection connection, ITenantContext tenantContext)
    {
        _connection = connection;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesAsync()
    {
        // Executes EXEC sp_set_session_context @key=N'tenant_id', @value=@TenantId atomically
        await using var transaction = await _connection.BeginTenantTransactionAsync(_tenantContext);

        var invoices = await _connection.QueryAsync<Invoice>(
            "SELECT * FROM dbo.invoices",
            transaction: transaction);

        await transaction.CommitAsync();
        return invoices.ToList();
    }
}
```

---

## 3. Connection Pooling Reset Invariant

Because physical SQL connections are recycled by the ADO.NET connection pool, `SESSION_CONTEXT` must be explicitly cleared or overwritten. The `ITenantTransaction` wrapper guarantees that upon transaction disposal, `sp_set_session_context @key=N'tenant_id', @value=NULL` is executed, preventing context leakage to subsequent requests.
