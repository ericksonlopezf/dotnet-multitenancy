# Architectural Boundary Specification: EricksonLopez.MultiTenancy

> **Note:** This file is a high-level reference document. The full architectural boundary specification, package dependency graph, and layer responsibility table are maintained in [`architecture.md`](architecture.md).

## 1. Purpose & Scope
`EricksonLopez.MultiTenancy` defines the foundational contracts, primitives, and enforcement mechanisms for multi-tenant SaaS architectures in .NET 8/9/10, guaranteeing fail-closed tenant resolution, Scoped context safety, and database-level isolation.

---

## 2. Layered Responsibilities

| Layer | Package(s) | Primary Responsibilities | Allowed Dependencies | Forbidden Dependencies |
| :--- | :--- | :--- | :--- | :--- |
| **L0: Contracts** | `EricksonLopez.MultiTenancy.Abstractions` | `TenantId`, `ITenantInfo`, `ITenantContext`, `ITenantResolver`, `ITenantScope`, `ITenantStore`, `TenantErrors` | .NET BCL, `EricksonLopez.Result` | Database SDKs, ASP.NET Core Http |
| **L1: Core Engine** | `EricksonLopez.MultiTenancy` | `ScopedTenantContextAccessor`, `DefaultTenantScopeFactory`, `InMemoryTenantStore`, `CachedTenantStore`, `HttpRemoteTenantStore`, health checks | `Abstractions`, `Microsoft.Extensions.*` | Database drivers, Web hosting |
| **L2: Analyzers** | `EricksonLopez.MultiTenancy.Analyzers` | Roslyn analyzers `ELMT001`, `ELMT002`, `ELMT003` for lifecycle safety | Roslyn SDK (`netstandard2.0`) | Runtime dependencies |
| **L3: Web Hosting** | `EricksonLopez.MultiTenancy.AspNetCore`, `Authentication` | Middleware, strategies (claims, host, route, header, basepath), endpoint filters, per-tenant options/auth | `Abstractions`, `Microsoft.AspNetCore.*` | Direct database drivers |
| **L4: Data Integration** | `EricksonLopez.MultiTenancy.Dapper` | Parameter helpers (`WithTenant`, `CreateTenantParameters`) | `Abstractions`, `Dapper` | Concrete DB drivers |
| **L5: DB Dialects** | `PostgreSql`, `SqlServer`, `MySql`, `MariaDb`, `Oracle`, `Sqlite` | Dialect-specific session variables, `SET LOCAL`, RLS policies, connection factories | `Abstractions`, `Dapper`, Concrete DB SDK | ASP.NET Core Http |
| **L6: Telemetry** | `EricksonLopez.MultiTenancy.OpenTelemetry` | Distributed tracing (`TenantActivitySource`), metrics (`TenantMetrics`), W3C Baggage | `Abstractions`, `OpenTelemetry.Api` | Database drivers |
| **L7: Testing** | `EricksonLopez.MultiTenancy.Testing` | Fakes (`FakeTenantStore`, `FakeTenantResolutionStrategy`), `TenantContextBuilder`, `FakeDbInfrastructure` | `Abstractions`, `Core` | None (Test harness only) |

---

## 3. Core Architectural Invariants

1. **Write-Once Scoped Accessor:** `ITenantContextAccessor` is Scoped to the request/job lifetime. Re-assignment within an active scope throws `InvalidOperationException`.
2. **Claims-First Precedence:** Authenticated JWT claims always override client-controlled HTTP headers. Any conflict aborts the request immediately (fail-closed, ADR-008).
3. **Transaction-Scoped Database Isolation:** In connection pooled environments, database tenant context must use `SET LOCAL` within a transaction to guarantee zero state leakage across recycled connections (ADR-009).
4. **Explicit SQL Parameterization:** Queries must explicitly include `WHERE tenant_id = @TenantId`. Implicit SQL rewriting is strictly rejected (ADR-007).
5. **Native AOT & Trimming Support:** All public APIs and stores are verified for Native AOT without runtime reflection or dynamic code generation.
