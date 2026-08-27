# Public API Surface Inventory — EricksonLopez.MultiTenancy

> **Version**: 1.0.0  
> **Frameworks**: .NET 8.0, .NET 9.0, .NET 10.0  
> **AOT Policy**: Strictly Typed, Trimming-Friendly, Zero Dynamic Code  

---

## 1. Core Abstractions (`EricksonLopez.MultiTenancy.Abstractions`)

### Primary Types & Contracts
- `public readonly struct TenantId : IEquatable<TenantId>, IComparable<TenantId>`
  - `public static TenantId Empty { get; }`
  - `public static TenantId NewTenantId()`
  - `public static TenantId Create(Guid value)`
  - `public static bool TryCreate(string? value, out TenantId tenantId)`
  - `public Guid Value { get; }`
- `public interface ITenantInfo`
  - `TenantId Id { get; }`
  - `string Name { get; }`
  - `IReadOnlyDictionary<string, string> Properties { get; }`
- `public interface ITenantContext`
  - `TenantId Id { get; }`
  - `TenantInfo? Tenant { get; }`
  - `bool IsResolved { get; }`
  - `TenantInfo RequiredTenant { get; }`
- `public interface ITenantContextAccessor`
  - `ITenantContext? TenantContext { get; set; }`
- `public interface ITenantStore`
  - `ValueTask<Result<TenantInfo>> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)`
- `public interface ITenantLookupStore : ITenantStore`
  - `ValueTask<Result<TenantInfo>> GetTenantByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)`
- `public interface ITenantResolutionStrategy`
  - `string StrategyName { get; }`
  - `ValueTask<Result<TenantId>> ResolveTenantIdAsync(CancellationToken cancellationToken = default)`
- `public static class TenantErrors`
  - `public static Error NotFound(TenantId tenantId)`
  - `public static readonly Error Unresolved`
  - `public static Error Inactive(TenantId tenantId)`
  - `public static Error InvalidId(string? value)`
  - `public static Error StrategyFailed(string strategyName, string? reason = null)`

---

## 2. Core Engine (`EricksonLopez.MultiTenancy`)

### Implementations & Builders
- `public sealed class ScopedTenantContextAccessor : ITenantContextAccessor`
- `public sealed class InMemoryTenantStore : ITenantLookupStore`
- `public sealed class TenantContextBuilder`
  - `public TenantContextBuilder WithId(Guid id)`
  - `public TenantContextBuilder WithName(string name)`
  - `public TenantContextBuilder WithProperty(string key, string value)`
  - `public ITenantContext BuildContext()`
- `public static class MultiTenancyServiceCollectionExtensions`
  - `public static IServiceCollection AddMultiTenancy(this IServiceCollection services, Action<MultiTenancyBuilder>? configure = null)`

---

## 3. Web Presentation (`EricksonLopez.MultiTenancy.AspNetCore`)

### Strategies & Middleware
- `public sealed class TenantResolutionMiddleware`
- `public sealed class HeaderTenantResolutionStrategy : ITenantResolutionStrategy`
- `public sealed class ClaimTenantResolutionStrategy : ITenantResolutionStrategy`
- `public sealed class RouteTenantResolutionStrategy : ITenantResolutionStrategy`
- `public sealed class BasePathTenantResolutionStrategy : ITenantResolutionStrategy`
- `public sealed class HostNameTenantResolutionStrategy : ITenantResolutionStrategy`
- `public sealed class DelegateTenantResolutionStrategy : ITenantResolutionStrategy`
- `public sealed class StaticTenantResolutionStrategy : ITenantResolutionStrategy`
- `public static class AspNetCoreExtensions`
  - `public static IApplicationBuilder UseMultiTenancy(this IApplicationBuilder app)`
  - `public static IServiceCollection AddAspNetCoreMultiTenancy(this IServiceCollection services)`

---

## 4. Data Access & Dialects

### Dapper (`EricksonLopez.MultiTenancy.Dapper`)
- `public static class DapperMultiTenancyExtensions`
  - `public static DynamicParameters WithTenant(this DynamicParameters parameters, ITenantContext context)`
  - `public static DynamicParameters CreateTenantParameters(this ITenantContext context)`

### PostgreSQL (`EricksonLopez.MultiTenancy.PostgreSql`)
- `public static class PostgreSqlRlsExtensions`
  - `public static Task SetTenantRlsContextAsync(this DbConnection connection, DbTransaction transaction, ITenantContext tenantContext, string sessionVariable = DefaultTenantSessionVariable, CancellationToken cancellationToken = default)`
- `public sealed class PostgreSqlTenantStore : ITenantLookupStore`

### SQL Server (`EricksonLopez.MultiTenancy.SqlServer`)
- `public static class SqlServerSessionContextExtensions`
  - `public static Task SetTenantSessionContextAsync(this DbConnection connection, ITenantContext tenantContext, DbTransaction? transaction = null, string sessionKey = DefaultTenantSessionKey, bool readOnly = true, CancellationToken cancellationToken = default)`
