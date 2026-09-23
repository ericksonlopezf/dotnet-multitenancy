# Public API Inventory — EricksonLopez.MultiTenancy

> **Version**: 2.0.0  
> **Target Frameworks**: .NET 8.0, .NET 9.0  
> **AOT Policy**: Zero Reflection, Zero Dynamic Code, Trimming-Friendly  
> **Source of Truth**: Compiled and tested assemblies from Core Library and Infrastructure

---

## Classified Projects

| Project | Classification | Role in Ecosystem |
|---|---|---|
| `EricksonLopez.MultiTenancy.Abstractions` | Core Library | Identity primitives (`TenantId`), context contracts (`ITenantContext`), store interfaces (`ITenantStore`), and domain errors. |
| `EricksonLopez.MultiTenancy` | Core Library | Scope factory implementations, in-memory store, cached store, HTTP store, and health checks. |
| `EricksonLopez.MultiTenancy.AspNetCore` | Infrastructure | Tenant resolution middleware, HTTP strategies (Claims, Header, Host, Route, BasePath), options, and endpoint filters. |
| `EricksonLopez.MultiTenancy.Authentication` | Infrastructure | Per-tenant authentication and per-tenant cookie validation event handlers. |
| `EricksonLopez.MultiTenancy.Configuration` | Infrastructure | Per-tenant configuration provider backed by `IConfiguration`. |
| `EricksonLopez.MultiTenancy.Dapper` | Infrastructure | Extensions for safe SQL parameterization (`WithTenant`, `CreateTenantParameters`, `WithOrganization`). |
| `EricksonLopez.MultiTenancy.OpenTelemetry` | Infrastructure | W3C distributed tracing (`ActivitySource`, tag enrichment, baggage) and resolution metrics. |
| `EricksonLopez.MultiTenancy.PostgreSql` | Infrastructure | Transactional Row-Level Security (`SET LOCAL app.current_tenant_id`) and PostgreSQL tenant catalog store. |
| `EricksonLopez.MultiTenancy.SqlServer` | Infrastructure | Row-Level Security and session-level isolation (`SESSION_CONTEXT`, `sp_set_session_context`). |
| `EricksonLopez.MultiTenancy.MySql` | Infrastructure | MySQL session variables (`SET @tenant_id = ...`) and connection lifecycle management. |
| `EricksonLopez.MultiTenancy.MariaDb` | Infrastructure | MariaDB session variables (`SET @tenant_id = ...`) and transactional lifecycle management. |
| `EricksonLopez.MultiTenancy.Oracle` | Infrastructure | Virtual Private Database (VPD) with `DBMS_SESSION.SET_IDENTIFIER`. |
| `EricksonLopez.MultiTenancy.Sqlite` | Infrastructure | Database-per-tenant connection factory and temporary table isolation for local verification. |
| `EricksonLopez.MultiTenancy.Testing` | Infrastructure | Test doubles (`FakeDbConnection`, `FakeTenantStore`, `TenantContextBuilder`, `TestTenantContext`). |

---

## 1. Core Abstractions (`EricksonLopez.MultiTenancy.Abstractions`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `TenantId` | `EricksonLopez.MultiTenancy` | Strongly-typed immutable struct encapsulating a `Guid` tenant identifier, implementing `ISpanParsable<TenantId>`, equality, and comparison operators. | `System.Guid`, `EricksonLopez.Result` | Primary tenant identity across the entire ecosystem. | Basic | Yes (Level 00, 11) |
| `ITenantInfo` | `EricksonLopez.MultiTenancy` | Base contract for tenant metadata (`Id`, `Name`, `ConnectionString`, `IsActive`, `Properties`). | `TenantId` | Decoupled access to tenant metadata. | Basic | Yes (Level 00, 08, 11) |
| `TenantInfo` | `EricksonLopez.MultiTenancy` | Canonical immutable record implementing `ITenantInfo`. | `TenantId` | Default registration of tenant metadata. | Basic | Yes (Level 00, 01, 11) |
| `ITenantContext` | `EricksonLopez.MultiTenancy` | Contract for accessing ambient tenant context in current scope (`Tenant`, `IsResolved`, `Source`, `RequiredTenant`). | `ITenantInfo`, `TenantResolutionSource` | Consuming tenant context in endpoints, domain services, and repositories. | Basic | Yes (Level 01, 03, 11) |
| `ITenantContext<TTenant>` | `EricksonLopez.MultiTenancy` | Strongly-typed contract for extended tenant models. | `ITenantContext`, `TTenant : ITenantInfo` | Enterprise systems with specialized tenant attributes. | Advanced | Yes (Level 08, 11) |
| `ITenantContextAccessor` | `EricksonLopez.MultiTenancy` | Write-once accessor per scope to establish the `ITenantContext`. | `ITenantContext` | Resolution pipeline and scope factories. | Intermediate | Yes (Level 01, 06, 11) |
| `ITenantStore` | `EricksonLopez.MultiTenancy` | Contract for asynchronous reading and streaming of tenants by `TenantId`. | `TenantId`, `ITenantInfo`, `EricksonLopez.Result` | Metadata resolution during request lifecycle. | Intermediate | Yes (Level 01, 02, 11) |
| `ITenantStore<TTenant>` | `EricksonLopez.MultiTenancy` | Typed contract for tenant stores. | `ITenantStore`, `TTenant` | Custom stores for extended domain models. | Advanced | Yes (Level 08, 11) |
| `ITenantLookupStore` | `EricksonLopez.MultiTenancy` | Contract for alternative string lookup (subdomain, name, or key). | `ITenantStore`, `ITenantInfo` | Resolution strategies based on Host or Route slugs. | Intermediate | Yes (Level 02, 11) |
| `ITenantLookupStore<TTenant>` | `EricksonLopez.MultiTenancy` | Typed contract for alternative string lookup. | `ITenantLookupStore`, `ITenantStore<TTenant>` | Typed lookup by slug or external identifier. | Advanced | Yes (Level 08, 11) |
| `ITenantResolutionStrategy` | `EricksonLopez.MultiTenancy` | Contract for extracting `TenantId` from execution environment. | `TenantId`, `TenantResolutionSource`, `EricksonLopez.Result` | Implementing HTTP, RPC, or messaging resolution strategies. | Intermediate | Yes (Level 02, 06, 08, 11) |
| `ITenantScopeFactory` | `EricksonLopez.MultiTenancy` | Factory for creating isolated DI scopes with pre-injected tenant context. | `ITenantInfo`, `TenantResolutionSource`, `ITenantScope` | Background workers, message queues, and batch jobs. | Advanced | Yes (Level 05, 11) |
| `ITenantScope` | `EricksonLopez.MultiTenancy` | Disposable scope (`IAsyncDisposable`, `IDisposable`) with `ServiceProvider` and `TenantContext` bound to tenant. | `ITenantContext`, `IServiceProvider` | Deterministic non-HTTP execution boundary enforcement. | Advanced | Yes (Level 05, 11) |
| `ITenantEntity` | `EricksonLopez.MultiTenancy` | Marker contract for tenant-partitioned data entities (`TenantId`). | `TenantId` | Persistence models and automated audit validation. | Basic | Yes (Level 03, 11) |
| `TenantResolutionSource` | `EricksonLopez.MultiTenancy` | Enum identifying resolution mechanism (`JwtClaim`, `Route`, `Host`, `Header`, `ExplicitScope`, `PlatformAdmin`, `BackgroundJob`, `MessageMetadata`). | None | Traceability, security logs, and conflict detection. | Basic | Yes (Level 00, 05, 09, 11) |
| `TenantErrors` | `EricksonLopez.MultiTenancy` | Factory for standardized domain errors (`NotFound`, `Unresolved`, `Inactive`, `InvalidId`, `StrategyFailed`). | `EricksonLopez.Result` | Deterministic error handling without unhandled exceptions. | Basic | Yes (Level 00, 06, 11) |
| `TenantNotFoundException` | `EricksonLopez.MultiTenancy` | Exception thrown when accessing `RequiredTenant` on an unresolved context. | `System.Exception` | Security invariants in protected controllers and endpoints. | Intermediate | Yes (Level 05, 06, 11) |
| `TenantInactiveException` | `EricksonLopez.MultiTenancy` | Exception thrown when accessing `RequiredTenant` for an inactive or suspended tenant. | `System.Exception`, `TenantId` | Access control for suspended or terminated tenants. | Intermediate | Yes (Level 06, 11) |
| `ICompanyContext` | `EricksonLopez.MultiTenancy` | Contract for enterprise legal entity context (`CompanyId`, `HasCompanyContext`). | `System.Guid` | Multi-company corporate hierarchy architectures. | Advanced | Yes (Level 11) |
| `IBranchContext` | `EricksonLopez.MultiTenancy` | Contract for branch/location context (`BranchId`, `AllowedBranchIds`, `AllBranchesAllowed`). | `System.Guid` | Operational isolation at branch/office level. | Advanced | Yes (Level 11) |
| `IOrganizationContext` | `EricksonLopez.MultiTenancy` | Composite contract combining `ITenantContext`, `ICompanyContext`, and `IBranchContext`. | `ITenantContext`, `ICompanyContext`, `IBranchContext` | Complex enterprise systems requiring 3-tier organizational isolation. | Advanced | Yes (Level 11) |
| `IPlatformAdminContext` | `EricksonLopez.MultiTenancy` | Contract for administrative cross-tenant bypass operations (`IsPlatformAdmin`, `AuditReason`). | None | Cross-tenant administrative tasks and orchestrations (ADR-004). | Advanced | Yes (Level 11) |
| `TenantIdJsonConverter` | `EricksonLopez.MultiTenancy.Serialization` | JSON converter for `System.Text.Json` serializing `TenantId` as a GUID string. | `System.Text.Json` | Web API interoperability and contract serialization. | Basic | Yes (Level 00, 11) |

---

## 2. Core Engine (`EricksonLopez.MultiTenancy`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `TenantContext` | `EricksonLopez.MultiTenancy` | Immutable implementation of `ITenantContext` with sentinel `TenantContext.Empty`. | `ITenantInfo`, `TenantResolutionSource` | Runtime representation of tenant state. | Basic | Yes (Level 01, 06, 11) |
| `TenantContext<TTenant>` | `EricksonLopez.MultiTenancy` | Typed implementation of `ITenantContext<TTenant>`. | `TenantContext`, `TTenant` | Injecting contexts with extended metadata schemas. | Advanced | Yes (Level 08, 11) |
| `ScopedTenantContextAccessor` | `EricksonLopez.MultiTenancy` | Implementation of `ITenantContextAccessor` with strict write-once semantics per scope. | `ITenantContext` | Scoped DI registration in container. | Intermediate | Yes (Level 06, 11) |
| `AmbientTenantContextHolder` | `EricksonLopez.MultiTenancy` | Container based on `AsyncLocal<ITenantContext?>` for ambient propagation outside HTTP pipelines. | `System.Threading.AsyncLocal` | Telemetry, infrastructure libraries, and asynchronous middleware. | Advanced | Yes (Level 11) |
| `DefaultTenantScopeFactory` | `EricksonLopez.MultiTenancy` | Implementation of `ITenantScopeFactory` creating DI scopes with pre-bound tenant context. | `IServiceScopeFactory`, `ITenantContextAccessor` | Background worker execution (`IHostedService`). | Advanced | Yes (Level 05, 11) |
| `InMemoryTenantStore` | `EricksonLopez.MultiTenancy` | Concurrent in-memory store (`ConcurrentDictionary`) for `TenantInfo`. | `TenantInfo` | Local development, testing environments, and static catalogs. | Basic | Yes (Level 01, 02, 11) |
| `InMemoryTenantStore<TTenant>` | `EricksonLopez.MultiTenancy` | Generic in-memory store for any `TTenant : ITenantInfo`. | `TTenant` | Unit testing of extended typed schemas. | Intermediate | Yes (Level 08, 11) |
| `CachedTenantStore<TTenant>` | `EricksonLopez.MultiTenancy.Stores` | Decorator for `ITenantStore<TTenant>` supporting `IMemoryCache` with absolute/sliding expiration. | `ITenantStore<TTenant>`, `IMemoryCache` | High-concurrency environments and minimizing database catalog queries. | Intermediate | Yes (Level 02, 07, 11) |
| `CachedTenantStoreOptions` | `EricksonLopez.MultiTenancy.Stores` | Cache configuration options (`AbsoluteExpirationRelativeToNow`, `SlidingExpiration`). | None | Configuring memory cache retention policies. | Basic | Yes (Level 02, 07, 11) |
| `HttpRemoteTenantStore<TTenant>` | `EricksonLopez.MultiTenancy.Stores` | Resilient HTTP client querying tenant catalogs from remote upstream services. | `HttpClient`, `HttpRemoteTenantStoreOptions` | Microservice architectures with central tenant directory services. | Advanced | Yes (Level 02, 11) |
| `HttpRemoteTenantStoreOptions` | `EricksonLopez.MultiTenancy.Stores` | Options for remote HTTP tenant store (`BaseAddress`, endpoint templates, timeout). | None | Integration with control plane APIs. | Intermediate | Yes (Level 02, 11) |
| `PlatformAdminContext` | `EricksonLopez.MultiTenancy` | Immutable implementation of `IPlatformAdminContext`. | None | Multi-tenant administrative tasks and background orchestration. | Advanced | Yes (Level 11) |
| `MultiTenancyHealthCheck` | `EricksonLopez.MultiTenancy.HealthChecks` | Health check verifying multi-tenancy subsystem and backing store connectivity. | `ITenantStore`, `MultiTenancyHealthCheckOptions` | Readiness probes (`/health`) in Kubernetes and container orchestrators. | Intermediate | Yes (Level 09, 11) |
| `MultiTenancyHealthCheckOptions` | `EricksonLopez.MultiTenancy.HealthChecks` | Configuration options for `MultiTenancyHealthCheck` (`FailureStatus`, `IncludeDiagnosticData`, `StoreProbe`). | `HealthStatus`, async delegates | Customizing health verification probes. | Intermediate | Yes (Level 09, 11) |
| `ServiceCollectionExtensions` | `EricksonLopez.MultiTenancy` | Extension methods: `AddMultiTenancy()`, `AddMultiTenancy<TTenant>()`, `AddInMemoryTenantStore()`, `AddHttpRemoteTenantStore()`. | `IServiceCollection` | Primary DI configuration entry point for .NET applications. | Basic | Yes (Level 01, 02, 08, 11) |

---

## 3. Web Presentation (`EricksonLopez.MultiTenancy.AspNetCore`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `TenantResolutionMiddleware` | `EricksonLopez.MultiTenancy.AspNetCore` | HTTP middleware executing cascading resolution strategies and detecting resolution conflicts (ADR-008). | `ITenantResolutionStrategy`, `ITenantStore`, `ITenantContextAccessor` | Pipeline activation via `app.UseMultiTenancy()`. | Intermediate | Yes (Level 01, 06, 11) |
| `TenantResolutionConflictException` | `EricksonLopez.MultiTenancy.AspNetCore` | Fail-closed exception thrown when multiple resolution strategies yield conflicting tenant identifiers. | `InvalidOperationException` | Defending against tenant spoofing attacks and routing discrepancies. | Advanced | Yes (Level 06, 11) |
| `TenantResolutionMiddlewareOptions` | `EricksonLopez.MultiTenancy.AspNetCore.Options` | Configuration options for middleware (`ThrowOnResolutionFailure`). | None | Configuring behavior when tenant resolution fails. | Basic | Yes (Level 11) |
| `AspNetCoreMultiTenancyExtensions` | `EricksonLopez.MultiTenancy.AspNetCore` | Extension methods: `UseMultiTenancy()`, `AddAspNetCoreMultiTenancy()`, `RequireTenant()`, `AllowAnonymousTenant()`. | `IApplicationBuilder`, `IEndpointRouteBuilder` | HTTP pipeline orchestration and endpoint security in Minimal APIs. | Basic | Yes (Level 01, 03, 11) |
| `RequireTenantFilter` | `EricksonLopez.MultiTenancy.AspNetCore` | Endpoint filter rejecting unresolved tenant requests with HTTP 400 ProblemDetails. | `ITenantContext` | Automated route protection without repetitive boilerplate code. | Basic | Yes (Level 03, 11) |
| `AllowAnonymousTenantAttribute` | `EricksonLopez.MultiTenancy.AspNetCore` | Attribute excluding public routes or health probes from mandatory tenant requirements. | `Attribute`, `IAllowAnonymousTenantMetadata` | Public `/health` endpoints, webhooks, and landing pages. | Basic | Yes (Level 11) |
| `TenantRouteConstraint` | `EricksonLopez.MultiTenancy.AspNetCore.Routing` | Route constraint validating whether URL segment represents a valid GUID or tenant slug. | `IRouteConstraint` | Defining parameterized route templates like `/{tenantId:tenant}/api/...`. | Intermediate | Yes (Level 11) |
| `TenantOptionsCache<TOptions>` | `EricksonLopez.MultiTenancy.AspNetCore.Options` | Isolated per-tenant options cache for `IOptionsSnapshot<T>` / `IOptionsMonitor<T>`. | `ITenantContext`, `IOptionsMonitorCache<TOptions>` | Per-tenant configurations in singleton/scoped services. | Advanced | Yes (Level 02, 11) |
| `MultiTenancyOptionsExtensions` | `EricksonLopez.MultiTenancy.AspNetCore.Options` | Extension method `AddPerTenantOptions<TOptions, TTenant>()`. | `IServiceCollection` | Registering per-tenant segmented options in DI container. | Advanced | Yes (Level 02, 11) |
| `ClaimTenantResolutionStrategy` | `EricksonLopez.MultiTenancy.AspNetCore.Strategies` | Resolves tenant from JWT claims (`tenant_id`, `tid`, etc.). | `IHttpContextAccessor`, `ClaimTypes` | Authenticated APIs using OAuth2 / OIDC / Azure AD / Auth0. | Basic | Yes (Level 01, 02, 11) |
| `HeaderTenantResolutionStrategy` | `EricksonLopez.MultiTenancy.AspNetCore.Strategies` | Resolves tenant from HTTP header (`X-Tenant-ID`). | `IHttpContextAccessor` | API clients, mobile apps, or internal service calls. | Basic | Yes (Level 02, 11) |
| `InternalGatewayHeaderTenantResolutionStrategy` | `EricksonLopez.MultiTenancy.AspNetCore.Strategies` | Resolves tenant from internal gateway header validated by shared secret token. | `IHttpContextAccessor` | Zero-Trust internal networks where an API Gateway injects tenant identity. | Advanced | Yes (Level 02, 11) |
| `HostNameTenantResolutionStrategy` | `EricksonLopez.MultiTenancy.AspNetCore.Strategies` | Resolves tenant by parsing subdomain or FQDN from HTTP Host header. | `IHttpContextAccessor`, `ITenantLookupStore` | Multi-tenant SaaS applications using tenant subdomains (`acme.app.com`). | Intermediate | Yes (Level 02, 11) |
| `RouteTenantResolutionStrategy` | `EricksonLopez.MultiTenancy.AspNetCore.Strategies` | Resolves tenant from route values in URL path. | `IHttpContextAccessor` | Route-partitioned APIs (`/api/{tenantId}/resources`). | Intermediate | Yes (Level 02, 11) |
| `BasePathTenantResolutionStrategy` | `EricksonLopez.MultiTenancy.AspNetCore.Strategies` | Resolves tenant from base path prefix in URL (`/{tenant}/api/...`). | `IHttpContextAccessor`, `ITenantLookupStore` | APIs structured with leading tenant path prefixes. | Intermediate | Yes (Level 02, 11) |
| `StaticTenantResolutionStrategy` | `EricksonLopez.MultiTenancy.AspNetCore.Strategies` | Fixed-value strategy for fallback, testing, or single-tenant deployments. | `TenantId` | Development environments and integration test suites. | Basic | Yes (Level 02, 11) |
| `DelegateTenantResolutionStrategy` | `EricksonLopez.MultiTenancy.AspNetCore.Strategies` | Custom strategy backed by delegate `Func<CancellationToken, ValueTask<Result<TenantId>>>`. | C# Delegates | Custom ad-hoc or hybrid resolution algorithms. | Intermediate | Yes (Level 02, 11) |

---

## 4. Authentication (`EricksonLopez.MultiTenancy.Authentication`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `TenantAuthenticationExtensions` | `EricksonLopez.MultiTenancy.Authentication` | Method `AddPerTenantAuthentication<TTenant>()` for dynamic scheme selection. | `IServiceCollection` | Systems with distinct Identity Providers per tenant. | Advanced | Yes (Level 02, 03, 11) |
| `TenantAuthenticationOptions` | `EricksonLopez.MultiTenancy.Authentication` | Configuration options for per-tenant authentication schemes. | None | Configuring per-tenant cookie and dynamic scheme routing. | Intermediate | Yes (Level 11) |
| `TenantCookieAuthenticationEvents<TTenant>` | `EricksonLopez.MultiTenancy.Authentication` | Validates that session cookie strictly matches active tenant resolved for request. | `ITenantContext`, `CookieValidatePrincipalContext` | Preventing cross-tenant cookie replay attacks. | Advanced | Yes (Level 03, 11) |

---

## 5. Configuration (`EricksonLopez.MultiTenancy.Configuration`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `ConfigurationTenantStore<TTenant>` | `EricksonLopez.MultiTenancy.Configuration` | Tenant store backed by `IConfiguration` sections (`appsettings.json`). | `IConfiguration`, `IOptions` | Static deployments, local development, or cloud App Configuration catalogs. | Intermediate | Yes (Level 02, 11) |
| `MultiTenancyConfigurationOptions<TTenant>` | `EricksonLopez.MultiTenancy.Configuration` | Options model containing list of tenants loaded from configuration. | `List<TTenant>` | Mapping `appsettings.json` sections to typed collections. | Basic | Yes (Level 02, 11) |
| `ConfigurationMultiTenancyBuilderExtensions` | `EricksonLopez.MultiTenancy.Configuration` | Extension method `AddConfigurationTenantStore<TTenant>()`. | `IServiceCollection` | Fluent registration of configuration tenant store. | Basic | Yes (Level 02, 11) |

---

## 6. Dapper Extensions (`EricksonLopez.MultiTenancy.Dapper`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `TenantDapperExtensions` | `EricksonLopez.MultiTenancy.Dapper` | Methods `WithTenant()` and `CreateTenantParameters()` for `DynamicParameters` and `ITenantContext`. | `Dapper.DynamicParameters`, `ITenantContext`, `TenantId` | Safe SQL query parameterization preventing data leakage (Layer 2). | Basic | Yes (Level 03, 11) |
| `OrganizationDapperExtensions` | `EricksonLopez.MultiTenancy.Dapper` | Methods `WithOrganization()` and `CreateOrganizationParameters()` for composite hierarchies. | `Dapper.DynamicParameters`, `IOrganizationContext` | SQL queries filtering across `TenantId`, `CompanyId`, and `BranchId`. | Advanced | Yes (Level 11) |

---

## 7. OpenTelemetry & Observability (`EricksonLopez.MultiTenancy.OpenTelemetry`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `TenantActivitySource` | `EricksonLopez.MultiTenancy.OpenTelemetry` | Canonical `ActivitySource` (`"EricksonLopez.MultiTenancy"`), span attributes, and W3C Baggage keys. | `System.Diagnostics.ActivitySource` | Distributed tracing compatible with Jaeger, Zipkin, and OTLP. | Intermediate | Yes (Level 09, 11) |
| `TenantActivityTags` | `EricksonLopez.MultiTenancy.OpenTelemetry` | Standard semantic attribute names (`tenant.id`, `tenant.name`, `tenant.source`, `tenant.is_active`). | None | Adherence to OpenTelemetry semantic conventions. | Basic | Yes (Level 09, 11) |
| `TenantActivityExtensions` | `EricksonLopez.MultiTenancy.OpenTelemetry` | Methods `EnrichWithTenant()`, `RecordTenantResolutionFailure()`, and `SetTenantBaggage()`. | `System.Diagnostics.Activity`, `ITenantContext` | Automatic enrichment of HTTP traces and background job spans. | Intermediate | Yes (Level 09, 11) |
| `TenantMetrics` | `EricksonLopez.MultiTenancy.OpenTelemetry` | Performance metrics and counters (`tenant.resolution.duration`, `tenant.resolution.conflicts`). | `System.Diagnostics.Metrics.Meter` | Metrics dashboards in Prometheus, Grafana, and security alerting. | Intermediate | Yes (Level 09, 11) |
| `ITenantTraceEnricher` | `EricksonLopez.MultiTenancy.OpenTelemetry` | Service contract for enriching current trace activity from application code. | `ITenantContext` | DI injection into application handlers and domain services. | Intermediate | Yes (Level 10, 11) |
| `TenantTraceEnricher` | `EricksonLopez.MultiTenancy.OpenTelemetry` | Default implementation of `ITenantTraceEnricher`. | `ITenantContext` | Scoped service registered by telemetry subsystem. | Intermediate | Yes (Level 10, 11) |
| `MultiTenancyOpenTelemetryExtensions` | `EricksonLopez.MultiTenancy.OpenTelemetry` | Extension method `AddMultiTenancyOpenTelemetry()`. | `IServiceCollection` | Comprehensive registration of telemetry enrichers in DI container. | Basic | Yes (Level 09, 11) |

---

## 8. Database Providers & Dialects

### 8.1. PostgreSQL (`EricksonLopez.MultiTenancy.PostgreSql`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `PostgreSqlRlsExtensions` | `EricksonLopez.MultiTenancy.PostgreSql` | Executes `SET LOCAL app.current_tenant_id = @tenantId` transactionally for PostgreSQL kernel-level RLS. | `DbConnection`, `DbTransaction`, `ITenantContext`, `TenantId` | Shared database with kernel-enforced cryptographic/RLS isolation (ADR-002). | Advanced | Yes (Level 04, 11) |
| `PostgreSqlOrganizationRlsExtensions` | `EricksonLopez.MultiTenancy.PostgreSql` | Binds RLS variables for Tenant, Company, and Branch in a single transactional call. | `DbConnection`, `DbTransaction`, `IOrganizationContext` | Complete corporate hierarchies in PostgreSQL RLS. | Advanced | Yes (Level 11) |
| `OrganizationRlsOptions` | `EricksonLopez.MultiTenancy.PostgreSql` | GUC variable name options for organizational hierarchy. | None | Customizing PostgreSQL RLS session variable names. | Intermediate | Yes (Level 11) |
| `PostgreSqlRlsDiagnostics` | `EricksonLopez.MultiTenancy.PostgreSql` | Queries `current_setting(variable)` to assert active RLS context on physical connection. | `DbConnection`, `DbTransaction` | Integration tests, assertions, and connection pool leak auditing. | Intermediate | Yes (Level 11) |
| `PostgreSqlTenantStore` | `EricksonLopez.MultiTenancy.PostgreSql` | Read-only persistent tenant store querying PostgreSQL tables. | `NpgsqlConnection` or `DbConnection` | Production relational tenant directory catalogs. | Intermediate | Yes (Level 04, 11) |
| `PostgreSqlTenantStore<TTenant>` | `EricksonLopez.MultiTenancy.PostgreSql` | Typed persistent tenant store querying PostgreSQL tables. | `TTenant` | Catalogs with customized tenant domain metadata schemas. | Advanced | Yes (Level 11) |
| `PostgreSqlTenantStoreOptions` | `EricksonLopez.MultiTenancy.PostgreSql` | Table, schema, and column mapping configuration for PostgreSQL catalog. | None | Flexible mapping to existing database catalog schemas. | Basic | Yes (Level 04, 11) |
| `PostgreSqlTenantStoreExtensions` | `EricksonLopez.MultiTenancy.PostgreSql` | Extension method `AddPostgreSqlTenantStore()`. | `IServiceCollection` | DI registration for PostgreSQL tenant store. | Basic | Yes (Level 04, 11) |

### 8.2. SQL Server (`EricksonLopez.MultiTenancy.SqlServer`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `SqlServerSessionContextExtensions` | `EricksonLopez.MultiTenancy.SqlServer` | Manages `SESSION_CONTEXT` via `sp_set_session_context` and handles cleanup on pool return. | `DbConnection`, `DbTransaction`, `ITenantContext`, `TenantId` | Row-Level Security in Microsoft SQL Server and Azure SQL Database. | Advanced | Yes (Level 04, 11) |

### 8.3. MySQL (`EricksonLopez.MultiTenancy.MySql`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `MySqlTenantExtensions` | `EricksonLopez.MultiTenancy.MySql` | Sets and resets MySQL user session variables (`SET @tenant_id = ...`) with safe rollback. | `DbConnection`, `DbTransaction`, `ITenantContext`, `TenantId` | Multi-tenant isolation in shared MySQL databases. | Advanced | Yes (Level 04, 11) |

### 8.4. MariaDB (`EricksonLopez.MultiTenancy.MariaDb`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `MariaDbTenantExtensions` | `EricksonLopez.MultiTenancy.MariaDb` | Manages MariaDB user session variables (`SET @tenant_id = ...`) and connection lifecycle. | `DbConnection`, `DbTransaction`, `ITenantContext`, `TenantId` | Multi-tenant isolation in MariaDB clusters. | Advanced | Yes (Level 04, 11) |

### 8.5. Oracle (`EricksonLopez.MultiTenancy.Oracle`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `OracleVpdExtensions` | `EricksonLopez.MultiTenancy.Oracle` | Configures Virtual Private Database (VPD) via `DBMS_SESSION.SET_IDENTIFIER` and `CLIENT_IDENTIFIER`. | `DbConnection`, `DbTransaction`, `ITenantContext`, `TenantId` | Corporate governance and VPD security policies in Oracle Database. | Advanced | Yes (Level 04, 11) |

### 8.6. SQLite (`EricksonLopez.MultiTenancy.Sqlite`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `ISqliteTenantConnectionFactory` | `EricksonLopez.MultiTenancy.Sqlite` | Factory contract for routing connections to individual per-tenant SQLite database files. | `ITenantInfo`, `DbConnection` | Database-per-tenant isolation pattern on SQLite. | Intermediate | Yes (Level 07, 11) |
| `SqliteTenantConnectionFactory` | `EricksonLopez.MultiTenancy.Sqlite` | SQLite connection factory implementation with directory creation and token replacement (`{TenantId}`, `{Name}`). | `ISqliteTenantConnectionFactory` | Embedded architectures, edge computing, and isolated local storage. | Intermediate | Yes (Level 07, 11) |
| `SqliteTenantExtensions` | `EricksonLopez.MultiTenancy.Sqlite` | Simulates tenant context isolation in SQLite using in-memory temporary tables. | `DbConnection`, `DbTransaction`, `ITenantContext`, `TenantId` | Local integration testing without requiring external database servers. | Intermediate | Yes (Level 04, 11) |

---

## 9. Testing & Harness (`EricksonLopez.MultiTenancy.Testing`)

| Name | Namespace | Responsibility | Dependencies | Use Cases | Complexity Level | Existing Example |
|---|---|---|---|---|---|---|
| `TenantContextBuilder` | `EricksonLopez.MultiTenancy.Testing` | Fluent builder for instantiating immutable test contexts (`WithId`, `WithName`, `WithProperty`, `WithSource`, `WithActive`). | `ITenantContext`, `TenantInfo` | Unit testing of application services, handlers, and repositories. | Basic | Yes (Level 10, 11) |
| `TestTenantContext` | `EricksonLopez.MultiTenancy.Testing` | Mutable test context with `Reset()` method and `TestTenantContext.Create()` factory. | `ITenantContext` | Complex integration test suites with multi-step tenant transitions. | Basic | Yes (Level 10, 11) |
| `FakeTenantStore` | `EricksonLopez.MultiTenancy.Testing` | Pre-populated test double for `ITenantLookupStore` in unit test scenarios. | `ITenantLookupStore`, `TenantInfo` | Layer isolation without external database dependencies. | Basic | Yes (Level 06, 11) |
| `FakeTenantStore<TTenant>` | `EricksonLopez.MultiTenancy.Testing` | Generic test double for `ITenantLookupStore<TTenant>`. | `ITenantLookupStore<TTenant>`, `TTenant` | Testing custom metadata schema stores. | Intermediate | Yes (Level 11) |
| `FakeTenantResolutionStrategy` | `EricksonLopez.MultiTenancy.Testing` | Configurable resolution strategy simulating successful or conflicting resolution outcomes. | `ITenantResolutionStrategy` | Testing resolution middleware and fail-closed conflict handling. | Basic | Yes (Level 06, 11) |
| `FakeDbConnection` | `EricksonLopez.MultiTenancy.Testing` | Mock connection recording executed commands without connecting to a real database. | `System.Data.Common.DbConnection` | Testing SQL dialect extensions and Dapper repositories. | Intermediate | Yes (Level 04, 10, 11) |
| `FakeDbCommand` | `EricksonLopez.MultiTenancy.Testing` | Mock command capturing `CommandText`, parameters, and associated transaction. | `System.Data.Common.DbCommand` | Asserting generated SQL statements and bound parameters. | Intermediate | Yes (Level 11) |
| `FakeDbParameter` | `EricksonLopez.MultiTenancy.Testing` | Mock database parameter with support for `ResetDbType`. | `System.Data.Common.DbParameter` | Verifying parameter types and values passed to ADO.NET engines. | Intermediate | Yes (Level 11) |
| `FakeDbParameterCollection` | `EricksonLopez.MultiTenancy.Testing` | Mock parameter collection supporting lookup, insertion, and indexing methods. | `System.Data.Common.DbParameterCollection` | Complete ADO.NET emulation for Dapper unit testing. | Intermediate | Yes (Level 11) |
| `FakeDbTransaction` | `EricksonLopez.MultiTenancy.Testing` | Mock transaction tracking commit and rollback calls. | `System.Data.Common.DbTransaction` | Testing RLS transactional boundary lifecycles. | Intermediate | Yes (Level 11) |
| `FakeDbDataReader` | `EricksonLopez.MultiTenancy.Testing` | Mock data reader with configurable schema and rows. | `System.Data.Common.DbDataReader` | Simulating catalog queries and relational query results. | Intermediate | Yes (Level 11) |
