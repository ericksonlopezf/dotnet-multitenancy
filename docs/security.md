# Multi-Tenancy Security Threat Model & Analysis — EricksonLopez.MultiTenancy

This document outlines the threat modeling, STRIDE analysis, attack vectors, and concrete technical mitigations implemented across the `EricksonLopez.MultiTenancy` architecture.

---

## 1. Threat Mitigation Matrix

| Threat (STRIDE) | Attack Vector | Mitigation Layer | Technical Mitigation Mechanism |
| :--- | :--- | :---: | :--- |
| **Tenant Spoofing** *(Spoofing)* | A malicious client sends an arbitrary tenant identifier in an HTTP header (`X-Tenant-ID`) to access another tenant's data. | **Application** | Enforce claims-first resolution. Authenticated JWT claims override client headers. Any mismatch triggers an immediate fail-closed conflict exception (ADR-008). |
| **Insecure Direct Object Reference (IDOR)** *(Elevation of Privilege)* | An authenticated user belonging to Tenant A requests a resource ID belonging to Tenant B (e.g. `/api/invoices/999`). | **Database** | PostgreSQL RLS filters rows based on the transaction-scoped `app.current_tenant_id`, returning zero rows (404 Not Found) even if requested by ID. |
| **Async Context Bleed** *(Information Disclosure)* | Ambient tenant state in static `AsyncLocal` slots bleeds across concurrent requests during thread pool thread recycling. | **Application** | Register `ITenantContextAccessor` with **Scoped** lifetime (ADR-002). Context lifetime is strictly bound to DI request scopes. |
| **Connection Pool Leakage** *(Information Disclosure)* | A physical database connection returned to the pool carries session state from a previous tenant transaction. | **Database / Infra** | Set tenant context exclusively via `SET LOCAL` within explicit database transactions (ADR-009). Variables reset automatically on commit/rollback. |
| **Empty/Null ID Bypass** *(Elevation of Privilege)* | An attacker supplies `TenantId = null` or `Guid.Empty` hoping to bypass validation query filters. | **Application** | `TenantId.Create()` and `TenantContext` enforce non-empty invariants. Passing empty IDs throws domain validation errors (ADR-004). |
| **Platform Admin Abuse** *(Elevation of Privilege)* | An administrative user performs cross-tenant mutations without auditing or explicit scope validation. | **Application** | Cross-tenant actions require an explicit administrative context and dedicated elevated database roles (`BYPASSRLS`) with immutable auditing. |

---

## 2. 4-Layer Defense-in-Depth Verification

```
Incoming Request
       │
       ▼
[ Layer 1: Identity & Claims Validation ] ──► Validates cryptographic JWT claims
       │
       ▼
[ Layer 2: Scoped Context Accessor ]     ──► Write-once per DI scope (No static leakage)
       │
       ▼
[ Layer 3: Explicit Dapper Parameters ]  ──► Queries include WHERE tenant_id = @TenantId
       │
       ▼
[ Layer 4: PostgreSQL RLS / SET LOCAL ]  ──► Database enforces FORCE ROW LEVEL SECURITY
```

---

## 3. Compliance and Security Audit Checklist

- [x] All tenant-partitioned database tables have `ENABLE ROW LEVEL SECURITY` and `FORCE ROW LEVEL SECURITY`.
- [x] Application connects via an unprivileged role (`app_user`) — never superuser (`postgres`).
- [x] Database transactions use `SET LOCAL` rather than `SET SESSION`.
- [x] Resolution pipeline fails closed on conflicting tenant inputs.
- [x] `ITenantContextAccessor` is registered as Scoped.
