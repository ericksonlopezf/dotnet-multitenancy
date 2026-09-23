# Architecture and Flow Diagrams — EricksonLopez.MultiTenancy

This document provides visual diagrams in Mermaid syntax illustrating the overall architecture, package dependencies, context lifecycle, HTTP pipeline, background processing, error handling, and database-level isolation of `EricksonLopez.MultiTenancy`.

---

## 1. General System Architecture

```mermaid
flowchart TB
    subgraph ClientTier["Client Tier & Gateway"]
        Web["Single Page App / Web"]
        Mobile["Mobile App"]
        Gateway["API Gateway / Ingress"]
        MsgBroker["Message Broker (RabbitMQ/Kafka)"]
    end

    subgraph PresentationTier["Presentation Tier (ASP.NET Core)"]
        MW["TenantResolutionMiddleware\n(ADR-008 Conflict Detection)"]
        Strategies["Resolution Strategies:\n- Claims (JWT)\n- Host (Subdomain)\n- Route (/api/{tenantId})\n- BasePath (/{tenant}/api)\n- Header (Internal Gateway)"]
        Filters["Endpoint Filters:\nRequireTenant() / AllowAnonymousTenant()"]
        Auth["Per-Tenant Authentication\nTenantCookieAuthenticationEvents"]
    end

    subgraph DomainCoreTier["Domain & Core Engine Tier"]
        Accessor["ScopedTenantContextAccessor\n(Write-Once Invariant)"]
        Context["ITenantContext / ITenantInfo\n(TenantId, IsResolved, IsActive)"]
        ScopeFactory["ITenantScopeFactory\n(Isolated Background Scopes)"]
        Telemetry["OpenTelemetry Enricher\n(ActivitySource & TenantMetrics)"]
    end

    subgraph CatalogTier["Tenant Catalog Tier"]
        Store["ITenantStore / ITenantLookupStore"]
        StoreCache["CachedTenantStore\n(IMemoryCache)"]
        StorePg["PostgreSqlTenantStore"]
        StoreHttp["HttpRemoteTenantStore"]
        StoreMem["InMemoryTenantStore"]
    end

    subgraph DataTier["Persistence & Isolation Tier (4-Layer Defense-in-Depth)"]
        DapperExt["Dapper Extensions\n(WithTenant, CreateTenantParameters)"]
        RlsPostgres["PostgreSQL RLS\n(SET LOCAL app.current_tenant_id)"]
        RlsSql["SQL Server\n(SESSION_CONTEXT)"]
        RlsMySql["MySQL / MariaDB\n(@tenant_id)"]
        RlsOracle["Oracle VPD\n(DBMS_SESSION.SET_IDENTIFIER)"]
        DbPerTenant["SQLite\n(SqliteTenantConnectionFactory)"]
    end

    Gateway --> MW
    Web --> Gateway
    Mobile --> Gateway
    MsgBroker --> ScopeFactory

    MW --> Strategies
    MW --> Store
    Store --> StoreCache
    StoreCache --> StorePg
    StoreCache --> StoreHttp
    StoreCache --> StoreMem

    MW --> Accessor
    Accessor --> Context
    Filters --> Context
    Auth --> Context
    Context --> Telemetry

    Context --> DapperExt
    DapperExt --> RlsPostgres
    DapperExt --> RlsSql
    DapperExt --> RlsMySql
    DapperExt --> RlsOracle
    DapperExt --> DbPerTenant
```

---

## 2. Primary System Flow (HTTP Request-Response)

```mermaid
sequenceDiagram
    autonumber
    actor Client as HTTP Client / Gateway
    participant MW as TenantResolutionMiddleware
    participant Strat as Strategies (Claims, Host, Route, Header)
    participant Store as ITenantStore / CachedTenantStore
    participant Acc as ScopedTenantContextAccessor
    participant Filter as RequireTenantFilter
    participant App as Handler / Repository (Dapper)
    participant DB as Database Engine (RLS)

    Client->>MW: Incoming HTTP Request
    MW->>Strat: Execute strategies in precedence order
    Strat-->>MW: Candidate TenantId identifiers
    
    alt Resolution conflict detected (ADR-008)
        MW-->>Client: HTTP 400 Bad Request (TenantResolutionConflictException)
    else TenantId successfully resolved
        MW->>Store: GetTenantAsync(tenantId)
        Store-->>MW: Result<ITenantInfo>
        
        alt Tenant not found or inactive
            MW-->>Client: HTTP 404 Not Found / HTTP 401 Unauthorized
        else Tenant active
            MW->>Acc: Set TenantContext (Write-once)
            MW->>Filter: Invoke next middleware
            Filter->>Filter: Verify IsResolved && IsActive
            Filter->>App: Invoke Minimal API Endpoint
            
            App->>DB: Begin RLS transaction (SET LOCAL app.current_tenant_id)
            App->>DB: Execute SELECT/INSERT with WHERE tenant_id = @TenantId
            DB-->>App: Rows filtered at kernel/database level
            App->>DB: Commit transaction (Purges SET LOCAL)
            
            App-->>Client: HTTP 200 OK Response
        end
    end
```

---

## 3. Context State Machine (`ITenantContext`)

```mermaid
stateDiagram-v2
    [*] --> Unresolved: Scope / Request Initialization

    state Unresolved {
        [*] --> EmptyContext
        EmptyContext: IsResolved = false
        EmptyContext: Tenant = null
        EmptyContext: Source = None
        EmptyContext: RequiredTenant throws TenantNotFoundException
    }

    Unresolved --> Resolving: TenantResolutionMiddleware / ITenantScopeFactory

    state Resolving {
        [*] --> EvaluatingStrategies
        EvaluatingStrategies: Evaluate Claims > Host > Route > Header
        EvaluatingStrategies --> ConflictDetected: Multiple strategies conflict
        EvaluatingStrategies --> SingleCandidate: Single TenantId extracted
    }

    Resolving --> RejectedConflict: Conflict Detected (ADR-008)
    RejectedConflict: Throw TenantResolutionConflictException (Fail-Closed)
    RejectedConflict --> [*]

    Resolving --> ValidatingStore: Candidate TenantId resolved
    state ValidatingStore {
        [*] --> QueryStore
        QueryStore --> InactiveFound: IsActive == false
        QueryStore --> NotFoundInStore: Tenant does not exist in catalog
        QueryStore --> ActiveFound: IsActive == true
    }

    ValidatingStore --> RejectedInactive: Inactive Tenant
    RejectedInactive: IsResolved = false
    RejectedInactive: RequiredTenant throws TenantInactiveException
    RejectedInactive --> [*]

    ValidatingStore --> RejectedNotFound: Tenant Not Found
    RejectedNotFound: IsResolved = false
    RejectedNotFound: RequiredTenant throws TenantNotFoundException
    RejectedNotFound --> [*]

    ValidatingStore --> Resolved: Active Tenant Confirmed
    state Resolved {
        [*] --> ActiveContext
        ActiveContext: IsResolved = true
        ActiveContext: Tenant != null
        ActiveContext: RequiredTenant returns ITenantInfo
        ActiveContext: Immutable (Write-Once)
    }

    Resolved --> [*]: Scope / Request End (Dispose)
```

---

## 4. Database Isolation Pipeline (4-Layer Defense-in-Depth)

```mermaid
flowchart TD
    subgraph L1["Layer 1: Application Code (Explicit SQL)"]
        L1_SQL["WHERE tenant_id = @TenantId\nPrevents logical collisions in queries"]
    end

    subgraph L2["Layer 2: Dapper Infrastructure (Strong Typing)"]
        L2_Params["DynamicParameters.WithTenant(context)\nTenantDapperExtensions guarantees safe GUID mapping"]
    end

    subgraph L3["Layer 3: Connection Transaction Boundary"]
        L3_Tx["BeginTenantTransactionAsync()\nInitializes session context prior to query execution:\n- PostgreSQL: SET LOCAL app.current_tenant_id = '...'\n- SQL Server: sp_set_session_context\n- MySQL/MariaDB: SET @tenant_id = '...'\n- Oracle: DBMS_SESSION.SET_IDENTIFIER"]
    end

    subgraph L4["Layer 4: Database Engine (Kernel RLS)"]
        L4_Engine["FORCE ROW LEVEL SECURITY\nRESTRICTIVE USING and WITH CHECK policies\nImpossible to bypass even if application code is defective"]
    end

    L1 --> L2
    L2 --> L3
    L3 --> L4
```

---

## 5. Background Processing (`ITenantScopeFactory`)

```mermaid
sequenceDiagram
    autonumber
    participant Job as Background Worker / Consumer
    participant Store as ITenantStore
    participant Factory as ITenantScopeFactory
    participant Scope as ITenantScope
    participant SP as Scope ServiceProvider
    participant Repo as IInvoiceRepository
    participant DB as Database Engine

    Job->>Store: GetTenantAsync(targetTenantId)
    Store-->>Job: ITenantInfo (Active)
    Job->>Factory: CreateScope(tenant, TenantResolutionSource.BackgroundJob)
    Factory->>Scope: Instantiate isolated scope with ScopedTenantContextAccessor
    Factory-->>Job: using var scope

    Job->>SP: GetRequiredService<IInvoiceRepository>()
    SP-->>Job: Repository injected with scope ITenantContext
    
    Job->>Repo: ProcessInvoicesForTenantAsync()
    Repo->>DB: Parameterized queries bound to scope tenant
    DB-->>Repo: Isolated partition data
    
    Job->>Scope: DisposeAsync()
    Scope->>Scope: Destroy scoped services and clear tenant context
```

---

## 6. Observability and OpenTelemetry Telemetry Pipeline

```mermaid
flowchart LR
    subgraph Request["Request Lifecycle"]
        In["Incoming Request"] --> Res["Tenant Resolution"]
        Res --> Proc["Business Processing"]
        Proc --> Out["Outgoing Response"]
    end

    subgraph TelemetrySubsystem["OpenTelemetry Subsystem"]
        Source["TenantActivitySource\n('EricksonLopez.MultiTenancy')"]
        Metrics["TenantMetrics\n('EricksonLopez.MultiTenancy')"]
        Baggage["W3C Baggage\n('tenant.id')"]
    end

    subgraph Collectors["Collection Systems & APM"]
        OtelCollector["OpenTelemetry Collector"]
        Jaeger["Jaeger / Zipkin (Tracing)"]
        Prometheus["Prometheus / Grafana (Metrics)"]
    end

    Res -->|"EnrichWithTenant(context)"| Source
    Res -->|"RecordResolutionSuccess / Failure"| Metrics
    Res -->|"SetTenantBaggage(tenantId)"| Baggage

    Source --> OtelCollector
    Metrics --> OtelCollector
    Baggage -->|"Downstream HTTP propagation"| Out

    OtelCollector --> Jaeger
    OtelCollector --> Prometheus
```

---

## 7. Component Dependency Hierarchy

```mermaid
graph TD
    Abstractions["EricksonLopez.MultiTenancy.Abstractions\n(L0: Primitives, TenantId, Contracts)"]
    
    Core["EricksonLopez.MultiTenancy\n(L1: Scopes, In-Memory/HTTP/Cached Stores, HealthChecks)"]
    Analyzers["EricksonLopez.MultiTenancy.Analyzers\n(L2: Roslyn Code Fixes & Diagnostics)"]
    AspNetCore["EricksonLopez.MultiTenancy.AspNetCore\n(L3: Middleware, HTTP Strategies, Routing)"]
    Authentication["EricksonLopez.MultiTenancy.Authentication\n(L3: Per-Tenant Auth Schemes)"]
    Configuration["EricksonLopez.MultiTenancy.Configuration\n(L3: Store for appsettings.json)"]
    Dapper["EricksonLopez.MultiTenancy.Dapper\n(L4: DynamicParameters Extensions)"]
    OpenTelemetry["EricksonLopez.MultiTenancy.OpenTelemetry\n(L6: ActivitySource, Metrics, Baggage)"]
    Testing["EricksonLopez.MultiTenancy.Testing\n(L7: Fakes, Test Doubles, Harness)"]
    
    PostgreSql["EricksonLopez.MultiTenancy.PostgreSql\n(L5: RLS SET LOCAL)"]
    SqlServer["EricksonLopez.MultiTenancy.SqlServer\n(L5: SESSION_CONTEXT)"]
    MySql["EricksonLopez.MultiTenancy.MySql\n(L5: Session Variables)"]
    MariaDb["EricksonLopez.MultiTenancy.MariaDb\n(L5: Session Variables)"]
    Oracle["EricksonLopez.MultiTenancy.Oracle\n(L5: DBMS_SESSION VPD)"]
    Sqlite["EricksonLopez.MultiTenancy.Sqlite\n(L5: Connection Factory)"]

    Core --> Abstractions
    Analyzers --> Abstractions
    AspNetCore --> Abstractions
    Authentication --> AspNetCore
    Configuration --> Core
    Configuration --> Abstractions
    Dapper --> Abstractions
    OpenTelemetry --> Abstractions
    Testing --> Core
    Testing --> Abstractions

    PostgreSql --> Abstractions
    PostgreSql --> Dapper
    SqlServer --> Abstractions
    SqlServer --> Dapper
    MySql --> Abstractions
    MySql --> Dapper
    MariaDb --> Abstractions
    MariaDb --> Dapper
    Oracle --> Abstractions
    Oracle --> Dapper
    Sqlite --> Abstractions
    Sqlite --> Dapper
```
