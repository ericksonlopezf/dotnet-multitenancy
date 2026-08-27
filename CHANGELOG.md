# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [1.0.0] - 2026-08-27

### Added
- **Core Abstractions (`EricksonLopez.MultiTenancy.Abstractions`)**:
  - Strongly-typed `TenantId` readonly record struct backed by `Guid` with deterministic parsing, comparison, and `System.Text.Json` converter.
  - Core interfaces: `ITenantInfo`, `ITenantContext`, `ITenantContext<TTenant>`, `ITenantResolver`, `ITenantResolutionStrategy`, `ITenantStore`, `ITenantLookupStore`, `ITenantScope`, `ITenantScopeFactory`, `ITenantEntity`.
  - Domain errors and exceptions: `TenantErrors`, `TenantNotFoundException`, `TenantInactiveException`.
  - `TenantResolutionSource` audit enumeration.
- **Core Implementation (`EricksonLopez.MultiTenancy`)**:
  - `ScopedTenantContextAccessor` ensuring request-scoped write-once tenant context without ambient `AsyncLocal` context leakage.
  - `DefaultTenantScopeFactory` for creating isolated DI scopes for non-HTTP background jobs and message handlers.
  - In-memory tenant store (`InMemoryTenantStore<TTenant>`) for development and testing.
  - Caching decorator (`CachedTenantStore<TTenant>`) using `IMemoryCache` with expiration policies.
  - Upstream remote HTTP tenant store (`HttpRemoteTenantStore<TTenant>`).
  - Multi-tenancy health check integration (`MultiTenancyHealthCheck`).
- **Roslyn Analyzers (`EricksonLopez.MultiTenancy.Analyzers`)**:
  - `ELMT001`: Static tenant context field analyzer.
  - `ELMT002`: Captive tenant context in singleton service analyzer.
  - `ELMT003`: Dapper execution without tenant parameter analyzer.
- **ASP.NET Core Integration (`EricksonLopez.MultiTenancy.AspNetCore`)**:
  - `TenantResolutionMiddleware` orchestrating resolution strategies with fail-closed conflict detection (ADR-008).
  - Built-in resolution strategies: `ClaimTenantResolutionStrategy`, `HostNameTenantResolutionStrategy`, `RouteTenantResolutionStrategy`, `BasePathTenantResolutionStrategy`, `HeaderTenantResolutionStrategy`, `StaticTenantResolutionStrategy`, `DelegateTenantResolutionStrategy`.
  - Minimal API endpoint filter (`RequireTenantFilter`) and `.RequireTenant()` extension.
  - Per-tenant options cache (`TenantOptionsCache<TOptions, TTenant>`).
- **Per-Tenant Authentication (`EricksonLopez.MultiTenancy.Authentication`)**:
  - Dynamic per-tenant authentication scheme selection and `TenantCookieAuthenticationEvents<TTenant>`.
- **Configuration Store (`EricksonLopez.MultiTenancy.Configuration`)**:
  - `ConfigurationTenantStore<TTenant>` binding tenant metadata from `IConfiguration` and `IOptionsMonitor`.
- **Dapper Integration (`EricksonLopez.MultiTenancy.Dapper`)**:
  - Parameter helpers: `WithTenant(DynamicParameters, ITenantContext)` and `CreateTenantParameters(ITenantContext)`.
- **Database Dialect Integrations**:
  - `EricksonLopez.MultiTenancy.PostgreSql`: PostgreSQL Row Level Security (RLS) enforcement via transaction-scoped `SET LOCAL` (`BeginTenantTransactionAsync`, `SetTenantRlsContextAsync`) and catalog store `PostgreSqlTenantStore`.
  - `EricksonLopez.MultiTenancy.SqlServer`: SQL Server `SESSION_CONTEXT` management via `sp_set_session_context` and `sp_set_session_context @reset`.
  - `EricksonLopez.MultiTenancy.MySql`: MySQL session variable isolation (`@app_tenant_id`).
  - `EricksonLopez.MultiTenancy.MariaDb`: MariaDB session variable isolation (`@app_tenant_id`).
  - `EricksonLopez.MultiTenancy.Oracle`: Oracle Virtual Private Database (VPD) and `DBMS_SESSION.SET_IDENTIFIER`.
  - `EricksonLopez.MultiTenancy.Sqlite`: SQLite database-per-tenant (`ISqliteTenantConnectionFactory`) and temp table session context.
- **OpenTelemetry & Observability (`EricksonLopez.MultiTenancy.OpenTelemetry`)**:
  - Distributed tracing via `TenantActivitySource`, W3C Baggage propagation, metrics via `TenantMetrics`, and trace enrichment.
- **Testing Kit (`EricksonLopez.MultiTenancy.Testing`)**:
  - Test doubles: `FakeTenantStore`, `FakeTenantResolutionStrategy`, `TestTenantContext`, `TenantContextBuilder`, and `FakeDbInfrastructure`.
- **Executable Showcase (`EricksonLopez.MultiTenancy.Showcase`)**:
  - 10-level progressive reference application covering conceptual foundations, configuration, Dapper, RLS, background jobs, conflict detection, scalability, custom stores, OpenTelemetry, and enterprise architecture.
- **DevOps & Quality Gates**:
  - Full CI/CD pipelines with Stryker mutation testing (100% test coverage, >95% mutation score threshold), SonarCloud static analysis, Sigstore provenance attestation, and NuGet OIDC publishing.

### Changed
- **Breaking:** Re-architected tenant identifier from mutable/string-based representation to strongly-typed immutable `TenantId` readonly record struct backed by `Guid`. All code previously accepting or returning `string` tenant IDs must migrate to `TenantId`. Use `TenantId.Create(string)`, `TenantId.From(string)`, or `TenantId.TryCreate(string, out TenantId)` for parsing. See [Migration Guide](docs/migration.md).
- **Breaking:** Replaced unsafe static `AsyncLocal` accessor (`AsyncLocalTenantContextAccessor`, registered as Singleton) with DI-scoped `ScopedTenantContextAccessor` (registered as Scoped). Consumers must remove manual `AddSingleton<ITenantContextAccessor>` calls and use `AddMultiTenancy()` instead. See [Migration Guide](docs/migration.md).
- **Breaking:** Enforced fail-closed tenant resolution conflict detection in `TenantResolutionMiddleware` (ADR-008): if two or more configured resolution strategies resolve different tenant identifiers within the same HTTP request, the pipeline now aborts immediately with HTTP 400 Bad Request and logs a security conflict event. Applications that previously relied on silent overwrite of tenant context across strategies will receive errors and must align their strategy configuration. See [ADR-008](docs/adr/adr-008-adopt-fail-closed-tenant-resolution-conflict-detection.md).
- **Breaking:** Enforced JWT claims precedence over client-controlled HTTP headers during tenant resolution (ADR-003). Headers and route values can no longer silently override an authenticated `ClaimTenantResolutionStrategy` result. Applications sending discrepant headers alongside authenticated tokens will receive HTTP 400 conflicts. See [ADR-003](docs/adr/adr-003-reject-header-first-resolution.md).
- **Breaking:** Mandated transaction-scoped `SET LOCAL` for PostgreSQL Row Level Security context via `BeginTenantTransactionAsync` (ADR-009). Setting tenant context outside an explicit ADO.NET transaction (`SET` at session level) is no longer supported and would leak context across connection pool requests. All PostgreSQL data access must open a transaction via `BeginTenantTransactionAsync(connection, tenantContext)` before executing queries protected by RLS. See [ADR-009](docs/adr/adr-009-reject-set-session-rls.md) and [RLS Guide](docs/rls.md).

### Removed
- **Breaking:** Removed `EricksonLopez.MultiTenancy.EntityFrameworkCore` package to eliminate runtime reflection, ensure Native AOT compatibility, and enforce database-level RLS over application query filters (ADR-001). Consumers must remove `<PackageReference Include="EricksonLopez.MultiTenancy.EntityFrameworkCore" />` and migrate to `EricksonLopez.MultiTenancy.PostgreSql` + Dapper with PostgreSQL RLS. `MultiTenantDbContext` and all EF Core-based global query filters are no longer available. See [Migration Guide](docs/migration.md) and [ADR-001](docs/adr/adr-001-remove-entityframeworkcore-package.md).
- **Breaking:** Removed legacy implicit SQL string rewriting and automatic global query filter injection (ADR-005, ADR-007). Tenant isolation is now enforced exclusively through explicit `WHERE tenant_id = @TenantId` Dapper parameters and database-level PostgreSQL RLS policies. Any code relying on automatic query modification will not receive tenant filtering.
- **Breaking:** Removed `net10.0` as an explicit Target Framework Moniker (TFM) from all packages. All packages now target `net8.0` and `net9.0` only. Consumers building against `net10.0` will resolve packages via built-in forward compatibility with the `net9.0` binary. No source or API changes are required, but explicit `net10.0` assembly selection is no longer available.

[Unreleased]: https://github.com/ericksonlopezf/dotnet-multitenancy/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/ericksonlopezf/dotnet-multitenancy/releases/tag/v1.0.0
