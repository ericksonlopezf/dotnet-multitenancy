# Master Feature Matrix & Capability Inventory — EricksonLopez.MultiTenancy

> **Version**: 1.0.0  
> **Status**: Production Ready  
> **Architecture**: Clean Architecture / DDD / Native AOT-First  
> **Supported Runtimes**: .NET 8.0, .NET 9.0, .NET 10.0  

---

## 1. Package Inventory & Architectural Boundaries

| Package Name | Nuget Identifier | Layer / Scope | Primary Responsibilities | Native AOT |
|---|---|---|---|:---:|
| `EricksonLopez.MultiTenancy.Abstractions` | `EricksonLopez.MultiTenancy.Abstractions` | **Core Contracts** | `TenantId`, `ITenantContext`, `ITenantStore`, `ITenantResolutionStrategy`, `TenantErrors`. Zero external dependencies. | ✅ 100% |
| `EricksonLopez.MultiTenancy` | `EricksonLopez.MultiTenancy` | **Core Engine** | `ScopedTenantContextAccessor`, `InMemoryTenantStore`, `TenantContextBuilder`, DI registration extensions. | ✅ 100% |
| `EricksonLopez.MultiTenancy.AspNetCore` | `EricksonLopez.MultiTenancy.AspNetCore` | **Web Presentation** | `TenantResolutionMiddleware`, Header/Claim/Host/Route strategies, endpoint routing, per-tenant options. | ✅ 100% |
| `EricksonLopez.MultiTenancy.Authentication` | `EricksonLopez.MultiTenancy.Authentication` | **Security / Identity** | Per-tenant authentication schemes, dynamic cookie naming, per-tenant JWT validation. | ✅ 100% |
| `EricksonLopez.MultiTenancy.Configuration` | `EricksonLopez.MultiTenancy.Configuration` | **Infrastructure / Config** | `ITenantConfigurationProvider`, dynamic JSON/environment per-tenant options binding. | ✅ 100% |
| `EricksonLopez.MultiTenancy.Dapper` | `EricksonLopez.MultiTenancy.Dapper` | **Data Access (Layer 1)** | `WithTenant()`, `CreateTenantParameters()`, Dapper integration extensions. | ✅ 100% |
| `EricksonLopez.MultiTenancy.PostgreSql` | `EricksonLopez.MultiTenancy.PostgreSql` | **Database Dialect** | PostgreSQL RLS integration (`SET LOCAL app.current_tenant_id`), `PostgreSqlTenantStore` with JSONB. | ✅ 100% |
| `EricksonLopez.MultiTenancy.SqlServer` | `EricksonLopez.MultiTenancy.SqlServer` | **Database Dialect** | SQL Server `sp_set_session_context` integration, RLS predicate binding. | ✅ 100% |
| `EricksonLopez.MultiTenancy.MySql` | `EricksonLopez.MultiTenancy.MySql` | **Database Dialect** | MySQL session variable parameterization and connection routing. | ✅ 100% |
| `EricksonLopez.MultiTenancy.MariaDb` | `EricksonLopez.MultiTenancy.MariaDb` | **Database Dialect** | MariaDB session context parameterization and connection routing. | ✅ 100% |
| `EricksonLopez.MultiTenancy.Oracle` | `EricksonLopez.MultiTenancy.Oracle` | **Database Dialect** | Oracle Virtual Private Database (VPD) `SYS_CONTEXT` integration. | ✅ 100% |
| `EricksonLopez.MultiTenancy.Sqlite` | `EricksonLopez.MultiTenancy.Sqlite` | **Database Dialect** | SQLite table/parameter isolation for local integration testing. | ✅ 100% |
| `EricksonLopez.MultiTenancy.OpenTelemetry` | `EricksonLopez.MultiTenancy.OpenTelemetry` | **Observability** | Activity enrichment, Baggage propagation, multi-tenancy metrics. | ✅ 100% |
| `EricksonLopez.MultiTenancy.Testing` | `EricksonLopez.MultiTenancy.Testing` | **Test Harness** | `FakeDbConnection`, `FakeDbTransaction`, `TenantContextBuilder`, `FakeTenantStore`. | ✅ 100% |
| `EricksonLopez.MultiTenancy.Analyzers` | `EricksonLopez.MultiTenancy.Analyzers` | **Static Analysis** | Roslyn Analyzers `ELMT001`, `ELMT002`, `ELMT003` for compile-time safety. | ✅ N/A |

---

## 2. 4-Layer Defense-in-Depth Security Matrix

| Layer | Enforcement Mechanism | Failure Mode If Broken | Protection Provided By |
|---|---|---|---|
| **Layer 1: Explicit Parameterization** | `parameters.WithTenant(_tenantContext)` in Dapper queries | Query would return empty or fail syntax if omitted; verified by Analyzer `ELMT003`. | Application Code / Dapper Extensions |
| **Layer 2: Scoped Context Isolation** | `ScopedTenantContextAccessor` write-once scoped lifecycle | Prevents concurrent request cross-talk; verified by Analyzers `ELMT001` and `ELMT002`. | Dependency Injection Container |
| **Layer 3: Transaction-Scoped Session Context** | `SET LOCAL app.current_tenant_id = @id` (PostgreSQL) / `sp_set_session_context` (SQL Server) | Variable resets automatically on transaction end; cannot leak to connection pool. | Relational Database Driver |
| **Layer 4: Database Row Level Security (RLS)** | Relational Engine Kernel Security Policies (`FORCE ROW LEVEL SECURITY`) | Database engine rejects queries attempting to read/write foreign tenant rows even if app code is compromised. | Database Kernel Engine |

---

## 3. Database Dialect Feature Matrix

| Dialect | Package | Multi-Tenant Model | Session Context Mechanism | Native AOT Verified | Connection Pooling Safe |
|---|---|---|---|:---:|:---:|
| **PostgreSQL** | `EricksonLopez.MultiTenancy.PostgreSql` | Shared DB / Shared Schema (RLS) | `SET LOCAL app.current_tenant_id` | ✅ Yes | ✅ Yes (Transaction Scoped) |
| **SQL Server** | `EricksonLopez.MultiTenancy.SqlServer` | Shared DB / Shared Schema (RLS) | `EXEC sp_set_session_context` | ✅ Yes | ✅ Yes (Reset on release) |
| **MySQL** | `EricksonLopez.MultiTenancy.MySql` | Shared DB / Discriminator Column | `@app_tenant_id` session variable | ✅ Yes | ✅ Yes |
| **MariaDB** | `EricksonLopez.MultiTenancy.MariaDb` | Shared DB / Discriminator Column | `@app_tenant_id` session variable | ✅ Yes | ✅ Yes |
| **Oracle** | `EricksonLopez.MultiTenancy.Oracle` | Oracle VPD / SYS_CONTEXT | `DBMS_SESSION.SET_CONTEXT` | ✅ Yes | ✅ Yes |
| **SQLite** | `EricksonLopez.MultiTenancy.Sqlite` | Database-per-tenant / In-Memory | Explicit Parameter Binding | ✅ Yes | ✅ Yes |

---

## 4. Tenant Resolution Strategies

| Strategy Name | Source Mechanism | Precedence | Conflict Detection | Header / Param Customization |
|---|---|:---:|:---:|:---:|
| **Claim** | JWT `tenant_id`, `tid`, `tenant` | 1 (Highest) | ✅ Yes | Custom Claim Type |
| **Header** | HTTP Request Header (`X-Tenant-ID`) | 2 | ✅ Yes | Custom Header Name |
| **Route** | Route Pattern (`/{tenant}/...`) | 3 | ✅ Yes | Custom Route Parameter |
| **BasePath** | First URL Path Segment (`/tenant-id/...`) | 4 | ✅ Yes | Automatic Extraction |
| **HostName** | Subdomain (`tenant.domain.com`) | 5 | ✅ Yes | Subdomain Lookup |
| **Delegate** | Custom programmatic lambda | Custom | ✅ Yes | Full programmatic control |
| **Static** | Hardcoded tenant for background workers | Standalone | N/A | Deterministic binding |
