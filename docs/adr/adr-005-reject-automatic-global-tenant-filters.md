# ADR-005: REJECT — Automatic Global Tenant Filters as Primary Security Mechanism

## Status
Accepted

## Context
Automatic global query filters (as implemented in EF Core's `HasQueryFilter` or as a SqlBuilder feature) that silently append `WHERE tenant_id = @CurrentTenant` to all queries were evaluated as a primary or sole isolation mechanism.

## Decision
Rejected as a primary security mechanism. Automatic global filters may exist as a developer convenience layer but cannot substitute for PostgreSQL RLS.

The rule: **Application-layer filters are defense-in-depth helpers. PostgreSQL RLS is the enforcement layer.**

## Why
1. **Bypassable by design:** EF Core: `.IgnoreQueryFilters()`. SqlBuilder: any raw SQL execution. Stored procedures. Migrations. Bulk operations.
2. **Single point of failure:** If the filter application logic has a bug (wrong parameter name, wrong column, disabled in test mode), all tenant data is exposed.
3. **Does not protect at-rest data:** Direct database access (DBA, migration tools, other services) bypasses all application filters.
4. **Implicit SQL rewriting is a security risk:** It hides access patterns and makes security audits harder.
5. **Audit complexity:** When a query does not include the filter explicitly, it is impossible to tell from the SQL log whether isolation was applied.

## Security Impact
**Positive.** Moves security responsibility to the database where it cannot be bypassed by application code. PostgreSQL RLS with `FORCE ROW LEVEL SECURITY` provides guarantees that no amount of application-layer filtering can provide.

## Architecture Impact
Repositories must explicitly include `WHERE tenant_id = @TenantId` in all SQL (application-layer defense) AND rely on PostgreSQL RLS as the database-layer enforcement. The application filter provides early error detection; the RLS provides guaranteed enforcement.

## Performance Impact
Negligible. An explicit `WHERE tenant_id = $1` clause is no more expensive than an automatically injected one. Index on `tenant_id` is required regardless.

## AOT Impact
None.

## Migration
No migration needed — this ADR prevents a feature from being added, rather than removing an existing one.

For existing applications using automatic filters as their sole isolation: add PostgreSQL RLS policies.

## Related Components
- `EricksonLopez.SqlBuilder` — must NOT silently inject tenant filters
- `EricksonLopez.DapperExtensions` — must NOT silently inject tenant filters
- `EricksonLopez.MultiTenancy.PostgreSql` — provides RLS enforcement infrastructure
