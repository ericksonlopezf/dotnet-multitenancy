# Architectural Decision Records (ADRs) Index

This directory contains the formal Architectural Decision Records (ADRs) and Systematic Discard Records governing the `EricksonLopez.MultiTenancy` ecosystem.

> **Target Frameworks**: Production libraries explicitly target `.NET 8.0 (LTS)` and `.NET 9.0` (STS), with validated full compatibility for `.NET 10.0 Native AOT` compilation.

---

## 1. Accepted Architectural Decisions

| ADR | Title | Status | Date | Key Decision |
| :--- | :--- | :---: | :---: | :--- |
| **[ADR-001](adr-001-remove-entityframeworkcore-package.md)** | Remove EntityFrameworkCore Package | **Accepted** | 2026-08-21 | Removed EF Core package to support Native AOT and enforce database RLS over application filters. |
| **[ADR-002](adr-002-redesign-async-local-accessor.md)** | Redesign AsyncLocal Tenant Context Accessor | **Accepted** | 2026-08-21 | Replaced static `AsyncLocal` accessor with DI-scoped `ScopedTenantContextAccessor`. |
| **[ADR-003](adr-003-reject-header-first-resolution.md)** | Reject Header-First Tenant Resolution | **Accepted** | 2026-08-21 | Enforced JWT claims precedence over unauthenticated headers to eliminate tenant spoofing. |
| **[ADR-004](adr-004-reject-null-tenantid-bypass.md)** | Reject Null/Empty TenantId Platform Bypass | **Accepted** | 2026-08-21 | Banned null/empty IDs as bypass mechanisms; cross-tenant operations require explicit contexts. |
| **[ADR-005](adr-005-reject-automatic-global-tenant-filters.md)** | Reject Automatic Global Tenant Filters | **Accepted** | 2026-08-21 | Mandated explicit SQL filters combined with database-level RLS. |
| **[ADR-006](adr-006-scope-database-per-tenant-not-core.md)** | Scope Database-Per-Tenant Not a Core Feature | **Accepted** | 2026-08-21 | Delegated DB-per-tenant routing to application layer; core focuses on shared DB + RLS. |
| **[ADR-007](adr-007-reject-implicit-tenant-sql-rewriting.md)** | Reject Implicit Tenant SQL Rewriting | **Accepted** | 2026-08-21 | Banned runtime SQL parsing and rewriting to ensure zero-allocation performance and auditable SQL. |
| **[ADR-008](adr-008-adopt-fail-closed-tenant-resolution-conflict-detection.md)** | Adopt Fail-Closed Resolution Conflict Detection | **Accepted** | 2026-08-21 | Mandated that conflicting strategy resolutions fail closed immediately with HTTP 400. |
| **[ADR-009](adr-009-reject-set-session-rls.md)** | Reject SET SESSION for Row Level Security | **Accepted** | 2026-08-21 | Banned session-level SET; enforced transaction-scoped `SET LOCAL` to eliminate pool leakage. |
| **[ADR-010](adr-010-per-tenant-service-provider-isolation.md)** | Per-Tenant Service Provider Isolation | **Accepted** | 2026-08-27 | Immutable root container; isolated scoped resolution per tenant request. |
| **[ADR-011](adr-011-native-aot-and-trimming-invariants.md)** | Native AOT and Trimming Invariants | **Accepted** | 2026-08-27 | Enforce zero dynamic reflection, `IsAotCompatible=true`, and continuous CI smoke testing. |
| **[ADR-012](adr-012-observability-tenant-enrichment.md)** | Observability & OpenTelemetry Tenant Enrichment | **Accepted** | 2026-08-27 | Semantic OpenTelemetry baggage and metric tagging with strict PII prevention. |

---

## 2. Systematic Discard Records (Rejected Non-Goals)

| Record | Title | Status | Date | Rationale |
| :--- | :--- | :---: | :---: | :--- |
| **[ADR-D01](adr-d01-no-dynamic-sql-string-rewriting.md)** | Rejection of Dynamic SQL Text Rewriting | **Rejected** | 2026-08-27 | Avoids SQL injection vectors, massive string allocations, and parser fragility. |
| **[ADR-D02](adr-d02-no-unbounded-in-memory-tenant-caching.md)** | Rejection of Unbounded In-Memory Tenant Caching | **Rejected** | 2026-08-27 | Prevents memory exhaustion attacks (OOM) and stale tenant security invariants. |
| **[ADR-D03](adr-d03-no-ambient-thread-static-state.md)** | Rejection of Ambient ThreadStatic State | **Rejected** | 2026-08-27 | Avoids context loss across async continuations and thread-pool concurrency leaks. |
| **[ADR-D04](adr-d04-no-permissive-cross-tenant-fallback.md)** | Rejection of Permissive Cross-Tenant Fallback | **Rejected** | 2026-08-27 | Rejects fallback to 'default' tenant upon resolution failure to prevent privilege escalation. |
| **[REJECT-007](reject-007-tenant-id-implicit-threading-async-local-mutation.md)** | Reject Implicit AsyncLocal Mutation | **Rejected** | 2026-08-21 | Background tasks must spawn dedicated DI scopes via `ITenantScopeFactory`. |
