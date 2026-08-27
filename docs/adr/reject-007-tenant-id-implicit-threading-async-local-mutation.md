# Architectural Decision Record: REJECT-007
## Rejection of Unsafe Mutable AsyncLocal Tenant Contexts Across Unbound Threads

### Status
**REJECTED (Permanent Directorial Invariant)**

### Context
Proposals were considered to allow background worker tasks to inherit mutable ambient tenant context without explicit scoped lifetime delegation.

### Decision
Permanently rejected. Multi-tenant isolation requires strict scoping via `ITenantContext` and `ITenantContextAccessor` managed through `IServiceScope` lifecycles to prevent cross-tenant data leaks in asynchronous thread pool executions.

### Consequences
- Guaranteed cross-tenant isolation and security compliance.
- No accidental tenant state bleed into asynchronous background tasks.
