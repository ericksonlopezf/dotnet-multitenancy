# ADR-003: REJECT — Header-First Tenant Resolution (Security Threat)

## Status
Accepted

## Context
The original `AddAspNetCoreMultiTenancy()` registers resolution strategies in this order:
1. `HeaderTenantResolutionStrategy` (X-Tenant-ID header)
2. `ClaimTenantResolutionStrategy` (JWT tenant_id claim)
3. `HostTenantResolutionStrategy` (subdomain)

The first non-empty result wins. This means a client-supplied `X-Tenant-ID` HTTP header can silently override an authenticated JWT claim, enabling trivial tenant spoofing.

Example attack:
```
JWT: { "sub": "user-123", "tenant_id": "legitimate-tenant" }
Header: X-Tenant-ID: victim-tenant
Result: Resolved tenant = "victim-tenant" ← WRONG
```

## Decision
Header-based resolution is **removed from default registration**. It is available as an opt-in extension for internal service-to-service communication only.

Default resolution order:
1. `ClaimTenantResolutionStrategy` (JWT) — highest trust
2. `RouteTenantResolutionStrategy` — URL structure
3. `HostTenantResolutionStrategy` — DNS

Header strategy: only available via explicit `services.AddInternalHeaderTenantResolution()`.

Additionally, when two strategies with different trust levels both resolve a tenant ID and they differ, the middleware MUST:
1. Log a security warning with both resolved values
2. Reject the request (return 400 or 401 depending on authentication state)
3. Never silently pick one value over the other

## Why
1. HTTP headers are client-controlled and trivially forgeable.
2. JWT claims have cryptographic integrity (server-signed).
3. Silently overriding a cryptographically-verified identity with a client-provided string is an elementary security mistake.
4. The "first match wins" pattern is dangerous when strategies are ordered by priority but can yield different results.

## Security Impact
**Critical positive.** Eliminates T01 (tenant spoofing via header) from the threat model. Any implementation that allowed header-before-JWT was vulnerable to trivial cross-tenant access.

## Architecture Impact
Breaking change. Any application that relied on `X-Tenant-ID` header for tenant resolution must:
1. Ensure the header is set by a trusted internal service (not by end users), AND
2. Call `services.AddInternalHeaderTenantResolution()` explicitly.

Applications relying on header resolution from end-user clients must migrate to JWT claim-based resolution.

## Performance Impact
None — no performance change from reordering strategy execution.

## AOT Impact
None.

## Migration
1. If your application uses header-based resolution from authenticated users: migrate to JWT tenant_id claims.
2. If your application uses header-based resolution for internal service calls: add `services.AddInternalHeaderTenantResolution()`.
3. Remove any dependency on `HeaderTenantResolutionStrategy` being included by default.

## Related Components
- `TenantResolutionMiddleware` (conflict detection added)
- `AspNetCoreMultiTenancyExtensions.AddAspNetCoreMultiTenancy()` (strategy list updated)
