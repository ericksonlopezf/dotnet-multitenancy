# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2026-09-23

### Added
- **Multi-tier Hierarchy Abstractions (`EricksonLopez.MultiTenancy.Abstractions`)**:
  - `IOrganizationContext`: Unified composite interface combining `ITenantContext`, `ICompanyContext`, and `IBranchContext`.
  - `ICompanyContext`: Legal entity context abstraction exposing `CompanyId` and `HasCompanyContext`.
  - `IBranchContext`: Branch/facility context abstraction exposing `BranchId`, `AllowedBranchIds`, and `AllBranchesAllowed`.
  - `IPlatformAdminContext`: Elevated cross-tenant platform administration contract exposing `IsPlatformAdmin` and `AuditReason` (ADR-004).
  - `ISpanParsable<TenantId>` interface implementation and zero-allocation parsing overloads: `TenantId.Create(ReadOnlySpan<char>)`, `TenantId.From(ReadOnlySpan<char>)`, `TenantId.Parse(ReadOnlySpan<char>, IFormatProvider?)`, `TenantId.TryParse(ReadOnlySpan<char>, IFormatProvider?, out TenantId)`.
  - Asynchronous tenant streaming support on `ITenantStore` and `ITenantStore<TTenant>` via default interface method `GetAllStreamAsync(CancellationToken)`.
  - `TenantResolutionSource.Source` default property on `ITenantResolutionStrategy`.
- **Core Implementation (`EricksonLopez.MultiTenancy`)**:
  - `PlatformAdminContext`: Immutable record implementation of `IPlatformAdminContext` with `PlatformAdminContext.None` singleton.
  - `AmbientTenantContextHolder`: Ambient context holder managing `AsyncLocal<ITenantContext>` propagation for background jobs, message queues, and non-HTTP workers without HttpContext dependencies.
  - Automatic scoped `TTenant` registration in `services.AddMultiTenancy<TTenant>()` for direct injection of typed tenant models into domain services.
  - Striped locking (64 stripes via `SemaphoreSlim`) and negative lookup caching in `CachedTenantStore<TTenant>`.
  - Optional `maxCapacity` bounded limit constructor in `InMemoryTenantStore<TTenant>`.
- **ASP.NET Core Integration (`EricksonLopez.MultiTenancy.AspNetCore`)**:
  - `InternalGatewayHeaderTenantResolutionStrategy`: Header-based resolution strategy validating a shared gateway secret header to prevent tenant spoofing behind reverse proxies.
  - `AllowAnonymousTenantAttribute` and `IAllowAnonymousTenantMetadata`: Endpoint metadata attribute allowing requests with missing or unregistered tenant identifiers to proceed past `TenantResolutionMiddleware`.
  - `TenantResolutionConflictException`: Strongly-typed exception thrown when conflicting tenant resolution strategies evaluate disparate tenant identifiers, exposing `FirstTenantId`, `FirstStrategy`, `SecondTenantId`, and `SecondStrategy`.
  - `TenantResolutionMiddlewareOptions`: Configuration options controlling `FailOnStoreMiss`, `WriteProblemDetailsOnConflict`, `IncludeStrategyDetailsInConflictResponse`, `ValidateAuthenticatedPrincipalTenantClaim`, and `TenantClaimType`.
  - `HostNameTenantResolutionStrategyOptions`: Configuration options controlling hostname extraction, root domains, port stripping, and custom subdomain patterns.
  - `TenantOptionsCacheOptions`: Configuration options controlling per-tenant options caching behavior.
- **Per-Tenant Authentication (`EricksonLopez.MultiTenancy.Authentication`)**:
  - `services.AddPerTenantAuthentication<TTenant>(Action<TenantAuthenticationOptions>)`: Fluent registration for dynamic per-tenant authentication schemes and cookie configurations.
- **Relational Database Session Reset & Invariants**:
  - `EricksonLopez.MultiTenancy.SqlServer`: Synchronous session context reset via `ResetTenantSessionContext(connection, transaction, sessionKey)` and strongly-typed `SqlConnection` overloads.
  - `EricksonLopez.MultiTenancy.MySql`: Synchronous session variable reset via `ResetTenantSessionVariable(connection, transaction, variableName)`.
  - `EricksonLopez.MultiTenancy.MariaDb`: Synchronous session variable reset via `ResetTenantSessionVariable(connection, transaction, variableName)`.
  - `EricksonLopez.MultiTenancy.Oracle`: Synchronous VPD identifier clear via `ResetTenantVpdContext(connection, transaction)`.
  - `EricksonLopez.MultiTenancy.PostgreSql`: Strongly-typed `NpgsqlConnection` extension overloads for `BeginTenantTransactionAsync` and `SetTenantRlsContextAsync`, and diagnostic helper `PostgreSqlRlsDiagnostics`.
- **Dapper & Organization Extensions (`EricksonLopez.MultiTenancy.Dapper`)**:
  - Multi-tier Dapper parameter helpers: `CreateOrganizationParameters(IOrganizationContext)` and `WithOrganization(DynamicParameters, IOrganizationContext)`.
- **Roslyn Analyzers (`EricksonLopez.MultiTenancy.Analyzers`)**:
  - `ELMT004`: Analyzer detecting database superuser credentials (`postgres`, `sa`, `root`) in connection strings that bypass Row-Level Security.
  - Code fix provider `TenantContextStaticFieldCodeFixProvider` for `ELMT001`.

### Changed
- **Breaking (BC-001):** `ITenantContext.RequiredTenant` now validates tenant active status and throws `TenantInactiveException` if `Tenant.IsActive` is `false`. Callers accessing `RequiredTenant` for inactive or suspended tenants must catch `TenantInactiveException` or inspect `context.Tenant?.IsActive` before accessing the property. See [Abstractions](src/EricksonLopez.MultiTenancy.Abstractions/ITenantContext.cs).
- **Breaking (BC-002):** `DefaultTenantScopeFactory.CreateScope` now validates tenant active status and throws `TenantInactiveException` if `tenant.IsActive` is `false`. Background job consumers performing administrative or maintenance tasks on inactive tenants must ensure tenant activation or use `IPlatformAdminContext`. See [Core](src/EricksonLopez.MultiTenancy/DefaultTenantScopeFactory.cs).
- **Breaking (BC-003):** Changed parameter order and nullability in `SqlServerSessionContextExtensions.SetTenantSessionContextAsync`. The `DbTransaction transaction` parameter was moved from 3rd position (`DbTransaction? transaction = null`) to 2nd position (`DbTransaction transaction`) and made non-nullable. Calls omitting `transaction` or passing `null` will fail compilation and throw `InvalidOperationException` at runtime. Consumers must migrate calls to `connection.SetTenantSessionContextAsync(transaction, tenantContext)` or use `connection.BeginTenantTransactionAsync(tenantContext)`. See [SqlServer](src/EricksonLopez.MultiTenancy.SqlServer/SqlServerSessionContextExtensions.cs).
- **Breaking (BC-004):** Changed parameter order and nullability in `MySqlTenantExtensions.SetTenantSessionVariableAsync`. The `DbTransaction transaction` parameter was moved from 3rd position (`DbTransaction? transaction = null`) to 2nd position (`DbTransaction transaction`) and made mandatory. Consumers must update invocations to `connection.SetTenantSessionVariableAsync(transaction, tenantContext)` or use `connection.BeginTenantTransactionAsync(tenantContext)`. See [MySql](src/EricksonLopez.MultiTenancy.MySql/MySqlTenantExtensions.cs).
- **Breaking (BC-005):** Changed parameter order and nullability in `MariaDbTenantExtensions.SetTenantSessionVariableAsync`. The `DbTransaction transaction` parameter was moved from 3rd position (`DbTransaction? transaction = null`) to 2nd position (`DbTransaction transaction`) and made mandatory. Consumers must update invocations to `connection.SetTenantSessionVariableAsync(transaction, tenantContext)` or use `connection.BeginTenantTransactionAsync(tenantContext)`. See [MariaDb](src/EricksonLopez.MultiTenancy.MariaDb/MariaDbTenantExtensions.cs).
- **Breaking (BC-006):** Changed parameter order and nullability in `OracleVpdExtensions.SetTenantVpdContextAsync`. The `DbTransaction transaction` parameter was moved from 3rd position (`DbTransaction? transaction = null`) to 2nd position (`DbTransaction transaction`) and made mandatory. Consumers must update invocations to `connection.SetTenantVpdContextAsync(transaction, tenantContext)` or use `connection.BeginTenantTransactionAsync(tenantContext)`. See [Oracle](src/EricksonLopez.MultiTenancy.Oracle/OracleVpdExtensions.cs).
- **Breaking (BC-007):** Replaced public constructor binary signature in `TenantResolutionMiddleware` by adding optional `IOptions<TenantResolutionMiddlewareOptions>?` parameter. Pre-compiled assemblies invoking `.ctor(RequestDelegate, ILogger<TenantResolutionMiddleware>)` must be recompiled to avoid runtime `MissingMethodException`. See [AspNetCore](src/EricksonLopez.MultiTenancy.AspNetCore/TenantResolutionMiddleware.cs).
- **Breaking (BC-008):** Replaced public constructor binary signature in `HostNameTenantResolutionStrategy` by adding optional `IOptions<HostNameTenantResolutionStrategyOptions>?` and `ILogger<HostNameTenantResolutionStrategy>?` parameters. Pre-compiled assemblies invoking `.ctor(IHttpContextAccessor, IServiceProvider)` must be recompiled to avoid runtime `MissingMethodException`. See [AspNetCore](src/EricksonLopez.MultiTenancy.AspNetCore/Strategies/HostNameTenantResolutionStrategy.cs).
- **Breaking (BC-009):** Replaced public constructor binary signature in `TenantCookieAuthenticationEvents<TTenant>` by adding optional `bool requireTenantClaim = true` parameter. Pre-compiled assemblies invoking `.ctor(string)` must be recompiled to avoid runtime `MissingMethodException`. See [Authentication](src/EricksonLopez.MultiTenancy.Authentication/TenantCookieAuthenticationEvents.cs).
- **Breaking (BC-010):** `TenantResolutionMiddleware` now enforces fail-closed store miss behavior by default (`FailOnStoreMiss = true`). If a tenant identifier is resolved by a strategy but is missing from `ITenantStore`, the middleware immediately terminates the request with HTTP 404 (Not Found). Applications requiring unauthenticated passthrough must configure `FailOnStoreMiss = false` in `TenantResolutionMiddlewareOptions` or decorate endpoints with `[AllowAnonymousTenant]`. See [AspNetCore](src/EricksonLopez.MultiTenancy.AspNetCore/TenantResolutionMiddleware.cs).
- **Breaking (BC-011):** `TenantResolutionMiddleware` now validates authenticated principal tenant claims by default (`ValidateAuthenticatedPrincipalTenantClaim = true`). Authenticated requests whose `tenant_id` claim mismatches the resolved request tenant, or authenticated requests lacking tenant claims that resolve tenant via untrusted HTTP headers, are rejected with HTTP 403 (Forbidden). To disable this security gate, set `ValidateAuthenticatedPrincipalTenantClaim = false`. See [AspNetCore](src/EricksonLopez.MultiTenancy.AspNetCore/TenantResolutionMiddleware.cs).
- **Breaking (BC-012):** `TenantCookieAuthenticationEvents<TTenant>` now rejects authenticated cookie principals lacking a tenant claim by default (`requireTenantClaim = true`) whenever an active tenant context exists. Consumers must ensure cookie claims include `tenant_id` or set `requireTenantClaim: false` in constructor. See [Authentication](src/EricksonLopez.MultiTenancy.Authentication/TenantCookieAuthenticationEvents.cs).
- **Breaking (BC-013):** Roslyn analyzers `ELMT001` and `ELMT002` now enforce tenant state rules on `TenantId`, `TenantInfo`, and `ITenantInfo`, and `ELMT002` now scans `HostedService`, `BackgroundService`, and `Worker` classes as well as properties. Projects building with `TreatWarningsAsErrors` will fail build if tenant identifiers are held in static fields, singleton properties, or background worker fields. Migrate to scoped resolution via `ITenantScopeFactory`. See [Analyzers](src/EricksonLopez.MultiTenancy.Analyzers/).
- **Breaking (BC-014):** `PostgreSqlRlsExtensions.SetTenantRlsContextAsync` now validates the `sessionVariable` parameter against alphanumeric regex `^[a-zA-Z0-9_.]+$`. Custom session variable names with invalid characters now throw `ArgumentException`. See [PostgreSql](src/EricksonLopez.MultiTenancy.PostgreSql/PostgreSqlRlsExtensions.cs).
- **Breaking (BC-015):** `SqliteTenantConnectionFactory.GetConnectionString` now validates `tenant.Name` against directory traversal sequences (`..`), slashes (`/`, `\`), and invalid file characters when `{Name}` is present in the template, throwing `ArgumentException` on invalid values. See [Sqlite](src/EricksonLopez.MultiTenancy.Sqlite/SqliteTenantConnectionFactory.cs).
- **Breaking (BC-016):** `AddMultiTenancy<TTenant>()` in `ServiceCollectionExtensions` now automatically registers `TTenant` as a scoped service in the DI container. Resolving `TTenant` in a scope where no tenant is resolved throws `TenantNotFoundException`. Consumers expecting `null` must inject `ITenantContext<TTenant>` instead. See [Core](src/EricksonLopez.MultiTenancy/ServiceCollectionExtensions.cs).
- **Breaking (BC-017):** `TenantResolutionMiddleware` throws typed `TenantResolutionConflictException` (or returns HTTP 409 Conflict if `WriteProblemDetailsOnConflict = true`) when strategies resolve conflicting tenants, replacing generic `InvalidOperationException`. Update catch blocks to catch `TenantResolutionConflictException`. See [AspNetCore](src/EricksonLopez.MultiTenancy.AspNetCore/TenantResolutionMiddleware.cs).

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
  - 12-level progressive reference application covering conceptual foundations, configuration, Dapper, RLS, background jobs, conflict detection, scalability, custom stores, OpenTelemetry, enterprise architecture, and comprehensive public API coverage (Levels 00 to 11).
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

[Unreleased]: https://github.com/ericksonlopezf/dotnet-multitenancy/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/ericksonlopezf/dotnet-multitenancy/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/ericksonlopezf/dotnet-multitenancy/releases/tag/v1.0.0
