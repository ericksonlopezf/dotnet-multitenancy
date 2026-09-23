# ADR-006: SCOPE — Database-Per-Tenant Not a Core Feature

## Status
Accepted

## Date
2026-09-04

## Context
`ITenantInfo.ConnectionString` implies that database-per-tenant is the primary deployment model. Several features (connection switching, per-tenant schema creation, per-tenant migrations) were considered as core library capabilities.

## Decision
Database-per-tenant is supported as an optional pattern via the `ConnectionString` property on `ITenantInfo`, but is NOT the recommended model and is NOT implemented as a core feature.

The recommended model is: **shared database + schema + PostgreSQL RLS** for tenant isolation.

The library does not implement:
- Connection switching infrastructure
- Per-tenant schema creation or migrations
- Per-tenant connection pool management

## Why
1. **Shared database + RLS is more operationally efficient** — fewer connection pools, simpler operations, standard PostgreSQL tooling.
2. **Database-per-tenant adds complexity** — connection routing, per-tenant migrations, connection pool fragmentation.
3. **RLS provides equivalent security guarantees** with significantly less operational overhead.
4. **Out of scope** — connection management and provisioning belong to Application and Infrastructure layers, not the multi-tenancy abstraction library.

## Security Impact
Neutral. Database-per-tenant provides stronger physical isolation but at higher operational cost. Shared database + `FORCE ROW LEVEL SECURITY` provides equivalent logical isolation for most threat models.

## Architecture Impact
Applications that need database-per-tenant can implement their own `ITenantConnectionContext` that switches connections based on `ITenantInfo.ConnectionString`. The library does not prevent this.

## Performance Impact
Positive (for shared-DB model) — no connection switching overhead.

## AOT Impact
None.

## Migration
N/A — this ADR prevents over-scoping, not a removal.

## Related Components
- `ITenantInfo.ConnectionString` — remains as optional field
- `EricksonLopez.MultiTenancy.PostgreSql` — implements shared-DB + RLS model
