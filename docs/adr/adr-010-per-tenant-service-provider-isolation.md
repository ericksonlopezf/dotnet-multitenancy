# ADR-010: Per-Tenant Service Provider Isolation

## Status
Accepted

## Context
In complex multi-tenant enterprise architectures, tenants frequently require tenant-specific implementations or options (e.g., custom payment gateways, tenant-specific API keys, or custom feature flags).

A common legacy pattern in .NET is to build a monolithic global `IServiceProvider` with dynamic keyed lookups, or continuously register new services into the root container during request execution. This leads to:
1. **Thread race conditions** during container mutation at runtime.
2. **Memory leaks** caused by holding root-level singletons tied to transient tenant scopes.
3. **Loss of Native AOT / Trimming guarantees** due to dynamic reflection lookups.

## Decision
We mandate that:
1. The **Root Container** remains immutable after application bootstrap.
2. Per-tenant service differentiation is achieved via **Scoped Service Resolution** and strongly typed options providers (`ITenantOptions<TOptions>` / `TenantConfigurationProvider`).
3. When isolated sub-containers are required (e.g., in `EricksonLopez.MultiTenancy.AspNetCore`), a lightweight child `IServiceScope` is created per request, seeded exclusively with the immutable root container registrations and tenant context.
4. Dynamic mutation of root service collections after `BuildServiceProvider()` is strictly prohibited.

## Consequences
### Positive
- Zero runtime synchronization locks when resolving services.
- Deterministic garbage collection when the request scope ends.
- 100% Native AOT compatibility without dynamic registration reflection.

### Negative
- Tenant-specific service definitions must be registered during application startup or dynamically resolved via typed factory strategies.
