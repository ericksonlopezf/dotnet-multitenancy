# ADR-001: REMOVE — EricksonLopez.MultiTenancy.EntityFrameworkCore

## Status
Accepted

## Context
The `EricksonLopez.MultiTenancy` ecosystem targets Dapper, raw SQL, and PostgreSQL. A `MultiTenantDbContext` was added that wraps Entity Framework Core with automatic global query filters and automatic `TenantId` stamping on `SaveChanges`.

This package:
- Uses `BindingFlags.NonPublic | BindingFlags.Instance` reflection via `MakeGenericMethod` to configure global query filters
- Declares `IsAotCompatible = false` and suppresses AOT warnings
- Relies on `UseInMemoryDatabase` in tests, which does not exercise real PostgreSQL semantics or RLS
- Implements application-layer-only isolation that can be bypassed by `.IgnoreQueryFilters()`
- Adds Entity Framework Core as a dependency to a Dapper/SQL-targeted library
- Creates architectural confusion in a library that explicitly targets Dapper/SQL/PostgreSQL

## Decision
The `EricksonLopez.MultiTenancy.EntityFrameworkCore` project is permanently removed from the solution, the solution file, and the repository.

## Why
1. **Wrong stack:** The ecosystem uses Dapper + SQL. EF Core is not in the target.
2. **False security:** Global query filters with `.IgnoreQueryFilters()` bypass capability provide the illusion of isolation, not actual isolation. This is more dangerous than no filter.
3. **AOT incompatible:** `MakeGenericMethod` via reflection is fundamentally incompatible with NativeAOT trimming.
4. **Tests prove nothing:** Tests use `UseInMemoryDatabase` — not real PostgreSQL, no RLS, no actual isolation guarantee.
5. **Maintenance burden:** Dual persistence strategies require dual verification paths with no ecosystem benefit.

## Alternatives Considered
- **Keep as optional, "not recommended":** Rejected — optional but present security packages get used incorrectly.
- **Fix to remove reflection:** Rejected — EF Core global filters fundamentally require reflection-based type discovery.
- **Restrict to non-security use:** Rejected — cannot control downstream use, creates liability.

## Security Impact
**Positive.** Removes a false security mechanism that could breed overconfidence and lead to unprotected tenant data.

## Architecture Impact
Medium breaking change. Applications using `MultiTenantDbContext` must migrate to Dapper + `ITenantConnectionContext` + PostgreSQL RLS (recommended) or implement their own EF Core tenant filtering.

## Performance Impact
Positive — removes EF Core from the dependency graph for all consumers of the multi-tenancy library.

## AOT Impact
Positive — the EFCore package was the only component with `IsAotCompatible = false`. After removal, the entire library is NativeAOT-compatible.

## Migration
1. Remove `<PackageReference Include="EricksonLopez.MultiTenancy.EntityFrameworkCore" />`.
2. Migrate to `EricksonLopez.MultiTenancy.PostgreSql` + Dapper for real isolation.
3. Add PostgreSQL RLS policies: `ENABLE ROW LEVEL SECURITY`, `FORCE ROW LEVEL SECURITY`, `USING`, `WITH CHECK`.

## Related Components
- `EricksonLopez.MultiTenancy.PostgreSql` (new — real RLS enforcement)
- `EricksonLopez.MultiTenancy.Dapper` (Dapper integration helpers)
