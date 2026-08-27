# Database Dialects & Multi-Engine Support — EricksonLopez.MultiTenancy

`EricksonLopez.MultiTenancy` provides first-class support for six relational database engines. Each database package implements engine-native tenant isolation mechanisms while preserving connection pooling safety.

---

## 1. Engine Comparison Matrix

| Database Engine | Package | Primary Isolation Mechanism | Session Context Command | Connection Pool Safety |
| :--- | :--- | :--- | :--- | :--- |
| **PostgreSQL** | `EricksonLopez.MultiTenancy.PostgreSql` | Row Level Security (RLS) | `SET LOCAL app.current_tenant_id = :id` | :white_check_mark: Transaction-scoped (`SET LOCAL` resets on commit/rollback) |
| **SQL Server** | `EricksonLopez.MultiTenancy.SqlServer` | `SESSION_CONTEXT` & Security Policies | `sp_set_session_context 'tenant_id', @id` | :white_check_mark: Explicit reset in transaction wrapper |
| **MySQL** | `EricksonLopez.MultiTenancy.MySql` | User Session Variables | `SET @app_tenant_id = @id` | :white_check_mark: Reset via transaction cleanup |
| **MariaDB** | `EricksonLopez.MultiTenancy.MariaDb` | User Session Variables | `SET @app_tenant_id = @id` | :white_check_mark: Reset via transaction cleanup |
| **Oracle** | `EricksonLopez.MultiTenancy.Oracle` | Virtual Private Database (VPD) | `DBMS_SESSION.SET_IDENTIFIER(:id)` | :white_check_mark: Reset via client identifier cleanup |
| **SQLite** | `EricksonLopez.MultiTenancy.Sqlite` | Database-Per-Tenant / Temp Table | Dynamic Connection Switching | :white_check_mark: Connection lifecycle bound to tenant |

---

## 2. Dialect Implementations

### 1. PostgreSQL (`EricksonLopez.MultiTenancy.PostgreSql`)
- **Driver:** `Npgsql`
- **Mechanism:** PostgreSQL Row Level Security (RLS) enforced via `SET LOCAL`.
- **Key Methods:**
  - `connection.BeginTenantTransactionAsync(tenantContext)`
  - `connection.SetTenantRlsContextAsync(tenantId, transaction)`
- **Detailed Guide:** See [PostgreSQL RLS Guide](rls.md).

---

### 2. Microsoft SQL Server (`EricksonLopez.MultiTenancy.SqlServer`)
- **Driver:** `Microsoft.Data.SqlClient`
- **Mechanism:** `SESSION_CONTEXT` combined with SQL Server Security Policies / Predicates.
- **Key Methods:**
  - `connection.BeginTenantTransactionAsync(tenantContext)`
  - `connection.SetTenantSessionContextAsync(tenantId, transaction)`
- **Detailed Guide:** See [SQL Server Multi-Tenancy Guide](sqlserver.md).

---

### 3. MySQL & MariaDB (`EricksonLopez.MultiTenancy.MySql`, `MariaDb`)
- **Driver:** `MySqlConnector`
- **Mechanism:** User-defined session variables (`@app_tenant_id`) evaluated in SQL views or stored routines.
- **Key Methods:**
  - `connection.BeginTenantTransactionAsync(tenantContext)`
  - `connection.SetTenantSessionVariableAsync(tenantId, transaction)`
- **Detailed Guide:** See [MySQL & MariaDB Guide](mysql-mariadb.md).

---

### 4. Oracle (`EricksonLopez.MultiTenancy.Oracle`)
- **Driver:** `Oracle.ManagedDataAccess.Core`
- **Mechanism:** Oracle Virtual Private Database (VPD) policies using `SYS_CONTEXT('USERENV', 'CLIENT_IDENTIFIER')`.
- **Key Methods:**
  - `connection.BeginTenantTransactionAsync(tenantContext)`
  - `connection.SetTenantVpdContextAsync(tenantId, transaction)`
- **Detailed Guide:** See [Oracle VPD Guide](oracle.md).

---

### 5. SQLite (`EricksonLopez.MultiTenancy.Sqlite`)
- **Driver:** `Microsoft.Data.Sqlite`
- **Mechanism:** Database-per-tenant file provisioning (`tenants/{tenantId}.db`) or session-isolated temporary tables.
- **Key Types:**
  - `ISqliteTenantConnectionFactory` & `SqliteTenantConnectionFactory`
  - `connection.BeginTenantTransactionAsync(tenantContext)`
- **Detailed Guide:** See [SQLite Multi-Tenancy Guide](sqlite.md).
