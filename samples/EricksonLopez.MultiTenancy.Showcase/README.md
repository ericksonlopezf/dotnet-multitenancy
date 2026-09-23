# EricksonLopez.MultiTenancy Showcase

> **Official Reference Implementation and Executable Suite Documentation**

The **Showcase** project (`EricksonLopez.MultiTenancy.Showcase`) represents the canonical reference implementation of the `EricksonLopez.MultiTenancy` suite. It simultaneously serves as:

- **Executable Documentation:** Every public API contract is supported by compilable, tested C# code.
- **Progressive Learning Curriculum:** 12 structured levels (Level 00 through Level 11) advancing from fundamental primitives to complex enterprise architectures.
- **Production Integration Cookbook:** Ready-to-use patterns for ASP.NET Core, Dapper, PostgreSQL RLS, SQL Server, MySQL, MariaDB, Oracle, SQLite, and OpenTelemetry.
- **Continuous Validation Harness:** Automated verification of 100% of the 148 public APIs on every build.

---

## Execution Modes

The Showcase can be run in three operational modes:

### 1. API Coverage Verification Mode (Console / CI)
Executes a complete automated verification of all curriculum levels and public API contracts, exiting with code `0` when all invariants are satisfied.

```bash
# Run on .NET 9.0
dotnet run --project samples/EricksonLopez.MultiTenancy.Showcase/EricksonLopez.MultiTenancy.Showcase.csproj -f net9.0

# Run on .NET 8.0
dotnet run --project samples/EricksonLopez.MultiTenancy.Showcase/EricksonLopez.MultiTenancy.Showcase.csproj -f net8.0
```

Expected output:
```text
==================================================================
 EricksonLopez.MultiTenancy Showcase & Official Reference App    
==================================================================
Executing Level 11: Comprehensive Public API Coverage Verification...
Level 11: All 148 Public APIs Verified Successfully.
All multi-tenancy showcase levels completed successfully (exit 0).
```

### 2. Interactive HTTP Web Server Mode (`--server`)
Launches Kestrel with interactive Minimal API endpoints to test HTTP resolution, security filters, protected endpoints, health checks, and telemetry:

```bash
dotnet run --project samples/EricksonLopez.MultiTenancy.Showcase/EricksonLopez.MultiTenancy.Showcase.csproj -f net9.0 -- --server
```

Once started, the API is accessible at `http://localhost:5000` (or the port configured in `Properties/launchSettings.json`).

### 3. Docker Container Execution

```bash
# Build Docker image
docker build -t ericksonlopez-multitenancy-showcase -f samples/EricksonLopez.MultiTenancy.Showcase/Dockerfile .

# Run container
docker run -d -p 8080:8080 --name multitenancy-showcase ericksonlopez-multitenancy-showcase

# Test root endpoint
curl http://localhost:8080/
```

---

## Showcase Curriculum Levels

The Showcase is structured into 12 progressive curriculum levels under the `Levels/` directory:

| Level | Directory | Entry File | Concepts & APIs Demonstrated |
|:---|:---|:---|:---|
| **Level 00** | `Level00_Conceptual` | `ConceptualOverview.cs` | `TenantId` primitives (immutable 16-byte struct wrapping `Guid`), `ITenantInfo`, `TenantInfo`, sentinel `TenantId.Empty`, domain errors `TenantErrors`, and JSON serialization via `TenantIdJsonConverter`. |
| **Level 01** | `Level01_QuickStart` | `QuickStartDemo.cs` | Minimal dependency injection (`AddMultiTenancy`, `AddAspNetCoreMultiTenancy`), HTTP middleware (`app.UseMultiTenancy`), in-memory catalog store (`AddInMemoryTenantStore`), and resolved `ITenantContext` access. |
| **Level 02** | `Level02_FullConfiguration` | `FullConfigurationDemo.cs` | Multi-strategy configuration (Claims, Host, Route, BasePath, Internal Header, Static, Delegate), per-tenant segmented options (`AddPerTenantOptions`), configuration stores, and cache decorators (`AddCachedTenantStore`). |
| **Level 03** | `Level03_RealWorldUseCases` | `RealWorldUseCasesDemo.cs` | Explicit Dapper repositories with `ITenantContext`, Minimal API security filters (`.RequireTenant()`), and per-tenant dynamic authentication cookies (`TenantCookieAuthenticationEvents`). |
| **Level 04** | `Level04_AdvancedIntegration` | `AdvancedIntegrationDemo.cs` | Transaction-scoped relational database isolation: PostgreSQL RLS (`SET LOCAL`), SQL Server (`SESSION_CONTEXT`), MySQL / MariaDB (`@tenant_id`), Oracle VPD (`DBMS_SESSION.SET_IDENTIFIER`), and SQLite. |
| **Level 05** | `Level05_BackgroundProcessing` | `BackgroundProcessingDemo.cs` | Non-HTTP background execution (`ITenantScopeFactory`, `ITenantScope`), creating isolated DI service scopes with `TenantResolutionSource.BackgroundJob`. |
| **Level 06** | `Level06_ErrorHandling` | `ErrorHandlingDemo.cs` | Fail-closed resolution conflict detection (ADR-008), suspended/inactive tenant handling (`TenantInactiveException`), and unresolved context guards (`TenantNotFoundException`). |
| **Level 07** | `Level07_Scalability` | `ScalabilityDemo.cs` | Database-per-tenant pattern with `ISqliteTenantConnectionFactory` / `SqliteTenantConnectionFactory` and high-concurrency caching with `CachedTenantStore`. |
| **Level 08** | `Level08_Customization` | `CustomizationDemo.cs` | Strongly-typed custom metadata models (`ITenantContext<TTenant>`), generic `AddMultiTenancy<TTenant>()` overloads, and domain-specific resolution strategies. |
| **Level 09** | `Level09_Observability` | `ObservabilityDemo.cs` | Distributed tracing with `TenantActivitySource`, W3C Baggage propagation, metrics with `TenantMetrics`, and readiness health endpoints with `MultiTenancyHealthCheck`. |
| **Level 10** | `Level10_EnterpriseArchitecture` | `EnterpriseArchitectureDemo.cs` | 4-layer Defense-in-Depth model, Clean Architecture with decoupled services, and unit testing harnesses using `TenantContextBuilder` and `TestTenantContext`. |
| **Level 11** | `Level11_ComprehensiveApiCoverage` | `ComprehensiveApiCoverageDemo.cs` | Exhaustive runtime verification covering 100% of all 148 public API members across the suite, including test doubles (`FakeDbConnection`, `FakeTenantStore`, `FakeTenantStore<TTenant>`), `PlatformAdminContext`, `TenantId` comparison operators, `TenantActivityTags` constants, and `TenantResolutionSource.MessageMetadata`. |

---

## Interactive Endpoints Catalog

When the Showcase runs in server mode (`--server`), it exposes the following endpoints:

### Information and Health Endpoints
- `GET /`: Showcase overview and list of available endpoints.
- `GET /health`: Subsystem health status evaluated by `MultiTenancyHealthCheck`.
- `GET /api/showcase/info`: Version metadata and curriculum level summary.

### Curriculum Demo Endpoints
- `GET /api/quickstart/current-tenant`: Returns the active tenant resolved for the request.
- `GET /api/config/tenant-settings`: Returns per-tenant options configured for the current tenant.
- `GET /api/invoices`: Lists invoices protected with `.RequireTenant()`.
- `GET /api/invoices/{id}`: Retrieves an invoice by ID within the tenant partition.
- `POST /api/invoices`: Creates a new invoice, automatically stamping `TenantId`.
- `GET /api/showcase/levels/level0`: Executes `TenantId` conceptual primitives.
- `GET /api/showcase/levels/level4/postgresql`: Simulates PostgreSQL RLS transaction flow with `FakeDbConnection`.
- `GET /api/showcase/levels/level4/sqlserver`: Simulates SQL Server `SESSION_CONTEXT` transaction flow.
- `GET /api/showcase/levels/level4/mysql`: Simulates MySQL session variable flow.
- `GET /api/showcase/levels/level4/oracle`: Simulates Oracle VPD `DBMS_SESSION` flow.
- `GET /api/showcase/levels/level4/sqlite`: Simulates SQLite temporary table context flow.
- `POST /api/showcase/levels/level5/background-job`: Dispatches an isolated background worker task via `ITenantScopeFactory`.
- `GET /api/showcase/levels/level6/conflicts`: Demonstrates fail-closed resolution conflict detection (ADR-008).
- `GET /api/showcase/levels/level7/scalability`: Demonstrates SQLite Database-per-tenant factory and `CachedTenantStore`.
- `GET /api/showcase/levels/level9/telemetry`: Records OpenTelemetry spans and metrics for the active request.
- `GET /api/showcase/levels/level10/enterprise`: Executes enterprise testing harness with `TenantContextBuilder`.
- `GET /api/level11/verify`: Executes exhaustive verification of all 148 public APIs.

---

## Seed Demonstration Tenants

The Showcase preconfigures the following test tenants in `SeedTenants`:

| Tenant | `TenantId` | Name | Status | Connection String |
|:---|:---|:---|:---:|:---|
| **Acme Corp** | `11111111-1111-1111-1111-111111111111` | `acme-corp` | Active | `Host=localhost;Database=acme_db;Username=postgres;Password=secret` |
| **Globex** | `22222222-2222-2222-2222-222222222222` | `globex` | Active | `Host=localhost;Database=globex_db;Username=postgres;Password=secret` |
| **Inactive** | `33333333-3333-3333-3333-333333333333` | `suspended-tenant` | Inactive | `null` |

To test protected endpoints using tools such as `curl` or Postman, supply the appropriate tenant header or shared gateway token:
```bash
curl -H "X-Tenant-ID: 11111111-1111-1111-1111-111111111111" -H "X-Gateway-Secret: gateway-secret-token" http://localhost:5000/api/quickstart/current-tenant
```

---

## Test Infrastructure and Test Doubles

The Showcase utilizes test doubles provided by `EricksonLopez.MultiTenancy.Testing` (`FakeDbConnection`, `FakeTenantStore`, `TenantContextBuilder`), enabling fully autonomous execution without requiring local installations of PostgreSQL, SQL Server, or Docker during continuous integration runs.
