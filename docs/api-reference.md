# Public API Reference — EricksonLopez.MultiTenancy

Detailed technical documentation of all public types, interfaces, structs, classes, extension methods, and error catalogs across the `EricksonLopez.MultiTenancy` ecosystem.

> All types, interfaces, and method signatures in this document are verified against the source code in `src/`. The **code is the source of truth**.

---

## Table of Contents

1. [EricksonLopez.MultiTenancy.Abstractions](#1-ericksonlopezmultitenancyabstractions)
2. [EricksonLopez.MultiTenancy (Core Engine)](#2-ericksonlopezmultitenancy-core-engine)
3. [EricksonLopez.MultiTenancy.Analyzers](#3-ericksonlopezmultitenancyanalyzers)
4. [EricksonLopez.MultiTenancy.AspNetCore](#4-ericksonlopezmultitenancyaspnetcore)
5. [EricksonLopez.MultiTenancy.Authentication](#5-ericksonlopezmultitenancyauthentication)
6. [EricksonLopez.MultiTenancy.Configuration](#6-ericksonlopezmultitenancyconfiguration)
7. [EricksonLopez.MultiTenancy.Dapper](#7-ericksonlopezmultitenancydapper)
8. [EricksonLopez.MultiTenancy.OpenTelemetry](#8-ericksonlopezmultitenancyopentelemetry)
9. [Database Dialect Packages](#9-database-dialect-packages)
10. [EricksonLopez.MultiTenancy.Testing](#10-ericksonlopezmultitenancytesting)

---

## 1. EricksonLopez.MultiTenancy.Abstractions

Foundational contracts and value objects with zero external dependencies (pure BCL and `EricksonLopez.Result`). All types live in namespace `EricksonLopez.MultiTenancy`.

### `TenantId` (Struct)
Strongly-typed, immutable, readonly record struct wrapping a `Guid`.

```csharp
[JsonConverter(typeof(TenantIdJsonConverter))]
public readonly record struct TenantId : IEquatable<TenantId>, IComparable<TenantId>, IComparable
```

- **Static Sentinels:**
  - `TenantId.Empty`: Sentinel representing an uninitialized/empty identifier (`Guid.Empty`).
- **Static Factory Methods:**
  - `TenantId.NewId()`: Generates a new random `TenantId`.
  - `TenantId.Create(Guid value)`: Returns `Result<TenantId>`. Failure if `value == Guid.Empty`.
  - `TenantId.Create(string value)`: Returns `Result<TenantId>`. Failure on invalid GUID string or empty.
  - `TenantId.From(Guid value)`: Returns `Result<TenantId>`. Allows `Guid.Empty` (returns `TenantId.Empty`).
  - `TenantId.From(string value)`: Returns `Result<TenantId>`. Parses the GUID string; allows empty GUID.
  - `TenantId.TryCreate(string? value, out TenantId result)`: Safe parsing without exceptions. Returns `true` if valid and non-empty.
  - `TenantId.TryCreate(Guid value, out TenantId result)`: Safe creation without exceptions. Returns `true` if non-empty.
- **Instance Properties:**
  - `Value` (`Guid`): Underlying GUID value.
  - `IsEmpty` (`bool`): Returns `true` if `Value == Guid.Empty`.
- **Conversions:**
  - Implicit conversion to `Guid` (`Guid guid = tenantId;`).
  - Explicit conversion to `string` (`string s = (string)tenantId;`).
- **JSON Serialization:**
  - Handled by `TenantIdJsonConverter` (namespace `EricksonLopez.MultiTenancy.Serialization`) — reads/writes as a UUID string.

---

### `ITenantInfo` (Interface)
Contract representing tenant metadata.

```csharp
public interface ITenantInfo
{
    TenantId Id { get; }
    string Name { get; }
    bool IsActive { get; }
    string? ConnectionString { get; }
}
```

#### `TenantInfo` (Default Implementation)
Immutable `record class` implementing `ITenantInfo`.

```csharp
public sealed record TenantInfo(
    TenantId Id,
    string Name,
    bool IsActive = true,
    string? ConnectionString = null) : ITenantInfo;
```

---

### `ITenantContext` & `ITenantContext<TTenant>` (Interfaces)
Immutable container representing the tenant context of the current execution unit.

```csharp
public interface ITenantContext
{
    // Resolved tenant metadata, or null if not yet resolved.
    ITenantInfo? Tenant { get; }

    // True only when Tenant != null, Tenant.Id != TenantId.Empty, AND Tenant.IsActive == true.
    [MemberNotNullWhen(true, nameof(Tenant))]
    bool IsResolved { get; }

    // Returns TenantResolutionSource.None when unresolved.
    TenantResolutionSource Source { get; }

    // Default interface implementation — throws TenantNotFoundException if !IsResolved.
    ITenantInfo RequiredTenant { get; }
}

// Strongly-typed variant.
public interface ITenantContext<out TTenant> : ITenantContext where TTenant : class, ITenantInfo
{
    new TTenant? Tenant { get; }
}
```

> **Important:** `IsResolved` returns `false` for inactive tenants even when the tenant record exists in the store. This is a security invariant — inactive tenants must never have an active resolved context.

#### `TenantContext` (Static Factory & Default Implementation)

```csharp
// Unresolved sentinel — returned when no tenant has been resolved.
public static ITenantContext Empty { get; }

// Create a resolved context from tenant metadata.
public static ITenantContext Create(ITenantInfo tenant,
    TenantResolutionSource source = TenantResolutionSource.None);

// Create a strongly-typed resolved context.
public static ITenantContext<TTenant> Create<TTenant>(TTenant tenant,
    TenantResolutionSource source = TenantResolutionSource.None)
    where TTenant : class, ITenantInfo;
```

---

### `ITenantContextAccessor` (Interface)
Defines the contract for reading and setting the tenant context for the active scope.

```csharp
public interface ITenantContextAccessor
{
    ITenantContext? TenantContext { get; set; }
}
```

- **Invariants:**
  - Registered as **Scoped** (one instance per DI scope / HTTP request).
  - Write-once per scope. Re-assigning after the context has been set throws `InvalidOperationException`.
  - Contains no static state — no `AsyncLocal<T>` fields (see [ADR-002](adr/adr-002-redesign-async-local-accessor.md)).

---

### `ITenantScope` & `ITenantScopeFactory` (Interfaces)
Contracts for executing non-HTTP background jobs in an isolated DI scope.

```csharp
public interface ITenantScopeFactory
{
    // source defaults to TenantResolutionSource.ExplicitScope.
    // Throws ArgumentNullException if tenant is null.
    // Throws ArgumentException if tenant.Id == TenantId.Empty.
    ITenantScope CreateScope(ITenantInfo tenant,
        TenantResolutionSource source = TenantResolutionSource.ExplicitScope);
}

public interface ITenantScope : IAsyncDisposable, IDisposable
{
    IServiceProvider ServiceProvider { get; }
    ITenantContext TenantContext { get; }
}
```

---

### `ITenantStore` & `ITenantLookupStore` (Interfaces)
Contracts for tenant persistence and query resolution. All methods use the `Result<T>` monad to avoid exceptions in the happy path.

```csharp
public interface ITenantStore
{
    Task<Result<ITenantInfo>> GetTenantAsync(TenantId tenantId,
        CancellationToken cancellationToken = default);
}

// Strongly-typed variant.
public interface ITenantStore<TTenant> : ITenantStore where TTenant : class, ITenantInfo
{
    new Task<Result<TTenant>> GetTenantAsync(TenantId tenantId,
        CancellationToken cancellationToken = default);
}

public interface ITenantLookupStore : ITenantStore
{
    // Resolves by tenant Name or TenantId string.
    Task<Result<ITenantInfo>> GetTenantByIdentifierAsync(string identifier,
        CancellationToken cancellationToken = default);
}

// Strongly-typed variant.
public interface ITenantLookupStore<TTenant> : ITenantStore<TTenant>, ITenantLookupStore
    where TTenant : class, ITenantInfo
{
    new Task<Result<TTenant>> GetTenantByIdentifierAsync(string identifier,
        CancellationToken cancellationToken = default);
}
```

> **Note:** There is no `GetByNameAsync` or `ExistsAsync` method. Use `GetTenantByIdentifierAsync` for name-based or string-key lookups, and check `result.IsSuccess` in place of `ExistsAsync`.

---

### `ITenantEntity` (Interface)
Marker interface for domain entities that belong to a specific tenant.

```csharp
public interface ITenantEntity
{
    TenantId TenantId { get; }
}
```

---

### `TenantResolutionSource` (Enum)
Audit trail enumeration indicating how the active tenant was resolved.

```csharp
public enum TenantResolutionSource
{
    None = 0,            // No tenant resolved
    JwtClaim = 1,        // Resolved from authenticated JWT claim (tenant_id, tid, tenant)
    Route = 2,           // Resolved from URL route template
    Host = 3,            // Resolved from hostname or subdomain
    Header = 4,          // Resolved from X-Tenant-ID HTTP header (opt-in only)
    ExplicitScope = 5,   // Established via ITenantScopeFactory
    PlatformAdmin = 6,   // Elevated platform admin context for cross-tenant operations
    BackgroundJob = 7,   // Resolved from background job execution context
    MessageMetadata = 8  // Resolved from message broker metadata
}
```

> **Important for switch statements:** Do not use integer literals — use the enum member names. The numeric assignments above are stable across versions.

---

### `TenantErrors` (Domain Error Catalog)
Standardized functional domain errors returning `EricksonLopez.Result.Error`. All methods return `Error`, never throw.

```csharp
public static class TenantErrors
{
    // Code: "Tenant.NotFound"
    public static Error NotFound(TenantId tenantId);

    // Code: "Tenant.Unresolved" — singleton, no parameters
    public static readonly Error Unresolved;

    // Code: "Tenant.Inactive"
    public static Error Inactive(TenantId tenantId);

    // Code: "Tenant.InvalidId" — for invalid GUID strings or empty values
    public static Error InvalidId(string? value);

    // Code: "Tenant.ResolutionFailed"
    public static Error StrategyFailed(string strategyName, string? reason = null);
}
```

> **There is no `TenantErrors.NotFound(string)` overload** — pass a `TenantId`. Use `TenantErrors.InvalidId(string)` for invalid identifier strings.
> **There is no `TenantErrors.ResolutionConflict` method** — resolution conflicts throw `InvalidOperationException` from the middleware (not a Result error).

#### Exceptions vs. Result errors

| Scenario | Mechanism |
|:---|:---|
| Tenant not found in store | `Result<T>.Failure(TenantErrors.NotFound(id))` |
| Tenant is inactive | `Result<T>.Failure(TenantErrors.Inactive(id))` |
| Tenant context not resolved | `TenantNotFoundException` (thrown by `RequiredTenant`) |
| Resolution conflict (different tenant IDs from two strategies) | `InvalidOperationException` (thrown by middleware) |

---

## 2. EricksonLopez.MultiTenancy (Core Engine)

### `ScopedTenantContextAccessor` (Class)
Write-once scoped accessor. Contains no static `AsyncLocal` state (see [ADR-002](adr/adr-002-redesign-async-local-accessor.md)).

```csharp
public sealed class ScopedTenantContextAccessor : ITenantContextAccessor
```

- **Lifetime:** Scoped — one instance per DI scope / HTTP request.
- The setter throws `InvalidOperationException` if context has already been set in the current scope.

---

### `DefaultTenantScopeFactory` (Class)
Default implementation of `ITenantScopeFactory` that creates an isolated `IServiceScope`, sets the resolved `ITenantContext`, and populates the `ScopedTenantContextAccessor`.

- **Lifetime:** Singleton (stateless factory).
- `CreateScope` throws `ArgumentException` if `tenant.Id == TenantId.Empty`.

---

### `InMemoryTenantStore<TTenant>` (Class)
Thread-safe `ConcurrentDictionary`-backed in-memory store. Suitable for development, testing, and single-instance deployments.

```csharp
public class InMemoryTenantStore<TTenant> : ITenantStore<TTenant>, ITenantLookupStore<TTenant>
    where TTenant : class, ITenantInfo
{
    // Constructors
    public InMemoryTenantStore();
    public InMemoryTenantStore(IEnumerable<TTenant> tenants);

    // Mutation — thread-safe
    public void AddOrUpdate(TTenant tenant);
}

// Convenience non-generic version pre-typed to TenantInfo.
public sealed class InMemoryTenantStore : InMemoryTenantStore<TenantInfo>
```

> **Note:** There is no `TryRemove(TenantId)` method. To deactivate a tenant, call `AddOrUpdate` with a new instance where `IsActive = false`.

---

### `CachedTenantStore<TTenant>` (Class)
High-performance `IMemoryCache`-backed decorator wrapping any `ITenantStore<TTenant>`.

- **Options:** `CachedTenantStoreOptions`

```csharp
public sealed class CachedTenantStoreOptions
{
    // TTL relative to the moment the entry is set (not absolute clock time).
    public TimeSpan AbsoluteExpirationRelativeToNow { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan? SlidingExpiration { get; set; }
}
```

- **Registration:**
```csharp
services.AddCachedTenantStore<TenantInfo>(options =>
{
    options.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
    options.SlidingExpiration = TimeSpan.FromMinutes(15);
});
```

> **Cache eviction:** Entries expire **only via TTL**. There is no active invalidation triggered by tenant status changes. Plan your TTL accordingly.

---

### `HttpRemoteTenantStore<TTenant>` (Class)
Upstream remote HTTP tenant catalog client using `HttpClient` and `System.Text.Json`.

- **Options:** `HttpRemoteTenantStoreOptions`

```csharp
public sealed class HttpRemoteTenantStoreOptions
{
    public Uri? BaseAddress { get; set; }
    // Format placeholder {0} is replaced with TenantId (UUID string).
    public string EndpointTemplate { get; set; } = "/api/tenants/{0}";
    // Format placeholder {0} is replaced with URL-encoded identifier string.
    public string IdentifierEndpointTemplate { get; set; } = "/api/tenants/by-identifier/{0}";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}
```

---

### `MultiTenancyHealthCheck` (Class)
ASP.NET Core Health Checks integration implementing `IHealthCheck`.

```csharp
// Registration
services.AddMultiTenancyHealthCheck(options =>
{
    options.FailureStatus = HealthStatus.Degraded;
    options.IncludeDiagnosticData = true;
});
```

---

### `ServiceCollectionExtensions`
Core DI registration helpers:

```csharp
// Registers ScopedTenantContextAccessor and TenantContext<TenantInfo> (scoped).
public static IServiceCollection AddMultiTenancy(this IServiceCollection services);

// Registers ScopedTenantContextAccessor and TenantContext<TTenant> (scoped).
public static IServiceCollection AddMultiTenancy<TTenant>(this IServiceCollection services)
    where TTenant : class, ITenantInfo;
```

---

## 3. EricksonLopez.MultiTenancy.Analyzers

Roslyn static analysis analyzers active during compilation (targets `netstandard2.0`). See the dedicated rule documentation in [`docs/rules/`](rules/elmt001.md):

| ID | Name | Severity | Detailed Rule Specification | Description |
|:---|:---|:---:|:---:|:---|
| **`ELMT001`** | `TenantContextStaticFieldAnalyzer` | Warning | **[ELMT001 Specification](rules/elmt001.md)** | `ITenantContext` or `ITenantContextAccessor` assigned to a static field |
| **`ELMT002`** | `TenantContextInSingletonAnalyzer` | Warning | **[ELMT002 Specification](rules/elmt002.md)** | `ITenantContext` injected into a Singleton or cache type |
| **`ELMT003`** | `DapperWithoutTenantAnalyzer` | Warning | **[ELMT003 Specification](rules/elmt003.md)** | Dapper SQL queries executed in tenant-aware context without tenant parameters |

---

## 4. EricksonLopez.MultiTenancy.AspNetCore

### `TenantResolutionMiddleware`
Request pipeline middleware that evaluates **all** registered strategies (fail-closed conflict detection per [ADR-008](adr/adr-008-adopt-fail-closed-tenant-resolution-conflict-detection.md)), validates tenant active status, and populates `ITenantContextAccessor`.

If two strategies resolve **different** `TenantId` values for the same request, the middleware logs a warning and throws `InvalidOperationException`.

```csharp
// Registration
app.UseMultiTenancy(); // adds TenantResolutionMiddleware to the pipeline
```

---

### HTTP Resolution Strategies

| Strategy | Precedence | Registration |
|:---|:---|:---|
| `ClaimTenantResolutionStrategy` | Highest (1) | `AddAspNetCoreMultiTenancy()` (default) |
| `HostNameTenantResolutionStrategy` | 2 | `AddHostNameTenantStrategy(...)` |
| `RouteTenantResolutionStrategy` | 3 | `AddRouteTenantStrategy(...)` |
| `BasePathTenantResolutionStrategy` | 4 | `AddBasePathStrategy(...)` |
| `HeaderTenantResolutionStrategy` | Opt-in only | `AddInternalHeaderTenantResolution()` |
| `DelegateTenantResolutionStrategy` | Custom | `AddDelegateTenantStrategy(Func<...>)` |
| `StaticTenantResolutionStrategy` | Test only | `AddStaticTenantStrategy(tenantId)` |

Claims resolved from JWT: `tenant_id`, `tid`, `tenant` claim names (in priority order).

> **Security note:** `HeaderTenantResolutionStrategy` is **not registered by default** to prevent header-based spoofing attacks. See [ADR-003](adr/adr-003-reject-header-first-resolution.md).

```csharp
// Default registration (Claim + Host + Route)
builder.Services.AddAspNetCoreMultiTenancy();

// Add opt-in header strategy for internal services only
builder.Services.AddInternalHeaderTenantResolution();

// Custom delegate strategy
builder.Services.AddDelegateTenantStrategy(async (httpContext) =>
{
    var value = httpContext.Request.Query["tenant"];
    return TenantId.TryCreate(value, out var id) ? id : TenantId.Empty;
});
```

---

### `RequireTenantFilter` & `.RequireTenant()`
Minimal API and MVC endpoint filter. Returns HTTP 401 Unauthorized if `ITenantContext.IsResolved` is `false`.

```csharp
// On individual route
app.MapGet("/api/data", () => Results.Ok()).RequireTenant();

// On a route group
var group = app.MapGroup("/api").RequireTenant();
```

---

### `TenantOptionsCache<TOptions, TTenant>`
Thread-safe per-tenant `IOptionsSnapshot<TOptions>` cache.

```csharp
// Registration
builder.Services.AddPerTenantOptions<MyOptions, TenantInfo>((options, tenant) =>
{
    options.ApiEndpoint = tenant.ConnectionString;
});
```

---

## 5. EricksonLopez.MultiTenancy.Authentication

### `TenantAuthenticationExtensions`
Provides dynamic per-tenant authentication scheme selection and handler routing.

```csharp
builder.Services.AddPerTenantAuthentication<TenantInfo>(options =>
{
    options.DefaultSchemeSelector = tenant => tenant.Id == SpecialTenant ? "CustomScheme" : "Bearer";
});
```

Also provides `TenantCookieAuthenticationEvents<TTenant>` for per-tenant cookie path and domain customization.

---

## 6. EricksonLopez.MultiTenancy.Configuration

### `ConfigurationTenantStore<TTenant>`
Binds tenant lists directly from `IConfiguration` sections (e.g. `appsettings.json`) with live reload support via `IOptionsMonitor`.

```csharp
builder.Services.AddConfigurationTenantStore<TenantInfo>(
    builder.Configuration.GetSection("Tenants"));
```

`appsettings.json` example:
```json
{
  "Tenants": [
    { "Id": "...", "Name": "Acme", "IsActive": true, "ConnectionString": "..." }
  ]
}
```

---

## 7. EricksonLopez.MultiTenancy.Dapper

### `TenantDapperExtensions`
Parameter enrichment helpers for Dapper queries. Both methods live in namespace `EricksonLopez.MultiTenancy.Dapper`.

```csharp
// Appends the tenant identifier to an existing DynamicParameters instance.
// parameterName defaults to "TenantId".
public static DynamicParameters WithTenant(
    this DynamicParameters parameters,
    ITenantContext tenantContext,
    string parameterName = "TenantId");

// Creates a new DynamicParameters instance pre-populated with the tenant identifier.
// parameterName defaults to "TenantId".
public static DynamicParameters CreateTenantParameters(
    this ITenantContext tenantContext,
    string parameterName = "TenantId");
```

**Usage:**

```csharp
// Option A — create a new DynamicParameters with only the tenant ID
var tenantParams = tenantContext.CreateTenantParameters();
var invoices = await connection.QueryAsync<Invoice>(
    "SELECT * FROM invoices WHERE tenant_id = @TenantId",
    tenantParams);

// Option B — add tenant ID to an existing parameters object
var parameters = new DynamicParameters();
parameters.Add("Status", "Pending");
parameters.WithTenant(tenantContext);

var invoices = await connection.QueryAsync<Invoice>(
    "SELECT * FROM invoices WHERE tenant_id = @TenantId AND status = @Status",
    parameters);
```

> **Note:** `CreateTenantParameters` does **not** accept additional parameter objects. To add extra parameters, use `WithTenant` on a pre-populated `DynamicParameters` instance (Option B above).

---

## 8. EricksonLopez.MultiTenancy.OpenTelemetry

### `TenantActivitySource`
Provides the canonical `ActivitySource` and semantic attribute constants.

```csharp
public static class TenantActivitySource
{
    public const string ActivitySourceName = "EricksonLopez.MultiTenancy";
    public static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");

    public static class Tags
    {
        public const string TenantId = "tenant.id";
        public const string TenantName = "tenant.name";
        public const string TenantSource = "tenant.source";
        public const string TenantIsActive = "tenant.is_active";
        public const string ResolutionStrategy = "tenant.resolution_strategy";
    }

    public static class Baggage
    {
        // W3C Baggage key for cross-service tenant propagation.
        public const string TenantId = "tenant.id";
    }
}
```

**Registration:**
```csharp
// Register the trace enricher (writes tags to Activity.Current)
builder.Services.AddMultiTenancyOpenTelemetry();

// Register with OpenTelemetry SDK
tracerProviderBuilder.AddSource(TenantActivitySource.ActivitySourceName);
```

---

### `TenantActivityExtensions`
Extension methods for enriching activities with tenant identity.

```csharp
// Enrich a specific activity (no-op if activity or context is null/unresolved)
Activity? EnrichWithTenant(this Activity? activity, ITenantContext? tenantContext);

// Enrich Activity.Current
void EnrichCurrentActivity(ITenantContext? tenantContext);

// Set W3C baggage for cross-service propagation
Activity? SetTenantBaggage(this Activity? activity, TenantId tenantId);

// Record a resolution failure event on the activity
Activity? RecordTenantResolutionFailure(this Activity? activity, Error error, string? strategyName = null);
```

---

### `TenantMetrics`
Instruments `System.Diagnostics.Metrics` for multi-tenancy operations.

**Metric instruments (exact names for dashboard configuration):**

| Metric Name | Type | Unit | Description |
|:---|:---|:---|:---|
| `tenant.resolution.total` | Counter | `{resolutions}` | Total resolution attempts |
| `tenant.resolution.failures` | Counter | `{failures}` | Total resolution failures |
| `tenant.resolution.conflicts` | Counter | `{conflicts}` | Total resolution conflicts |
| `tenant.resolution.duration` | Histogram | `ms` | Resolution duration in milliseconds |

**Helper methods:**
```csharp
// Record success with strategy name, source, and optional duration
TenantMetrics.RecordResolutionSuccess(string strategyName, TenantResolutionSource source, double durationMs = 0);

// Record failure with strategy name and error code
TenantMetrics.RecordResolutionFailure(string strategyName, string errorCode, double durationMs = 0);

// Record a conflict between two strategies
TenantMetrics.RecordResolutionConflict(string firstStrategy, string secondStrategy);
```

**Registration with OpenTelemetry SDK:**
```csharp
meterProviderBuilder.AddMeter(TenantMetrics.MeterName);
```

> **Note:** `TenantMetrics` is a utility library for application-level instrumentation. Invoke the `Record*` methods from your own middleware or pipeline code where you control the resolution lifecycle.

---

### `TenantTraceEnricher` / `ITenantTraceEnricher`
Interface and default implementation for enriching telemetry activities from a tenant context.

```csharp
public interface ITenantTraceEnricher
{
    void Enrich(ITenantContext context);
}

public sealed class TenantTraceEnricher : ITenantTraceEnricher
```

Registered as `Singleton` via `services.AddMultiTenancyOpenTelemetry()`.

---

## 9. Database Dialect Packages

### PostgreSQL (`EricksonLopez.MultiTenancy.PostgreSql`)

Provides Row Level Security (RLS) enforcement via transaction-scoped `SET LOCAL`.

```csharp
public static class PostgreSqlRlsExtensions
{
    public const string DefaultTenantSessionVariable = "app.current_tenant_id";

    // Executes: SET LOCAL "<sessionVariable>" = '<tenantId>' inside the provided transaction.
    // Throws InvalidOperationException if transaction is null.
    // Throws ArgumentException if sessionVariable is null or whitespace.
    // Throws TenantNotFoundException if tenantContext has an empty TenantId.
    public static Task SetTenantRlsContextAsync(
        this DbConnection connection,
        DbTransaction transaction,        // required — SET LOCAL requires an active transaction
        ITenantContext tenantContext,
        string sessionVariable = DefaultTenantSessionVariable,
        CancellationToken cancellationToken = default);

    // Opens a transaction, executes SetTenantRlsContextAsync, and returns the active transaction.
    // Rolls back and disposes the transaction if SET LOCAL fails.
    public static async Task<DbTransaction> BeginTenantTransactionAsync(
        this DbConnection connection,
        ITenantContext tenantContext,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        string sessionVariable = DefaultTenantSessionVariable,
        CancellationToken cancellationToken = default);
}
```

Required RLS policy pattern:
```sql
CREATE POLICY tenant_isolation ON invoices
  USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
```

See [ADR-009](adr/adr-009-reject-set-session-rls.md) — `SET SESSION` is permanently rejected.

### SQL Server (`EricksonLopez.MultiTenancy.SqlServer`)
- `connection.BeginTenantTransactionAsync(tenantContext)`: Atomically sets `sp_set_session_context 'tenant_id', @TenantId`.
- `connection.ResetTenantSessionContextAsync(transaction)`: Clears session context via `sp_set_session_context 'tenant_id', NULL`.

### MySQL (`EricksonLopez.MultiTenancy.MySql`) & MariaDB (`EricksonLopez.MultiTenancy.MariaDb`)
- `connection.BeginTenantTransactionAsync(tenantContext)`: Executes `SET @app_tenant_id = @TenantId`.

### Oracle (`EricksonLopez.MultiTenancy.Oracle`)
- `connection.BeginTenantTransactionAsync(tenantContext)`: Executes `DBMS_SESSION.SET_IDENTIFIER(:tenantId)`.

### SQLite (`EricksonLopez.MultiTenancy.Sqlite`)
- `ISqliteTenantConnectionFactory`: Opens a per-tenant database file at `tenants/{tenantId}.db`.
- `connection.BeginTenantTransactionAsync(tenantContext)`: Sets temporary table context for single-database multi-tenancy.

---

## 10. EricksonLopez.MultiTenancy.Testing

Test harness primitives for unit and integration testing:

| Type | Description |
|:---|:---|
| `FakeTenantStore<TTenant>` | Pre-populated in-memory store with controllable failures |
| `FakeTenantResolutionStrategy` | Controllable strategy for mocking resolution outcomes |
| `TenantContextBuilder` | Fluent builder for constructing `ITenantContext` instances in tests |
| `TestTenantContext` | Lightweight `ITenantContext` mock for assertion |
| `FakeDbInfrastructure` | In-memory `DbConnection` + `DbTransaction` test doubles |
