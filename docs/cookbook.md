# EricksonLopez.MultiTenancy Cookbook

A comprehensive collection of production-ready, tested, and compilable engineering recipes for the `EricksonLopez.MultiTenancy` suite. Each recipe solves a concrete architectural need utilizing exclusively APIs from the official inventory.

---

## Table of Contents

1. [Recipe 1: Minimal Multi-Tenant Web API Configuration](#recipe-1-minimal-multi-tenant-web-api-configuration)
2. [Recipe 2: Subdomain / HostName Tenant Resolution](#recipe-2-subdomain--hostname-tenant-resolution)
3. [Recipe 3: URL Route Segment Tenant Resolution](#recipe-3-url-route-segment-tenant-resolution)
4. [Recipe 4: Base Path Prefix (BasePath) Tenant Resolution](#recipe-4-base-path-prefix-basepath-tenant-resolution)
5. [Recipe 5: Authenticated User JWT Claims Tenant Resolution](#recipe-5-authenticated-user-jwt-claims-tenant-resolution)
6. [Recipe 6: Internal Gateway Header Resolution with Shared Secret](#recipe-6-internal-gateway-header-resolution-with-shared-secret)
7. [Recipe 7: Safe SQL Parameterization with Dapper and Leak Prevention](#recipe-7-safe-sql-parameterization-with-dapper-and-leak-prevention)
8. [Recipe 8: Transactional Isolation with PostgreSQL RLS (`SET LOCAL`)](#recipe-8-transactional-isolation-with-postgresql-rls-set-local)
9. [Recipe 9: Isolation with Microsoft SQL Server (`SESSION_CONTEXT`)](#recipe-9-isolation-with-microsoft-sql-server-session_context)
10. [Recipe 10: Isolation with MySQL and MariaDB using Session Variables](#recipe-10-isolation-with-mysql-and-mariadb-using-session-variables)
11. [Recipe 11: Isolation with Oracle Virtual Private Database (VPD)](#recipe-11-isolation-with-oracle-virtual-private-database-vpd)
12. [Recipe 12: Database-per-Tenant Pattern with SQLite](#recipe-12-database-per-tenant-pattern-with-sqlite)
13. [Recipe 13: Isolated Background Job Execution with `ITenantScopeFactory`](#recipe-13-isolated-background-job-execution-with-itenantscopefactory)
14. [Recipe 14: Per-Tenant Segmented Options Pattern (`AddPerTenantOptions`)](#recipe-14-per-tenant-segmented-options-pattern-addpertenantoptions)
15. [Recipe 15: In-Memory Tenant Metadata Cache (`AddCachedTenantStore`)](#recipe-15-in-memory-tenant-metadata-cache-addcachedtenantstore)
16. [Recipe 16: Distributed Tracing and Metrics with OpenTelemetry](#recipe-16-distributed-tracing-and-metrics-with-opentelemetry)
17. [Recipe 17: Multi-Tenancy Subsystem Health Verification](#recipe-17-multi-tenancy-subsystem-health-verification)
18. [Recipe 18: Unit Testing Harness with Test Doubles (`TenantContextBuilder`)](#recipe-18-unit-testing-harness-with-test-doubles-tenantcontextbuilder)
19. [Recipe 19: Fail-Closed Resolution Conflict Detection (ADR-008)](#recipe-19-fail-closed-resolution-conflict-detection-adr-008)
20. [Recipe 20: Complete Corporate Hierarchy (Tenant + Company + Branch)](#recipe-20-complete-corporate-hierarchy-tenant--company--branch)

---

## Recipe 1: Minimal Multi-Tenant Web API Configuration

### Problem
You need to configure an ASP.NET Core API that resolves the tenant for every incoming HTTP request, securely injects it into endpoint handlers and controllers, and rejects unauthenticated or unresolved requests.

### Solution
Use `AddMultiTenancy()`, `AddAspNetCoreMultiTenancy()`, `AddInMemoryTenantStore()`, `app.UseMultiTenancy()`, and the `.RequireTenant()` endpoint filter.

### Complete Code
```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Service registration
builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();
builder.Services.AddInMemoryTenantStore<TenantInfo>(store =>
{
    var acmeId = TenantId.Create("11111111-1111-1111-1111-111111111111");
    store.AddOrUpdate(new TenantInfo(acmeId, "acme-corp", isActive: true));
});

var app = builder.Build();

// 2. Middleware pipeline activation
app.UseMultiTenancy();

// 3. Protected endpoint requiring resolved tenant
app.MapGet("/api/tenant-profile", (ITenantContext tenantContext) =>
{
    var tenant = tenantContext.RequiredTenant;
    return Results.Ok(new
    {
        tenant.Id,
        tenant.Name,
        tenant.IsActive,
        ResolutionSource = tenantContext.Source.ToString()
    });
}).RequireTenant();

app.Run();
```

### Explanation
- `AddMultiTenancy()` registers `ITenantContextAccessor` (Scoped), `ITenantContext` (Scoped), and `ITenantScopeFactory` (Singleton).
- `AddAspNetCoreMultiTenancy()` registers default HTTP resolution strategies and resolution middleware.
- `.RequireTenant()` applies `RequireTenantFilter`, returning an RFC 7807 HTTP 400 ProblemDetails response if the tenant is unresolved or inactive.

### Best Practices
- Always place `app.UseMultiTenancy()` after routing/authentication and before endpoint execution.
- Protect sensitive endpoints with `.RequireTenant()` instead of duplicating manual `if (!context.IsResolved)` checks.

### Common Pitfalls
- Invoking `tenantContext.RequiredTenant` on unprotected endpoints without checking `tenantContext.IsResolved`, which throws `TenantNotFoundException`.

---

## Recipe 2: Subdomain / HostName Tenant Resolution

### Problem
Clients access the application via dedicated subdomains (e.g., `acme.app.example.com`), and the system must extract the subdomain and map it to the corresponding `TenantId`.

### Solution
Register `AddHostNameTenantStrategy()` and configure an `ITenantLookupStore` supporting identifier lookups.

### Complete Code
```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

// Enable HostName / Subdomain resolution strategy
builder.Services.AddHostNameTenantStrategy();

builder.Services.AddInMemoryTenantStore<TenantInfo>(store =>
{
    store.AddOrUpdate(new TenantInfo(
        TenantId.Create("11111111-1111-1111-1111-111111111111"),
        "acme", // Matches subdomain acme.app.example.com
        isActive: true));
});

var app = builder.Build();
app.UseMultiTenancy();
app.MapGet("/api/ping", (ITenantContext ctx) => Results.Ok(new { Tenant = ctx.RequiredTenant.Name })).RequireTenant();
app.Run();
```

### Explanation
`HostNameTenantResolutionStrategy` parses the first label from `HttpContext.Request.Host` and calls `ITenantLookupStore.GetTenantByIdentifierAsync()` to resolve the tenant record.

### Best Practices
- Configure reverse proxies (e.g., NGINX, Cloudflare, Azure Front Door) to pass the original host via `X-Forwarded-Host`.

### Common Pitfalls
- Forgetting to register a store implementing `ITenantLookupStore`, which prevents string identifier lookups.

---

## Recipe 3: URL Route Segment Tenant Resolution

### Problem
The API exposes route-partitioned URLs such as `/api/tenants/{tenantId}/invoices` and needs to extract and validate the tenant identifier automatically.

### Solution
Register `AddRouteTenantStrategy("tenantId")` and use `TenantRouteConstraint` for route matching.

### Complete Code
```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();
builder.Services.AddRouteTenantStrategy("tenantId");

builder.Services.AddInMemoryTenantStore<TenantInfo>(store =>
{
    var id = TenantId.Create("11111111-1111-1111-1111-111111111111");
    store.AddOrUpdate(new TenantInfo(id, "acme", isActive: true));
});

var app = builder.Build();
app.UseMultiTenancy();

app.MapGet("/api/tenants/{tenantId}/invoices", (ITenantContext context) =>
{
    return Results.Ok(new { Message = $"Invoices for {context.RequiredTenant.Name}" });
}).RequireTenant();

app.Run();
```

### Explanation
`RouteTenantResolutionStrategy` extracts the parameter value from `HttpContext.GetRouteData().Values` and parses it into a `TenantId`.

### Best Practices
- Use a uniform route parameter name across the entire application (e.g., `"tenantId"`).

### Common Pitfalls
- Placing `app.UseMultiTenancy()` before `app.UseRouting()`, preventing route values from being populated before middleware execution.

---

## Recipe 4: Base Path Prefix (BasePath) Tenant Resolution

### Problem
Client applications issue requests where the first URL path segment represents the tenant slug (e.g., `/acme-corp/api/v1/orders`).

### Solution
Use `AddBasePathStrategy(segmentIndex: 0)`.

### Complete Code
```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

// Segment 0: /{tenant}/api/...
builder.Services.AddBasePathStrategy(segmentIndex: 0);

builder.Services.AddInMemoryTenantStore<TenantInfo>(store =>
{
    store.AddOrUpdate(new TenantInfo(
        TenantId.Create("11111111-1111-1111-1111-111111111111"),
        "acme-corp",
        isActive: true));
});

var app = builder.Build();
app.UseMultiTenancy();

app.MapGet("/{tenant}/api/v1/orders", (ITenantContext context) =>
{
    return Results.Ok(new { Tenant = context.RequiredTenant.Name });
}).RequireTenant();

app.Run();
```

### Explanation
`BasePathTenantResolutionStrategy` splits `Request.Path` by forward slashes, takes the token at `segmentIndex`, and looks it up via `ITenantLookupStore`.

### Best Practices
- Ensure static routes (e.g., `/swagger`, `/health`, `/favicon.ico`) do not collide with tenant slugs.

---

## Recipe 5: Authenticated User JWT Claims Tenant Resolution

### Problem
The architecture uses OAuth2/OIDC JWT tokens where the tenant identity is securely embedded within a standard claim (`tenant_id`).

### Solution
Configure standard JWT bearer authentication and call `AddAspNetCoreMultiTenancy()`, which registers `ClaimTenantResolutionStrategy` with highest evaluation precedence.

### Complete Code
```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // JWT signature validation configuration
    });

builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

var app = builder.Build();

app.UseAuthentication(); // 1. Extract ClaimsPrincipal
app.UseMultiTenancy();   // 2. Extract TenantId from Claims (Priority 1)
app.UseAuthorization();  // 3. Authorize request

app.MapGet("/api/me", (ITenantContext context) =>
{
    return Results.Ok(new { Tenant = context.RequiredTenant.Name });
}).RequireTenant();

app.Run();
```

### Explanation
When authenticated, `ClaimTenantResolutionStrategy` inspects claims matching `tenant_id`, `tid`, or `http://schemas.microsoft.com/identity/claims/tenantid`. Because this originates from a cryptographically signed token, it cannot be overridden by unauthenticated headers or routes (ADR-003).

### Best Practices
- Always place `app.UseAuthentication()` strictly before `app.UseMultiTenancy()`.

---

## Recipe 6: Internal Gateway Header Resolution with Shared Secret

### Problem
An upstream API Gateway terminates external TLS, authenticates callers, and forwards requests with an internal `X-Tenant-ID` header. You must ensure external clients cannot spoof this header.

### Solution
Use `AddInternalHeaderTenantResolution(gatewaySecret)` to validate a shared secret signature between the gateway and internal services.

### Complete Code
```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

// Require shared gateway secret token to trust internal header
builder.Services.AddInternalHeaderTenantResolution("prod-gateway-shared-secret-key-12345");

var app = builder.Build();
app.UseMultiTenancy();
app.MapGet("/api/data", (ITenantContext ctx) => Results.Ok(new { Tenant = ctx.RequiredTenant.Name })).RequireTenant();
app.Run();
```

### Explanation
`InternalGatewayHeaderTenantResolutionStrategy` verifies that the request includes both the tenant header and the gateway authentication secret (`X-Gateway-Secret`). If the secret is missing or incorrect, the strategy discards the header.

### Best Practices
- Rotate shared secrets regularly using Azure Key Vault, AWS Secrets Manager, or Kubernetes Secrets.

---

## Recipe 7: Safe SQL Parameterization with Dapper and Leak Prevention

### Problem
You execute raw SQL queries with Dapper on a shared multi-tenant relational database and must guarantee that every statement includes `@TenantId` with correct data typing.

### Solution
Use the `WithTenant()` and `CreateTenantParameters()` extension methods from `EricksonLopez.MultiTenancy.Dapper`.

### Complete Code
```csharp
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;

public record Invoice
{
    public Guid Id { get; init; }
    public TenantId TenantId { get; init; }
    public decimal Amount { get; init; }
}

public class InvoiceRepository
{
    private readonly DbConnection _connection;
    private readonly ITenantContext _tenantContext;

    public InvoiceRepository(DbConnection connection, ITenantContext tenantContext)
    {
        _connection = connection;
        _tenantContext = tenantContext;
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Creates DynamicParameters with @TenantId automatically typed as Guid
        var parameters = _tenantContext.CreateTenantParameters();
        parameters.Add("Id", id, DbType.Guid);

        const string sql = "SELECT id, tenant_id AS TenantId, amount FROM invoices WHERE id = @Id AND tenant_id = @TenantId";
        var command = new CommandDefinition(sql, parameters, cancellationToken: ct);
        return await _connection.QuerySingleOrDefaultAsync<Invoice>(command);
    }
}
```

### Explanation
`CreateTenantParameters()` returns an initialized `DynamicParameters` instance containing `@TenantId` populated with `_tenantContext.RequiredTenant.Id.Value` and typed as `DbType.Guid`.

### Best Practices
- Never concatenate raw strings into SQL statements (`$"WHERE tenant_id = '{id}'"`). Always use parameterized queries to prevent SQL injection and maximize query plan reuse.

---

## Recipe 8: Transactional Isolation with PostgreSQL RLS (`SET LOCAL`)

### Problem
In PostgreSQL under connection pooling, tables have Row-Level Security enabled (`FORCE ROW LEVEL SECURITY`). You must inject the `app.current_tenant_id` session variable without leaking context across pooled connections.

### Solution
Use `PostgreSqlRlsExtensions.BeginTenantTransactionAsync()` to open a transaction and execute `SET LOCAL` atomically.

### Complete Code
```csharp
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.PostgreSql;

public class PostgreSqlOrderService
{
    public static async Task ExecuteOrderWorkflowAsync(
        DbConnection connection,
        ITenantContext tenantContext,
        CancellationToken ct = default)
    {
        // 1. Begin transaction and execute: SET LOCAL app.current_tenant_id = '<guid>'
        await using var transaction = await connection.BeginTenantTransactionAsync(
            tenantContext,
            IsolationLevel.ReadCommitted,
            PostgreSqlRlsExtensions.DefaultTenantSessionVariable,
            ct);

        // 2. Execute queries automatically constrained by kernel RLS
        var command = new CommandDefinition("SELECT * FROM invoices", transaction: transaction, cancellationToken: ct);
        _ = await connection.QueryAsync(command);

        // 3. Commit transaction
        await transaction.CommitAsync(ct);
        // On commit or rollback, PostgreSQL automatically purges the SET LOCAL session setting.
    }
}
```

### Explanation
`SET LOCAL` guarantees that the variable exists strictly for the duration of the transaction. Once the transaction completes, the setting disappears, preventing pool contamination (ADR-009).

### Best Practices
- Configure PostgreSQL tables with `ALTER TABLE invoices FORCE ROW LEVEL SECURITY` to ensure policies apply even when connecting as the table owner.

---

## Recipe 9: Isolation with Microsoft SQL Server (`SESSION_CONTEXT`)

### Problem
You are targeting Microsoft SQL Server or Azure SQL Database with security policies based on `SESSION_CONTEXT(N'TenantId')`.

### Solution
Use `SqlServerSessionContextExtensions.BeginTenantTransactionAsync()` and `ResetTenantSessionContextAsync()`.

### Complete Code
```csharp
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.SqlServer;

public class SqlServerService
{
    public static async Task RunScopedWorkAsync(
        DbConnection connection,
        ITenantContext tenantContext,
        CancellationToken ct = default)
    {
        // Begin transaction and execute: EXEC sp_set_session_context @key=N'TenantId', @value='...', @read_only=0
        await using var transaction = await connection.BeginTenantTransactionAsync(
            tenantContext,
            IsolationLevel.ReadCommitted,
            SqlServerSessionContextExtensions.DefaultTenantSessionKey,
            readOnly: false,
            cancellationToken: ct);

        var command = new CommandDefinition("SELECT * FROM invoices", transaction: transaction, cancellationToken: ct);
        _ = await connection.QueryAsync(command);

        await transaction.CommitAsync(ct);

        // Clean up session context before returning physical connection to pool
        await connection.ResetTenantSessionContextAsync(
            sessionKey: SqlServerSessionContextExtensions.DefaultTenantSessionKey,
            cancellationToken: ct);
    }
}
```

### Explanation
`sp_set_session_context` binds a key-value pair to the active session. SQL Server RLS predicate functions inspect this variable via `SESSION_CONTEXT(N'TenantId')`.

---

## Recipe 10: Isolation with MySQL and MariaDB using Session Variables

### Problem
You need session variable isolation (`@tenant_id`) in shared MySQL or MariaDB databases.

### Solution
Use `MySqlTenantExtensions` or `MariaDbTenantExtensions`.

### Complete Code
```csharp
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.MySql;

public class MySqlService
{
    public static async Task ExecuteAsync(DbConnection connection, ITenantContext tenantContext, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTenantTransactionAsync(
            tenantContext,
            IsolationLevel.ReadCommitted,
            MySqlTenantExtensions.DefaultTenantVariableName,
            ct);

        var command = new CommandDefinition("SELECT * FROM invoices", transaction: transaction, cancellationToken: ct);
        _ = await connection.QueryAsync(command);

        await transaction.CommitAsync(ct);

        await connection.ResetTenantSessionVariableAsync(
            variableName: MySqlTenantExtensions.DefaultTenantVariableName,
            cancellationToken: ct);
    }
}
```

---

## Recipe 11: Isolation with Oracle Virtual Private Database (VPD)

### Problem
You must integrate with Oracle Virtual Private Database (VPD) using `DBMS_SESSION.SET_IDENTIFIER` to enforce tenant security policies.

### Solution
Use `OracleVpdExtensions.BeginTenantTransactionAsync()`.

### Complete Code
```csharp
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Oracle;

public class OracleService
{
    public static async Task ExecuteAsync(DbConnection connection, ITenantContext tenantContext, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTenantTransactionAsync(
            tenantContext,
            IsolationLevel.ReadCommitted,
            setClientIdProperty: true,
            cancellationToken: ct);

        var command = new CommandDefinition("SELECT * FROM invoices", transaction: transaction, cancellationToken: ct);
        _ = await connection.QueryAsync(command);

        await transaction.CommitAsync(ct);

        await connection.ResetTenantVpdContextAsync(cancellationToken: ct);
    }
}
```

---

## Recipe 12: Database-per-Tenant Pattern with SQLite

### Problem
Each tenant requires an isolated physical SQLite database file (e.g., `data/tenants/{TenantId}_{Name}.db`).

### Solution
Register and inject `ISqliteTenantConnectionFactory` / `SqliteTenantConnectionFactory`.

### Complete Code
```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Sqlite;
using Microsoft.Extensions.DependencyInjection;

public class DatabasePerTenantDemo
{
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton<ISqliteTenantConnectionFactory>(_ =>
            new SqliteTenantConnectionFactory("Data Source=tenants/{TenantId}_{Name}.db;"));
    }

    public static void OpenTenantDatabase(ISqliteTenantConnectionFactory factory, ITenantInfo tenant)
    {
        // Generates connection string with token replacement and automatically creates parent directories
        string connStr = factory.BuildConnectionString(tenant);
        using var connection = factory.CreateConnection(tenant);
        connection.Open();
    }
}
```

---

## Recipe 13: Isolated Background Job Execution with `ITenantScopeFactory`

### Problem
A background task (`IHostedService`, Hangfire job, or message queue consumer) processes work across different tenants asynchronously outside the HTTP request lifecycle.

### Solution
Inject `ITenantScopeFactory` and `ITenantStore`, wrapping execution within `await using var scope = scopeFactory.CreateScope(tenant, TenantResolutionSource.BackgroundJob)`.

### Complete Code
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;

public class BackgroundInvoiceWorker
{
    private readonly ITenantStore _store;
    private readonly ITenantScopeFactory _scopeFactory;

    public BackgroundInvoiceWorker(ITenantStore store, ITenantScopeFactory scopeFactory)
    {
        _store = store;
        _scopeFactory = scopeFactory;
    }

    public async Task ProcessInvoicesAsync(TenantId targetTenantId, CancellationToken ct)
    {
        var storeResult = await _store.GetTenantAsync(targetTenantId, ct);
        if (!storeResult.IsSuccess || !storeResult.Value.IsActive)
        {
            return;
        }

        var tenant = storeResult.Value;

        // Create isolated scope with tenant context pre-bound
        await using var scope = _scopeFactory.CreateScope(tenant, TenantResolutionSource.BackgroundJob);

        // Services resolved from scope.ServiceProvider receive the scoped ITenantContext
        var repository = scope.ServiceProvider.GetRequiredService<InvoiceRepository>();
        await repository.GetByIdAsync(Guid.NewGuid(), ct);
    }
}
```

---

## Recipe 14: Per-Tenant Segmented Options Pattern (`AddPerTenantOptions`)

### Problem
Different tenants require unique configuration settings (e.g., currency, rate limits, visual branding) accessed via `IOptionsSnapshot<T>` or `IOptionsMonitor<T>`.

### Solution
Use `AddPerTenantOptions<TOptions, TTenant>()`.

### Complete Code
```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public class CompanyBillingOptions
{
    public string Currency { get; set; } = "USD";
    public int MaxUsers { get; set; } = 50;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

// Configure options segmented per tenant
builder.Services.AddPerTenantOptions<CompanyBillingOptions, TenantInfo>((options, tenant) =>
{
    if (tenant.Properties.TryGetValue("Currency", out var curr))
    {
        options.Currency = curr;
    }
});

var app = builder.Build();
app.UseMultiTenancy();

app.MapGet("/api/billing-options", (IOptionsMonitor<CompanyBillingOptions> options) =>
{
    return Results.Ok(options.CurrentValue);
}).RequireTenant();

app.Run();
```

---

## Recipe 15: In-Memory Tenant Metadata Cache (`AddCachedTenantStore`)

### Problem
The tenant directory resides in PostgreSQL or a remote API, and issuing catalog queries on every HTTP request creates excessive database overhead.

### Solution
Decorate the registered store with `AddCachedTenantStore<TenantInfo>()`.

### Complete Code
```csharp
using System;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Stores;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMultiTenancy();
builder.Services.AddMemoryCache();

// 1. Base backing store (PostgreSQL or remote HTTP)
builder.Services.AddInMemoryTenantStore<TenantInfo>();

// 2. Cache decorator
builder.Services.AddCachedTenantStore<TenantInfo>(options =>
{
    options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
    options.SlidingExpiration = TimeSpan.FromMinutes(5);
});
```

---

## Recipe 16: Distributed Tracing and Metrics with OpenTelemetry

### Problem
You need to correlate distributed logs, traces, and metrics by automatically tagging the `TenantId` onto OpenTelemetry spans and propagating it to downstream services via W3C Baggage.

### Solution
Call `AddMultiTenancyOpenTelemetry()` and configure `TenantActivitySource` and `TenantMetrics`.

### Complete Code
```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMultiTenancy();
builder.Services.AddMultiTenancyOpenTelemetry();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(TenantActivitySource.ActivitySourceName)
        .AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter(TenantMetrics.MeterName));
```

---

## Recipe 17: Multi-Tenancy Subsystem Health Verification

### Problem
Container orchestrators such as Kubernetes need to probe the health and connectivity of the tenant catalog store via `/health`.

### Solution
Configure `AddMultiTenancyHealthCheck()` with a custom `StoreProbe`.

### Complete Code
```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMultiTenancy();
builder.Services.AddMultiTenancyHealthCheck(options =>
{
    options.FailureStatus = HealthStatus.Degraded;
    options.IncludeDiagnosticData = true;
    options.StoreProbe = async (store, ct) =>
    {
        var result = await store.GetTenantAsync(TenantId.Create("11111111-1111-1111-1111-111111111111"), ct);
        return result.IsSuccess;
    };
});
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");
app.Run();
```

---

## Recipe 18: Unit Testing Harness with Test Doubles (`TenantContextBuilder`)

### Problem
Write high-speed unit tests for domain services and repositories without spinning up real databases or Docker containers.

### Solution
Use `TenantContextBuilder` and `FakeDbConnection` from `EricksonLopez.MultiTenancy.Testing`.

### Complete Code
```csharp
using System;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Testing;
using Xunit;

public class InvoiceServiceTests
{
    [Fact]
    public async Task GetInvoice_WithValidTenant_ReturnsData()
    {
        // 1. Arrange: Build immutable test context
        var tenantId = TenantId.NewId();
        var context = new TenantContextBuilder()
            .WithId(tenantId)
            .WithName("acme-test")
            .WithActive(true)
            .BuildContext();

        using var fakeConn = new FakeDbConnection();
        var repo = new InvoiceRepository(fakeConn, context);

        // 2. Act
        var result = await repo.GetByIdAsync(Guid.NewGuid());

        // 3. Assert: Verify SQL query was executed against fake connection
        Assert.Single(fakeConn.Commands);
    }
}
```

---

## Recipe 19: Fail-Closed Resolution Conflict Detection (ADR-008)

### Problem
Detect and block tenant spoofing attacks where a client transmits conflicting tenant identifiers across headers, tokens, or routes.

### Solution
The suite enforces the Fail-Closed principle by default (ADR-008). When conflicting non-empty identifiers are detected across strategies, the middleware immediately aborts with `TenantResolutionConflictException`.

### Complete Code
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;
using EricksonLopez.MultiTenancy.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

public static class ConflictDetectionDemo
{
    public static async Task RunAsync()
    {
        var store = new FakeTenantStore();
        var accessor = new ScopedTenantContextAccessor();

        // Strategy 1 resolves Tenant A (e.g., from JWT Claim)
        var strat1 = new FakeTenantResolutionStrategy(TenantId.NewId(), "JwtClaim");
        // Strategy 2 resolves Tenant B (e.g., from spoofed Header)
        var strat2 = new FakeTenantResolutionStrategy(TenantId.NewId(), "Header");

        var middleware = new TenantResolutionMiddleware(
            _ => Task.CompletedTask,
            NullLogger<TenantResolutionMiddleware>.Instance);

        var httpContext = new DefaultHttpContext();
        var strategies = new List<ITenantResolutionStrategy> { strat1, strat2 };

        try
        {
            await middleware.InvokeAsync(httpContext, strategies, store, accessor);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Spoofing attack blocked: {ex.Message}");
        }
    }
}
```

---

## Recipe 20: Complete Corporate Hierarchy (Tenant + Company + Branch)

### Problem
Enterprise ERP or CRM systems require 3-tier organizational partitioning: Tenant (SaaS customer), Company (legal entity), and Branch (physical office).

### Solution
Implement `IOrganizationContext` and parameterize Dapper queries with `WithOrganization()`.

### Complete Code
```csharp
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;

public class CustomOrganizationContext : IOrganizationContext
{
    public ITenantInfo? Tenant { get; init; }
    public bool IsResolved => Tenant is not null && Tenant.IsActive;
    public TenantResolutionSource Source => TenantResolutionSource.ExplicitScope;

    public Guid? CompanyId { get; init; }
    public Guid? BranchId { get; init; }
    public IReadOnlyList<Guid> AllowedBranchIds { get; init; } = Array.Empty<Guid>();
    public bool AllBranchesAllowed => false;
}

public class MultiLevelOrderRepository
{
    public static async Task<IEnumerable<dynamic>> GetOrdersAsync(
        DbConnection connection,
        IOrganizationContext orgContext,
        CancellationToken ct)
    {
        var parameters = new DynamicParameters();
        // Binds @TenantId, @CompanyId, and @BranchId in a single step
        parameters.WithOrganization(orgContext);

        const string sql = "SELECT * FROM orders WHERE tenant_id = @TenantId AND company_id = @CompanyId AND branch_id = @BranchId";
        return await connection.QueryAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }
}
```
