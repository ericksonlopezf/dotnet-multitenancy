# Official Public API Reference — EricksonLopez.MultiTenancy

> **Version**: 2.0.0  
> **Target Frameworks**: .NET 8.0, .NET 9.0  
> **Documentation Style**: Microsoft Learn Format  
> **Source of Truth**: Compiled and tested assemblies from `src/`  
> **Compatibility**: Native AOT, Trimming-Friendly, Zero Dynamic Code

---

## Table of Contents

1. [EricksonLopez.MultiTenancy.Abstractions](#1-ericksonlopezmultitenancyabstractions)
   - [TenantId (Readonly Record Struct)](#tenantid-readonly-record-struct)
   - [ITenantInfo & TenantInfo](#itenantinfo--tenantinfo)
   - [ITenantContext & ITenantContext\<TTenant\>](#itenantcontext--itenantcontextttenant)
   - [ITenantContextAccessor](#itenantcontextaccessor)
   - [ITenantStore & ITenantLookupStore](#itenantstore--itenantlookupstore)
   - [ITenantScopeFactory & ITenantScope](#itenantscopefactory--itenantscope)
   - [TenantErrors](#tenanterrors)
   - [Organizational Hierarchy (ICompanyContext, IBranchContext, IOrganizationContext)](#organizational-hierarchy)
   - [IPlatformAdminContext](#iplatformadmincontext)
2. [EricksonLopez.MultiTenancy (Core Engine)](#2-ericksonlopezmultitenancy-core-engine)
   - [TenantContext (Class)](#tenantcontext-class)
   - [ScopedTenantContextAccessor](#scopedtenantcontextaccessor)
   - [AmbientTenantContextHolder](#ambienttenantcontextholder)
   - [DefaultTenantScopeFactory](#defaulttenantscopefactory)
   - [InMemoryTenantStore](#inmemorytenantstore)
   - [CachedTenantStore & CachedTenantStoreExtensions](#cachedtenantstore)
   - [HttpRemoteTenantStore](#httpremotetenantstore)
   - [MultiTenancyHealthCheck](#multitenancyhealthcheck)
   - [ServiceCollectionExtensions](#servicecollectionextensions)
3. [EricksonLopez.MultiTenancy.AspNetCore](#3-ericksonlopezmultitenancyaspnetcore)
   - [TenantResolutionMiddleware & Options](#tenantresolutionmiddleware)
   - [HTTP Resolution Strategies](#http-resolution-strategies)
   - [RequireTenantFilter & Endpoint Extensions](#requiretenantfilter--endpoint-extensions)
   - [TenantOptionsCache & AddPerTenantOptions](#tenantoptionscache--addpertenantoptions)
   - [TenantRouteConstraint](#tenantrouteconstraint)
4. [EricksonLopez.MultiTenancy.Authentication](#4-ericksonlopezmultitenancyauthentication)
   - [TenantAuthenticationExtensions & TenantCookieAuthenticationEvents](#tenantauthenticationextensions)
5. [EricksonLopez.MultiTenancy.Configuration](#5-ericksonlopezmultitenancyconfiguration)
   - [ConfigurationTenantStore](#configurationtenantstore)
6. [EricksonLopez.MultiTenancy.Dapper](#6-ericksonlopezmultitenancydapper)
   - [TenantDapperExtensions & OrganizationDapperExtensions](#tenantdapperextensions)
7. [EricksonLopez.MultiTenancy.OpenTelemetry](#7-ericksonlopezmultitenancyopentelemetry)
   - [TenantActivitySource, TenantMetrics, ITenantTraceEnricher](#tenantactivitysource--tenantmetrics)
8. [Database Dialect Packages](#8-database-dialect-packages)
   - [PostgreSQL (PostgreSqlRlsExtensions, PostgreSqlTenantStore)](#postgresql-rls)
   - [SQL Server (SqlServerSessionContextExtensions)](#sql-server-session_context)
   - [MySQL & MariaDB (MySqlTenantExtensions, MariaDbTenantExtensions)](#mysql--mariadb)
   - [Oracle (OracleVpdExtensions)](#oracle-vpd)
   - [SQLite (SqliteTenantConnectionFactory, SqliteTenantExtensions)](#sqlite-database-per-tenant)
9. [EricksonLopez.MultiTenancy.Testing](#9-ericksonlopezmultitenancytesting)
   - [TenantContextBuilder & TestTenantContext](#tenantcontextbuilder--testtenantcontext)
   - [FakeTenantStore & FakeTenantResolutionStrategy](#faketenantstore)
   - [FakeDbConnection & ADO.NET Test Doubles](#fakedbconnection--adonet-test-doubles)

---

## 1. EricksonLopez.MultiTenancy.Abstractions

Namespace: `EricksonLopez.MultiTenancy`  
Assembly: `EricksonLopez.MultiTenancy.Abstractions.dll`

### `TenantId` (Readonly Record Struct)

Immutable, strongly-typed identifier backed internally by a 128-bit `System.Guid`.

```csharp
[System.Text.Json.Serialization.JsonConverter(typeof(Serialization.TenantIdJsonConverter))]
public readonly record struct TenantId : IEquatable<TenantId>, IComparable<TenantId>, IComparable, ISpanParsable<TenantId>
```

#### Properties
- `Guid Value { get; }`: Gets the underlying `Guid` value.
- `bool IsEmpty { get; }`: Returns `true` if `Value == Guid.Empty`.
- `static TenantId Empty { get; }`: Static sentinel equivalent to `new TenantId(Guid.Empty)`.

#### Factory & Parsing Methods
- `static TenantId NewId()`: Generates a new `TenantId` backed by `Guid.NewGuid()`.
- `static TenantId Create(Guid value)`: Creates a `TenantId`. Throws `ArgumentException` if `value == Guid.Empty`.
- `static TenantId Create(string value)`: Parses a GUID string. Throws `ArgumentException` if null, whitespace, or invalid.
- `static TenantId Create(ReadOnlySpan<char> value)`: Zero-allocation span parser.
- `static Result<TenantId> From(Guid value)`: Creates a `Result<TenantId>`. Returns `Failure` with `TenantErrors.InvalidId` if empty.
- `static Result<TenantId> From(string? value)`: Safe string factory returning a `Result<TenantId>`.
- `static Result<TenantId> From(ReadOnlySpan<char> value)`: Zero-allocation span factory.
- `static bool TryCreate(string? value, out TenantId tenantId)`: Safe non-throwing factory. Returns `false` if invalid.
- `static bool TryCreate(ReadOnlySpan<char> value, out TenantId tenantId)`: Zero-allocation span try-parse overload.
- `static bool TryCreate(Guid value, out TenantId tenantId)`: Returns `false` if `value == Guid.Empty`.
- `static TenantId Parse(string s, IFormatProvider? provider = null)`: `IParsable<TenantId>` implementation.
- `static bool TryParse(string? s, IFormatProvider? provider, out TenantId result)`: `IParsable<TenantId>` implementation.
- `static TenantId Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)`: `ISpanParsable<TenantId>` implementation.
- `static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out TenantId result)`: `ISpanParsable<TenantId>` implementation.

#### Operators
- `implicit operator Guid(TenantId tenantId)`: Implicit conversion to `Guid`.
- `explicit operator TenantId(Guid value)`: Explicit conversion from `Guid`.
- `<`, `<=`, `>`, `>=`: Comparison operators delegated to `Guid.CompareTo`.

#### When to Use
Use `TenantId` as the mandatory type for all foreign keys, primary keys, and parameter representations of tenant identity across application layers.

#### When NOT to Use
Do not use untyped strings or raw GUIDs (`string tenantId`, `Guid tenantId`) in public APIs, avoiding parameter collisions and assignment errors.

---

### `ITenantInfo` & `TenantInfo`

#### `ITenantInfo` (Interface)
Fundamental metadata contract for an active tenant.
```csharp
public interface ITenantInfo
{
    TenantId Id { get; }
    string Name { get; }
    string? ConnectionString { get; }
    bool IsActive { get; }
    IReadOnlyDictionary<string, string> Properties { get; }
}
```

#### `TenantInfo` (Record Class)
Standard immutable implementation of `ITenantInfo`.
```csharp
public record class TenantInfo : ITenantInfo
{
    public TenantId Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ConnectionString { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    public TenantInfo();
    public TenantInfo(TenantId id, string name, string? connectionString = null, bool isActive = true);
}
```

---

### `ITenantContext` & `ITenantContext<TTenant>`

#### `ITenantContext` (Interface)
Immutable contract for ambient tenant context resolved for the active scope.
```csharp
public interface ITenantContext
{
    ITenantInfo? Tenant { get; }
    [MemberNotNullWhen(true, nameof(Tenant))]
    bool IsResolved { get; }
    TenantResolutionSource Source { get; }
    ITenantInfo RequiredTenant { get; }
}
```

- **`RequiredTenant` Property**:
  - **Return**: Returns the guaranteed active `ITenantInfo`.
  - **Exceptions**:
    - `TenantNotFoundException`: Thrown if `Tenant` is null or `TenantId.Empty`.
    - `TenantInactiveException`: Thrown if tenant exists but `IsActive` is `false`.
  - **Remarks**: Designed for protected endpoints where unresolved tenant state constitutes an invariant violation.

#### `ITenantContext<TTenant>` (Interface)
```csharp
public interface ITenantContext<out TTenant> : ITenantContext
    where TTenant : class, ITenantInfo
{
    new TTenant? Tenant { get; }
}
```

---

### `ITenantContextAccessor` (Interface)

```csharp
public interface ITenantContextAccessor
{
    ITenantContext? TenantContext { get; set; }
}
```
- **Invariant**: Registered as `Scoped`. The setter must only be called **once** per scope lifecycle. Subsequent attempts throw `InvalidOperationException`.

---

### `ITenantStore` & `ITenantLookupStore`

```csharp
public interface ITenantStore
{
    Task<Result<ITenantInfo>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ITenantInfo> GetAllStreamAsync(CancellationToken cancellationToken = default);
}

public interface ITenantStore<TTenant> : ITenantStore where TTenant : class, ITenantInfo
{
    new Task<Result<TTenant>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    new IAsyncEnumerable<TTenant> GetAllStreamAsync(CancellationToken cancellationToken = default);
}

public interface ITenantLookupStore : ITenantStore
{
    Task<Result<ITenantInfo>> GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
}

public interface ITenantLookupStore<TTenant> : ITenantLookupStore, ITenantStore<TTenant> where TTenant : class, ITenantInfo
{
    new Task<Result<TTenant>> GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
}
```

---

### `ITenantScopeFactory` & `ITenantScope`

Execution scope factory for non-HTTP background workers and message queue consumers.

```csharp
public interface ITenantScopeFactory
{
    ITenantScope CreateScope(ITenantInfo tenant, TenantResolutionSource source = TenantResolutionSource.ExplicitScope);
}

public interface ITenantScope : IAsyncDisposable, IDisposable
{
    IServiceProvider ServiceProvider { get; }
    ITenantContext TenantContext { get; }
}
```

---

### `TenantErrors`

Standardized domain error catalog based on `EricksonLopez.Result.Error`.
- `static Error NotFound(TenantId tenantId)`: Code `Tenant.NotFound`.
- `static readonly Error Unresolved`: Code `Tenant.Unresolved`.
- `static Error Inactive(TenantId tenantId)`: Code `Tenant.Inactive`.
- `static Error InvalidId(string? value)`: Code `Tenant.InvalidId`.
- `static Error StrategyFailed(string strategyName, string? reason = null)`: Code `Tenant.ResolutionFailed`.

---

### Organizational Hierarchy

Native support for multi-tiered corporate models:
- `ICompanyContext`: Exposes `Guid? CompanyId` and `bool HasCompanyContext`.
- `IBranchContext`: Exposes `Guid? BranchId`, `IReadOnlyList<Guid> AllowedBranchIds`, and `bool AllBranchesAllowed`.
- `IOrganizationContext`: Combines `ITenantContext`, `ICompanyContext`, and `IBranchContext`.

---

### `IPlatformAdminContext`

Contract for privileged administrative and migration operations requiring explicit cross-tenant bypass (ADR-004).
```csharp
public interface IPlatformAdminContext
{
    bool IsPlatformAdmin { get; }
    string? AuditReason { get; }
}
```

---

## 2. EricksonLopez.MultiTenancy (Core Engine)

Namespace: `EricksonLopez.MultiTenancy`  
Assembly: `EricksonLopez.MultiTenancy.dll`

### `TenantContext` (Class)

```csharp
public class TenantContext : ITenantContext
{
    public static ITenantContext Empty { get; }
    public ITenantInfo? Tenant { get; }
    public bool IsResolved { get; }
    public TenantResolutionSource Source { get; }
    public ITenantInfo RequiredTenant { get; }

    public TenantContext();
    public TenantContext(ITenantInfo? tenant, TenantResolutionSource source = TenantResolutionSource.None);
}
```

### `ScopedTenantContextAccessor`

Scoped write-once accessor upholding isolation invariants and eliminating static global state.

### `AmbientTenantContextHolder`

Low-level container backed by `AsyncLocal<ITenantContext?>` for asynchronous contexts outside HTTP dependency injection lifecycles.

### `DefaultTenantScopeFactory`

Singleton implementation of `ITenantScopeFactory` creating `IServiceScope` instances with a `ScopedTenantContextAccessor` bound to the requested tenant.

### `InMemoryTenantStore`

```csharp
public class InMemoryTenantStore<TTenant> : ITenantStore<TTenant>, ITenantLookupStore<TTenant> where TTenant : class, ITenantInfo
{
    public InMemoryTenantStore();
    public InMemoryTenantStore(IEnumerable<TTenant> tenants);
    public void AddOrUpdate(TTenant tenant);
}
```

### `CachedTenantStore`

Namespace: `EricksonLopez.MultiTenancy.Stores`  
Decorator for `ITenantStore<TTenant>` and `ITenantLookupStore<TTenant>` backed by `Microsoft.Extensions.Caching.Memory.IMemoryCache`.
- Options: `CachedTenantStoreOptions` (`AbsoluteExpirationRelativeToNow`, `SlidingExpiration`).
- Extension method: `services.AddCachedTenantStore<TTenant>(Action<CachedTenantStoreOptions>? configure = null)`.

### `HttpRemoteTenantStore`

Namespace: `EricksonLopez.MultiTenancy.Stores`  
Resilient HTTP client for querying upstream tenant directory services.
- Options: `HttpRemoteTenantStoreOptions` (`BaseAddress`, `EndpointTemplate`, `IdentifierEndpointTemplate`, `Timeout`).
- Extension method: `services.AddHttpRemoteTenantStore<TTenant>(Action<HttpRemoteTenantStoreOptions>? configure = null)`.

### `MultiTenancyHealthCheck`

Namespace: `EricksonLopez.MultiTenancy.HealthChecks`  
`IHealthCheck` implementation verifying multi-tenancy subsystem readiness.
- Options: `MultiTenancyHealthCheckOptions` (`FailureStatus`, `IncludeDiagnosticData`, `StoreProbe`).
- Extension method: `services.AddMultiTenancyHealthCheck(Action<MultiTenancyHealthCheckOptions>? configure = null)`.

### `ServiceCollectionExtensions`

- `AddMultiTenancy(this IServiceCollection services)`: Registers base infrastructure (`ITenantContextAccessor` scoped, `ITenantContext` scoped, `ITenantScopeFactory` singleton).
- `AddMultiTenancy<TTenant>(this IServiceCollection services)`: Registers generic overload with typed `TTenant` model.
- `AddInMemoryTenantStore<TTenant>(this IServiceCollection services, Action<InMemoryTenantStore<TTenant>>? configure = null)`.

---

## 3. EricksonLopez.MultiTenancy.AspNetCore

Namespace: `EricksonLopez.MultiTenancy.AspNetCore`  
Assembly: `EricksonLopez.MultiTenancy.AspNetCore.dll`

### `TenantResolutionMiddleware`

Cascading tenant resolution middleware executed across configured strategies.
- **Fail-Closed Conflict Detection (ADR-008)**: Throws `TenantResolutionConflictException` if multiple strategies resolve conflicting non-null `TenantId` values.
- **Pipeline Registration**: `app.UseMultiTenancy()`.

### HTTP Resolution Strategies

Located under `EricksonLopez.MultiTenancy.AspNetCore.Strategies`:
- `ClaimTenantResolutionStrategy`: Resolves tenant from JWT claims (`tenant_id`, `tid`, `tenant`). Registered by default in `AddAspNetCoreMultiTenancy()`.
- `HostNameTenantResolutionStrategy`: Resolves tenant from Host header / subdomain. Registered with `AddHostNameTenantStrategy()`.
- `RouteTenantResolutionStrategy`: Resolves tenant from route values. Registered with `AddRouteTenantStrategy(routeParamName)`.
- `BasePathTenantResolutionStrategy`: Resolves tenant from URL leading path segment. Registered with `AddBasePathStrategy(segmentIndex)`.
- `InternalGatewayHeaderTenantResolutionStrategy`: Resolves tenant from internal gateway headers validated by shared secret token. Registered with `AddInternalHeaderTenantResolution(expectedSecret)`.
- `StaticTenantResolutionStrategy`: Returns a fixed tenant identifier for fallback or testing. Registered with `AddStaticTenantStrategy(tenantId)`.
- `DelegateTenantResolutionStrategy`: Custom resolver delegate `Func<CancellationToken, ValueTask<Result<TenantId>>>`. Registered with `AddDelegateTenantStrategy(resolver)`.

### `RequireTenantFilter` & Endpoint Extensions

- `app.MapGet(...).RequireTenant()` / `group.RequireTenant()`: Applies `RequireTenantFilter`, returning HTTP 400 ProblemDetails if `!IsResolved`.
- `app.MapGet(...).AllowAnonymousTenant()` / `group.AllowAnonymousTenant()`: Attaches metadata allowing anonymous access to the endpoint.

### `TenantOptionsCache` & `AddPerTenantOptions`

Namespace: `EricksonLopez.MultiTenancy.AspNetCore.Options`  
Provides isolated memory partitioning for `IOptionsMonitor<T>` and `IOptionsSnapshot<T>` per tenant.
- Method: `services.AddPerTenantOptions<TOptions, TTenant>(Action<TOptions, TTenant>? configure = null)`.

### `TenantRouteConstraint`

Namespace: `EricksonLopez.MultiTenancy.AspNetCore.Routing`  
Route constraint `IRouteConstraint` validating identifier syntax in URL paths. Registered via `services.AddTenantRouteConstraint()`.

---

## 4. EricksonLopez.MultiTenancy.Authentication

Namespace: `EricksonLopez.MultiTenancy.Authentication`  
Assembly: `EricksonLopez.MultiTenancy.Authentication.dll`

- `AddPerTenantAuthentication<TTenant>()`: Configures dynamic scheme provider for multi-tenant authentication.
- `TenantCookieAuthenticationEvents<TTenant>`: Cookie event handler validating in `ValidatePrincipal` that the cookie tenant matches the resolved request tenant, preventing cross-tenant session hijacking.

---

## 5. EricksonLopez.MultiTenancy.Configuration

Namespace: `EricksonLopez.MultiTenancy.Configuration`  
Assembly: `EricksonLopez.MultiTenancy.Configuration.dll`

- `ConfigurationTenantStore<TTenant>`: `ITenantLookupStore<TTenant>` implementation backed by `IConfiguration` sections.
- `AddConfigurationTenantStore<TTenant>(this IServiceCollection services, Action<MultiTenancyConfigurationOptions<TTenant>>? configure = null)`.

---

## 6. EricksonLopez.MultiTenancy.Dapper

Namespace: `EricksonLopez.MultiTenancy.Dapper`  
Assembly: `EricksonLopez.MultiTenancy.Dapper.dll`

### `TenantDapperExtensions`
- `WithTenant(this DynamicParameters parameters, ITenantContext tenantContext, string parameterName = "TenantId")`: Adds `@TenantId` parameter from resolved tenant context.
- `WithTenant(this DynamicParameters parameters, TenantId tenantId, string parameterName = "TenantId")`: Struct overload.
- `CreateTenantParameters(this ITenantContext tenantContext, string parameterName = "TenantId")`: Creates a new `DynamicParameters` with `@TenantId`.
- `CreateTenantParameters(TenantId tenantId, string parameterName = "TenantId")`: Direct struct overload.

### `OrganizationDapperExtensions`
- `WithOrganization(this DynamicParameters parameters, IOrganizationContext context)`: Adds `@TenantId`, `@CompanyId`, and `@BranchId`.
- `CreateOrganizationParameters(this IOrganizationContext context)`: Returns a new `DynamicParameters` initialized with composite hierarchy.

---

## 7. EricksonLopez.MultiTenancy.OpenTelemetry

Namespace: `EricksonLopez.MultiTenancy.OpenTelemetry`  
Assembly: `EricksonLopez.MultiTenancy.OpenTelemetry.dll`

### `TenantActivitySource`
- `const string ActivitySourceName = "EricksonLopez.MultiTenancy"`
- `static readonly ActivitySource Source`
- `Tags`: `tenant.id`, `tenant.name`, `tenant.source`, `tenant.is_active`, `tenant.resolution_strategy`, `tenant.error_code`, `tenant.error_message`, `tenant.conflict_id`.
- `Baggage`: `tenant.id`.

### `TenantMetrics`
- `const string MeterName = "EricksonLopez.MultiTenancy"`
- `RecordResolutionSuccess(string strategyName, TenantResolutionSource source, double durationMs = 0)`
- `RecordResolutionFailure(string strategyName, string errorCode, double durationMs = 0)`
- `RecordResolutionConflict(string firstStrategy, string secondStrategy)`

### `TenantActivityExtensions`
- `EnrichWithTenant(this Activity? activity, ITenantContext? tenantContext)`
- `EnrichCurrentActivity(ITenantContext? tenantContext)`
- `SetTenantBaggage(this Activity? activity, TenantId tenantId)`
- `RecordTenantResolutionFailure(this Activity? activity, Error error, string? strategyName = null)`

### `ITenantTraceEnricher` & `TenantTraceEnricher`
Injectable DI service for manual span enrichment. Registered with `services.AddMultiTenancyOpenTelemetry()`.

---

## 8. Database Dialect Packages

### PostgreSQL (`EricksonLopez.MultiTenancy.PostgreSql`)

- `PostgreSqlRlsExtensions.SetTenantRlsContextAsync(this DbConnection connection, DbTransaction transaction, ITenantContext tenantContext, string sessionVariable = DefaultTenantSessionVariable, CancellationToken cancellationToken = default)`
- `PostgreSqlRlsExtensions.BeginTenantTransactionAsync(this DbConnection connection, ITenantContext tenantContext, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, string sessionVariable = DefaultTenantSessionVariable, CancellationToken cancellationToken = default)`
- `PostgreSqlRlsDiagnostics.GetCurrentTenantSettingAsync(this DbConnection connection, DbTransaction? transaction = null, string sessionVariable = DefaultTenantSessionVariable, CancellationToken cancellationToken = default)`
- `PostgreSqlOrganizationRlsExtensions.SetOrganizationRlsContextAsync(this DbConnection connection, DbTransaction transaction, IOrganizationContext orgContext, OrganizationRlsOptions? options = null, CancellationToken cancellationToken = default)`
- `PostgreSqlTenantStore` / `PostgreSqlTenantStore<TTenant>`: Persistent relational tenant store querying PostgreSQL tables. Registered with `services.AddPostgreSqlTenantStore()`.

### SQL Server (`EricksonLopez.MultiTenancy.SqlServer`)

- `SqlServerSessionContextExtensions.SetTenantSessionContextAsync(this DbConnection connection, ITenantContext tenantContext, DbTransaction? transaction = null, string sessionKey = DefaultTenantSessionKey, bool readOnly = true, CancellationToken cancellationToken = default)`
- `SqlServerSessionContextExtensions.BeginTenantTransactionAsync(this DbConnection connection, ITenantContext tenantContext, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, string sessionKey = DefaultTenantSessionKey, bool readOnly = true, CancellationToken cancellationToken = default)`
- `SqlServerSessionContextExtensions.ResetTenantSessionContextAsync(this DbConnection connection, DbTransaction? transaction = null, string sessionKey = DefaultTenantSessionKey, CancellationToken cancellationToken = default)`

### MySQL (`EricksonLopez.MultiTenancy.MySql`) & MariaDB (`EricksonLopez.MultiTenancy.MariaDb`)

- `BeginTenantTransactionAsync(this DbConnection connection, ITenantContext tenantContext, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, string variableName = DefaultTenantVariableName, CancellationToken cancellationToken = default)`
- `ResetTenantSessionVariableAsync(this DbConnection connection, DbTransaction? transaction = null, string variableName = DefaultTenantVariableName, CancellationToken cancellationToken = default)`

### Oracle (`EricksonLopez.MultiTenancy.Oracle`)

- `OracleVpdExtensions.BeginTenantTransactionAsync(this DbConnection connection, ITenantContext tenantContext, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, bool setClientIdProperty = true, CancellationToken cancellationToken = default)`
- `OracleVpdExtensions.ResetTenantVpdContextAsync(this DbConnection connection, DbTransaction? transaction = null, CancellationToken cancellationToken = default)`

### SQLite (`EricksonLopez.MultiTenancy.Sqlite`)

- `ISqliteTenantConnectionFactory`: Connection factory contract for routing per-tenant SQLite database files.
- `SqliteTenantConnectionFactory`: Factory implementation with token replacement (`{TenantId}`, `{Name}`) and automated directory provisioning.
- `SqliteTenantExtensions.BeginTenantTransactionAsync`: Local testing isolation using temporary tables.

---

## 9. EricksonLopez.MultiTenancy.Testing

Namespace: `EricksonLopez.MultiTenancy.Testing`  
Assembly: `EricksonLopez.MultiTenancy.Testing.dll`

### `TenantContextBuilder`
Fluent builder for instantiating immutable test contexts:
- `WithId(Guid id)` / `WithId(TenantId id)`
- `WithName(string name)`
- `WithActive(bool isActive)`
- `WithProperty(string key, string value)`
- `WithSource(TenantResolutionSource source)`
- `ITenantContext BuildContext()`

### `TestTenantContext`
Mutable test double with `Reset()` method and convenience factory `TestTenantContext.Create(tenantId, tenantName, isActive)`.

### ADO.NET Test Doubles
- `FakeDbConnection`: Captures executed commands in `Commands` (`List<FakeDbCommand>`).
- `FakeDbCommand`: Captures SQL command text, bound parameters, and associated transaction.
- `FakeDbParameter` & `FakeDbParameterCollection`: Complete ADO.NET parameter emulation.
- `FakeDbTransaction`: Records commit and rollback calls.
- `FakeDbDataReader`: Configurable mock data cursor.
- `FakeTenantStore` / `FakeTenantStore<TTenant>`: In-memory store test double with fault simulation support.
- `FakeTenantResolutionStrategy`: Configurable strategy simulating successful or conflicting resolution results in integration tests.
