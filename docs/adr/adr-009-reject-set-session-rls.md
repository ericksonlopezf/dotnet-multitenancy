# ADR-009: REJECT — SET SESSION for Row Level Security

## Status
Accepted

## Context
When implementing PostgreSQL Row Level Security (RLS) integration, a mechanism must be chosen to bind the current application tenant context to a session variable that the PostgreSQL policies can evaluate (e.g. `current_setting('app.current_tenant_id')`).

Two primary mechanisms exist in PostgreSQL:
1. `SET SESSION app.current_tenant_id = '...'` (persists for the entire connection lifespan)
2. `SET LOCAL app.current_tenant_id = '...'` (persists only for the current transaction block)

The use of `SET SESSION` was evaluated as a way to avoid wrapping every request in an explicit database transaction.

## Decision
Permanently rejected. The library and all extensions must NEVER use `SET SESSION` to configure tenant variables.

All RLS context binding must be executed using `SET LOCAL` within an explicitly opened transaction (e.g., via `BeginTenantTransactionAsync`).

## Why
1. **Connection Pool Leakage (Critical Vulnerability):** When using `SET SESSION` with standard ADO.NET / Npgsql connection pooling, the session variable persists even after the connection is closed and returned to the pool. When another user receives the same physical connection from the pool, they inherit the previous user's tenant context. This results in silent, catastrophic cross-tenant data leakage.
2. **Transaction Scoping Guarantees Safety:** `SET LOCAL` is strictly scoped to the transaction block. When a transaction ends (either via Commit or Rollback), PostgreSQL automatically clears the variable. The connection is guaranteed to be clean when returned to the pool.
3. **Implicit vs Explicit:** Forcing `SET LOCAL` requires developers to explicitly wrap tenant operations in transactions, which is a best practice for data integrity and ensures the boundaries of tenant context are clearly defined in code.

## Alternatives Considered
- **SET SESSION with explicit RESET:** Using `SET SESSION` and requiring developers to call `RESET app.current_tenant_id` or overriding connection `Dispose` to clean up. Rejected because it relies on "perfect execution" by developers. If an exception prevents the `RESET` command from executing, the connection pool is poisoned. `SET LOCAL` guarantees cleanup even on fatal exceptions.

## Security Impact
**Critical Positive.** Eliminates a major class of cross-tenant data leakage vulnerability caused by connection pool poisoning.

## Architecture Impact
Applications must use transactions for any queries that require RLS. `EricksonLopez.MultiTenancy.PostgreSql` provides `BeginTenantTransactionAsync` to atomically start a transaction and apply the `SET LOCAL` variable.

## Performance Impact
Negligible overhead from requiring transactions. In standard Npgsql usage, lightweight transactions are extremely fast. The security guarantee vastly outweighs the minor latency of explicit transactions.

## AOT Impact
None.

## Migration
N/A — this ADR formalizes a rejection that prevents an unsafe pattern. The library already uses `SET LOCAL`.

## Related Components
- `EricksonLopez.MultiTenancy.PostgreSql.PostgreSqlRlsExtensions` (enforces transaction requirement for `SetTenantRlsContextAsync`)
- `RLS.md` documentation guide.
