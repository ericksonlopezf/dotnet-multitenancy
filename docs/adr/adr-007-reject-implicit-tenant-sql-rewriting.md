# ADR-007: REJECT — Implicit Tenant SQL Rewriting in SqlBuilder/DapperExtensions

## Status
Accepted

## Date
2026-09-04

## Context
Automatic insertion of `WHERE tenant_id = @CurrentTenant` into SQL queries by `EricksonLopez.SqlBuilder` or `EricksonLopez.DapperExtensions` was evaluated. The idea: whenever a multi-tenant table is queried, the builder automatically appends the tenant filter.

## Decision
Rejected. SQL Builder and Dapper extensions must not silently insert tenant filters.

Explicit SQL always wins:
```sql
-- CORRECT: Developer writes this explicitly
SELECT id, name FROM invoices WHERE tenant_id = @TenantId AND status = 'pending';

-- WRONG: Library silently rewrites to this
SELECT id, name FROM invoices WHERE status = 'pending';
-- → becomes → SELECT id, name FROM invoices WHERE status = 'pending' AND tenant_id = @TenantId;
```

## Why
1. **Invisible magic in security-critical code is dangerous.** If the rewriting has a bug (wrong table name heuristic, missed join, CTE not covered), cross-tenant data leaks silently.
2. **Impossible to audit.** The SQL in the repository does not match what executes in the database. Security reviews become unreliable.
3. **RLS already handles this at the database level.** PostgreSQL RLS with `USING (tenant_id = current_setting(...))` provides transparent filtering that cannot be disabled from application code. Duplicating this in the SQL builder adds complexity without adding security.
4. **False confidence in un-RLS'd environments.** If an application relies on SQL rewriting without RLS, any query that bypasses the builder (raw SQL, stored procedures, migrations) silently exposes cross-tenant data.
5. **Testing difficulty.** Tests must verify that the correct WHERE clause was generated, not just that results were filtered — this is fragile.

## Security Impact
**Positive.** Explicit SQL is auditable, reviewable, and testable. Silent SQL rewriting creates hidden security surface that is difficult to review.

## Architecture Impact
Repositories are responsible for including `WHERE tenant_id = @TenantId` in all tenant-scoped queries. This is an explicit contract, not an implicit convention.

## Performance Impact
None — explicit WHERE clauses are no different from silently injected ones.

## AOT Impact
None — reflection-based SQL parsing would be AOT-incompatible anyway.

## Migration
N/A — prevents a feature, does not remove an existing one.

## Related Components
- `EricksonLopez.SqlBuilder` — no tenant-aware magic
- `EricksonLopez.DapperExtensions` — parameter helpers only (WithTenant), not SQL rewriting
- Repository pattern in application — must include explicit WHERE clauses
