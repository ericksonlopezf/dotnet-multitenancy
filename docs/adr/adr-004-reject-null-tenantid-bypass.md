# ADR-004: REJECT — Null/Empty TenantId as Platform Bypass Mechanism

## Status
Accepted

## Date
2026-09-04

## Context
Using `TenantId.Empty` or `null` TenantId as a convention to indicate "platform admin context" or "no tenant filter" was considered. This pattern appears in several multi-tenant frameworks and ORMs.

## Decision
Permanently rejected. `TenantId.Empty` and `null` TenantId mean exactly one thing: the tenant has not been resolved. They never mean "elevated", "platform", or "bypass".

For platform administration (cross-tenant operations), an explicit `PlatformTenantContext` type must be created with:
- An explicit authorization requirement before creation
- An audit record for every creation
- A separate code path that does not pass through normal tenant-scoped infrastructure

## Why
1. **Null propagation is a bug magnet.** If empty TenantId is a valid bypass, then any code that accidentally fails to set the TenantId (uninitialized field, null parameter, default struct value) silently bypasses isolation.
2. **Impossible to audit.** A null TenantId in a query log could be a platform operation OR a bug. They must be distinguishable.
3. **Defense in depth breaks.** If RLS is configured to allow `NULL = current_setting(...)` to match all rows, then any connection where the setting is not set accesses all tenants' data.
4. **Confused deputy attacks.** Services that accept "empty = bypass" are vulnerable to any caller who can pass an empty tenant identifier.

## Alternatives Considered
- **TenantId.Platform sentinel value:** Better than null, but still a string-valued sentinel that could be accidentally used. Rejected in favor of an explicit type.
- **Separate IPlatformContext interface:** This is the accepted solution.

## Security Impact
**Critical positive.** Eliminates T13 (null bypass) and T14 (platform admin abuse) from the threat model. Any `TenantId.Empty` that reaches the database layer is now an error, not a bypass.

## Architecture Impact
Applications that used empty TenantId for platform operations must create an explicit platform context mechanism. This is a larger design effort but the correct approach.

## Performance Impact
None.

## AOT Impact
None.

## Migration
1. Audit all code paths where `TenantId.Empty` or `TenantId.IsEmpty` is used as a success condition.
2. Replace platform admin patterns with an explicit `IPlatformAdminContext` type.
3. Require explicit authorization (e.g., platform admin role claim) before creating platform context.
4. Ensure all platform context creations are logged to the audit trail.

## Related Components
- `TenantContext<T>.IsResolved` — returns `false` for empty TenantId
- PostgreSQL RLS policies — configured to reject when `current_setting('app.current_tenant_id', true)` is NULL
- Repository base classes — must reject operations when context is not resolved
