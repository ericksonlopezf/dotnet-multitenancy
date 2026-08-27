# ADR-D04: Rejection of Permissive Cross-Tenant Fallback

## Status
Rejected (Discard Record)

## Context
Some multi-tenancy frameworks allow falling back to a "Default Tenant" or "System Tenant" whenever an incoming HTTP request fails to provide valid tenant resolution credentials (e.g., missing header, missing subdomain, or invalid token).

## Rationale for Rejection
1. **Security Vulnerability (Privilege Escalation)**: Falling back to a default tenant allows unauthorized clients to access default shared data or trigger background operations in an unintended default context.
2. **Masking Configuration and Routing Errors**: When client routing is misconfigured (e.g., typo in subdomain or malformed token), permissive fallback masks the error by returning 200 OK with default data instead of failing immediately.
3. **Data Pollution in Shared Schema**: New records created during unresolved requests end up assigned to the default tenant ID, polluting production databases.

## Enforced Alternative
1. **Fail-Closed Resolution**: When a tenant is required but cannot be unambiguously resolved, the system returns `Result.Failure(TenantErrors.Unresolved)` and terminates the HTTP pipeline with `400 Bad Request` or `404 Not Found`.
2. **Explicit Bypass Attributes**: In scenarios where an endpoint is intentionally tenant-agnostic (e.g., `/health`, `/swagger`), developers must explicitly decorate the endpoint with `[AllowAnonymousTenant]`, rather than relying on automatic ambient fallback.
