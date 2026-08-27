# Architectural & Visual Diagrams — EricksonLopez.MultiTenancy

This document provides visual diagrams illustrating the structural relationships, request lifecycles, database security mechanics, and isolation boundaries of `EricksonLopez.MultiTenancy`.

---

## 1. 4-Layer Defense-in-Depth Model

```mermaid
flowchart TD
    subgraph L1["Layer 1: Application Layer"]
        L1_Desc["Explicit SQL Query Parameters\nWHERE tenant_id = @TenantId"]
    end

    subgraph L2["Layer 2: Infrastructure Layer"]
        L2_Desc["Scoped Write-Once Accessor\nDapper Parameters (WithTenant)"]
    end

    subgraph L3["Layer 3: Transaction-Scoped Context"]
        L3_Desc["Atomic SET LOCAL app.current_tenant_id\nWithin Explicit Database Transaction"]
    end

    subgraph L4["Layer 4: Database Engine RLS"]
        L4_Desc["PostgreSQL FORCE ROW LEVEL SECURITY\nRESTRICTIVE USING & WITH CHECK Policies"]
    end

    L1 --> L2
    L2 --> L3
    L3 --> L4
```

---

## 2. HTTP Request Resolution Pipeline

```mermaid
sequenceDiagram
    autonumber
    actor Client as HTTP Client
    participant MW as TenantResolutionMiddleware
    participant Claims as ClaimTenantResolutionStrategy
    participant Host as HostNameTenantResolutionStrategy
    participant Route as RouteTenantResolutionStrategy
    participant Header as HeaderTenantResolutionStrategy
    participant Store as ITenantStore
    participant Acc as ScopedTenantContextAccessor
    participant Endpoint as Minimal API / Controller

    Client->>MW: Incoming HTTP Request
    
    MW->>Claims: ResolveFromClaims(HttpContext)
    alt Claims Found
        Claims-->>MW: Candidate TenantId (A)
    else No Claims
        MW->>Host: ResolveFromHost(HttpContext)
        alt Host Match
            Host-->>MW: Candidate TenantId (B)
        else No Host Match
            MW->>Route: ResolveFromRoute(HttpContext)
            alt Route Match
                Route-->>MW: Candidate TenantId (C)
            else No Route Match
                MW->>Header: ResolveFromHeader(HttpContext)
                Header-->>MW: Candidate TenantId (D)
            end
        end
    end

    opt Conflict Check (ADR-008)
        Note over MW: Verifies that no two strategies resolve conflicting tenant IDs
    end

    MW->>Store: GetTenantAsync(resolvedTenantId)
    Store-->>MW: ITenantInfo

    alt Tenant Inactive or Not Found
        MW-->>Client: HTTP 401 Unauthorized / 404 Not Found
    else Tenant Active
        MW->>Acc: Set TenantContext (Write-Once)
        MW->>Endpoint: Invoke Next(HttpContext)
        Endpoint-->>Client: HTTP 200 OK
    end
```

---

## 3. PostgreSQL RLS Transaction Lifecycle

```mermaid
sequenceDiagram
    autonumber
    participant App as Application Code
    participant Ext as PostgreSqlRlsExtensions
    participant Conn as NpgsqlConnection
    participant DB as PostgreSQL Database

    App->>Ext: connection.BeginTenantTransactionAsync(tenantContext)
    Ext->>Conn: OpenAsync()
    Ext->>Conn: BeginTransactionAsync()
    Ext->>DB: SET LOCAL app.current_tenant_id = '<guid>' (Transaction Scoped)
    Ext-->>App: ITenantTransaction Wrapper

    App->>DB: SELECT * FROM invoices (Filtered by RLS automatically)
    DB-->>App: Rows matching app.current_tenant_id

    App->>Ext: transaction.CommitAsync()
    Ext->>DB: COMMIT (Automatically clears SET LOCAL context)
    Ext->>Conn: Close/Return Connection to Pool
    Note over Conn,DB: Physical connection returns to pool in 100% clean state
```

---

## 4. Background Job & Message Consumer Scoping

```mermaid
flowchart LR
    Consumer["Message Consumer / Job"] -->|"1. Extract TenantId"| Store["ITenantStore"]
    Store -->|"2. Return ITenantInfo"| Factory["ITenantScopeFactory"]
    Factory -->|"3. CreateScope(tenantInfo)"| Scope["ITenantScope"]
    
    subgraph IsolatedScope["Isolated DI Container Scope"]
        Scope --> Context["Scoped ITenantContext"]
        Scope --> Services["Scoped Repositories & DbConnection"]
    end
    
    Services -->|"4. Execute Work"| Process["Process Message"]
    Process -->|"5. Scope Disposed"| Clean["Clean Teardown"]
```

---

## 5. Ecosystem Package Dependency Tree

```mermaid
graph TD
    Abstractions["EricksonLopez.MultiTenancy.Abstractions"]
    
    Core["EricksonLopez.MultiTenancy"]
    Analyzers["EricksonLopez.MultiTenancy.Analyzers"]
    AspNetCore["EricksonLopez.MultiTenancy.AspNetCore"]
    Authentication["EricksonLopez.MultiTenancy.Authentication"]
    Configuration["EricksonLopez.MultiTenancy.Configuration"]
    Dapper["EricksonLopez.MultiTenancy.Dapper"]
    OpenTelemetry["EricksonLopez.MultiTenancy.OpenTelemetry"]
    Testing["EricksonLopez.MultiTenancy.Testing"]
    
    PostgreSql["EricksonLopez.MultiTenancy.PostgreSql"]
    SqlServer["EricksonLopez.MultiTenancy.SqlServer"]
    MySql["EricksonLopez.MultiTenancy.MySql"]
    MariaDb["EricksonLopez.MultiTenancy.MariaDb"]
    Oracle["EricksonLopez.MultiTenancy.Oracle"]
    Sqlite["EricksonLopez.MultiTenancy.Sqlite"]

    Core --> Abstractions
    Analyzers --> Abstractions
    AspNetCore --> Abstractions
    Authentication --> AspNetCore
    Configuration --> Core
    Configuration --> Abstractions
    Dapper --> Abstractions
    OpenTelemetry --> Abstractions
    Testing --> Core

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
