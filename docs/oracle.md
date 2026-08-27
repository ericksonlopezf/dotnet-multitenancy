# Oracle Database Multi-Tenancy Guide (VPD) — EricksonLopez.MultiTenancy

> **Package:** `EricksonLopez.MultiTenancy.Oracle`  
> **Driver:** `Oracle.ManagedDataAccess.Core`

---

## 1. Oracle Virtual Private Database (VPD) Architecture

Oracle Database enforces row-level security through **Virtual Private Database (VPD)**, using fine-grained access control (FGAC) security policies linked to application context parameters.

`EricksonLopez.MultiTenancy.Oracle` sets the tenant identifier via `DBMS_SESSION.SET_IDENTIFIER`, which populates the native Oracle context variable `SYS_CONTEXT('USERENV', 'CLIENT_IDENTIFIER')`.

```sql
-- Step 1: Create VPD security predicate function
CREATE OR REPLACE FUNCTION tenant_security_predicate(
    p_schema IN VARCHAR2,
    p_table  IN VARCHAR2
) RETURN VARCHAR2 IS
BEGIN
    RETURN 'tenant_id = SYS_CONTEXT(''USERENV'', ''CLIENT_IDENTIFIER'')';
END;
/

-- Step 2: Attach VPD policy to tables
BEGIN
    DBMS_RLS.ADD_POLICY(
        object_schema   => 'APP_USER',
        object_name     => 'INVOICES',
        policy_name     => 'TENANT_ISOLATION_POLICY',
        function_schema => 'APP_USER',
        policy_function => 'TENANT_SECURITY_PREDICATE',
        statement_types => 'SELECT, INSERT, UPDATE, DELETE',
        update_check    => TRUE
    );
END;
/
```

---

## 2. Code Implementation

```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Oracle;
using Oracle.ManagedDataAccess.Client;

public class OracleInvoiceRepository
{
    private readonly OracleConnection _connection;
    private readonly ITenantContext _tenantContext;

    public OracleInvoiceRepository(OracleConnection connection, ITenantContext tenantContext)
    {
        _connection = connection;
        _tenantContext = tenantContext;
    }

    public async Task<IEnumerable<Invoice>> GetInvoicesAsync()
    {
        // Executes DBMS_SESSION.SET_IDENTIFIER(:tenantId) within an explicit transaction
        await using var transaction = await _connection.BeginTenantTransactionAsync(_tenantContext);

        var invoices = await _connection.QueryAsync<Invoice>(
            "SELECT * FROM invoices",
            transaction: transaction);

        await transaction.CommitAsync();
        return invoices;
    }
}
```

---

## 3. Connection Pool Safety

Oracle pooled connections maintain `CLIENT_IDENTIFIER` until overwritten or cleared. The `ITenantTransaction` disposal mechanism executes `DBMS_SESSION.CLEAR_IDENTIFIER` on rollback or completion to guarantee zero cross-tenant contamination.
