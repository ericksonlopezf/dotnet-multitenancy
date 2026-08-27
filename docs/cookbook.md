# EricksonLopez.MultiTenancy Cookbook

A collection of 12 production-ready, verified engineering recipes for implementing secure multi-tenancy in .NET applications.

---

## Table of Contents

1. [Recipe 1: Minimal Multi-Tenant Web API](#recipe-1-minimal-multi-tenant-web-api)
2. [Recipe 2: Hostname / Subdomain Tenant Resolution](#recipe-2-hostname--subdomain-tenant-resolution)
3. [Recipe 3: Route-Based Tenant Resolution](#recipe-3-route-based-tenant-resolution)
4. [Recipe 4: Base Path Segment Tenant Resolution](#recipe-4-base-path-segment-tenant-resolution)
5. [Recipe 5: Authenticated Claims (JWT) Resolution](#recipe-5-authenticated-claims-jwt-resolution)
6. [Recipe 6: Explicit Dapper Parameterization](#recipe-6-explicit-dapper-parameterization)
7. [Recipe 7: PostgreSQL Row Level Security (RLS) with SET LOCAL](#recipe-7-postgresql-row-level-security-rls-with-set-local)
8. [Recipe 8: Microsoft SQL Server SESSION_CONTEXT Integration](#recipe-8-microsoft-sql-server-session_context-integration)
9. [Recipe 9: Background Job Scoping with ITenantScopeFactory](#recipe-9-background-job-scoping-with-itenantscopefactory)
10. [Recipe 10: Per-Tenant Options Pattern](#recipe-10-per-tenant-options-pattern)
11. [Recipe 11: OpenTelemetry Tracing & Metrics](#recipe-11-opentelemetry-tracing--metrics)
12. [Recipe 12: Unit & Integration Testing Harnesses](#recipe-12-unit--integration-testing-harnesses)

---

## Recipe 1: Minimal Multi-Tenant Web API

```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register Core MultiTenancy + ASP.NET Core Middleware
builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

// Seed In-Memory Store
builder.Services.AddInMemoryTenantStore<TenantInfo>(store =>
{
    store.AddOrUpdate(new TenantInfo(
        TenantId.Create("11111111-1111-1111-1111-111111111111"),
        "acme",
        isActive: true));
});

var app = builder.Build();

app.UseMultiTenancy();

app.MapGet("/api/tenant", (ITenantContext context) =>
{
    var tenant = context.RequiredTenant;
    return Results.Ok(new { tenant.Id, tenant.Name });
}).RequireTenant();

app.Run();
```

---

## Recipe 2: Hostname / Subdomain Tenant Resolution

Resolves tenants from subdomains (e.g. `acme.app.com` -> `acme`).

```csharp
builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

// Subdomain strategy extracts the first host segment
builder.Services.AddHostNameTenantStrategy();
```

---

## Recipe 3: Route-Based Tenant Resolution

Resolves tenants from ASP.NET Core route values (e.g. `/api/{tenantId}/invoices`).

```csharp
builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

// Injects RouteTenantResolutionStrategy looking for "tenantId" route value
builder.Services.AddRouteTenantStrategy("tenantId");

var app = builder.Build();
app.UseMultiTenancy();

app.MapGet("/api/{tenantId}/invoices", (ITenantContext context) =>
{
    return Results.Ok(new { Tenant = context.RequiredTenant.Name });
}).RequireTenant();
```

---

## Recipe 4: Base Path Segment Tenant Resolution

Extracts the tenant identifier from the first URL path segment (e.g. `/t/acme-corp/dashboard`).

```csharp
builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

// Extracts tenant from the path segment following '/t/'
builder.Services.AddBasePathTenantStrategy("t");
```

---

## Recipe 5: Authenticated Claims (JWT) Resolution

Extracts tenant identity directly from cryptographically signed JWT tokens (`tenant_id`, `tid`, or `tenant` claims).

```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options => { /* JWT Bearer configuration */ });

builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy(); // Claim strategy is active by default with Priority 1

var app = builder.Build();

app.UseAuthentication();
app.UseMultiTenancy(); // Must be placed AFTER UseAuthentication
app.UseAuthorization();
```

---

## Recipe 6: Explicit Dapper Parameterization

```csharp
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;
using System.Data.Common;

public class OrderRepository
{
    private readonly ITenantContext _tenantContext;
    private readonly DbConnection _connection;

    public OrderRepository(ITenantContext tenantContext, DbConnection connection)
    {
        _tenantContext = tenantContext;
        _connection = connection;
    }

    public async Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        // Explicit parameter injection (Layer 1 Defense-in-Depth)
        var parameters = _tenantContext.CreateTenantParameters(new { OrderId = orderId });

        return await _connection.QuerySingleOrDefaultAsync<Order>(
            "SELECT * FROM orders WHERE id = @OrderId AND tenant_id = @TenantId",
            parameters);
    }
}
```

---

## Recipe 7: PostgreSQL Row Level Security (RLS) with SET LOCAL

```csharp
using EricksonLopez.MultiTenancy.PostgreSql;
using Npgsql;

public class SecureInvoiceService
{
    private readonly NpgsqlConnection _connection;
    private readonly ITenantContext _tenantContext;

    public SecureInvoiceService(NpgsqlConnection connection, ITenantContext tenantContext)
    {
        _connection = connection;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesAsync()
    {
        // Atomically opens connection and sets SET LOCAL app.current_tenant_id = :tenantId
        await using var transaction = await _connection.BeginTenantTransactionAsync(_tenantContext);

        var invoices = await _connection.QueryAsync<Invoice>(
            "SELECT * FROM invoices",
            transaction: transaction);

        await transaction.CommitAsync();
        return invoices.ToList();
    }
}
```

---

## Recipe 8: Microsoft SQL Server SESSION_CONTEXT Integration

```csharp
using EricksonLopez.MultiTenancy.SqlServer;
using Microsoft.Data.SqlClient;

public class SqlServerOrderService
{
    private readonly SqlConnection _connection;
    private readonly ITenantContext _tenantContext;

    public SqlServerOrderService(SqlConnection connection, ITenantContext tenantContext)
    {
        _connection = connection;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<Order>> GetOrdersAsync()
    {
        // Executes sp_set_session_context 'tenant_id', @TenantId
        await using var transaction = await _connection.BeginTenantTransactionAsync(_tenantContext);

        var orders = await _connection.QueryAsync<Order>(
            "SELECT * FROM orders",
            transaction: transaction);

        await transaction.CommitAsync();
        return orders.ToList();
    }
}
```

---

## Recipe 9: Background Job Scoping with ITenantScopeFactory

```csharp
using EricksonLopez.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;

public class BackgroundInvoiceProcessor
{
    private readonly ITenantStore _store;
    private readonly ITenantScopeFactory _scopeFactory;

    public BackgroundInvoiceProcessor(ITenantStore store, ITenantScopeFactory scopeFactory)
    {
        _store = store;
        _scopeFactory = scopeFactory;
    }

    public async Task ProcessTenantInvoicesAsync(TenantId tenantId, CancellationToken ct)
    {
        var tenantInfo = await _store.GetTenantAsync(tenantId, ct);
        if (tenantInfo is null || !tenantInfo.IsActive) return;

        // Creates an isolated DI scope with pre-populated Scoped ITenantContext
        await using var scope = _scopeFactory.CreateScope(tenantInfo);

        var service = scope.ServiceProvider.GetRequiredService<IInvoiceBillingService>();
        await service.ProcessPendingInvoicesAsync(ct);
    }
}
```

---

## Recipe 10: Per-Tenant Options Pattern

```csharp
public class TenantThemeOptions
{
    public string PrimaryColor { get; set; } = "#000000";
    public string LogoUrl { get; set; } = "/images/default-logo.png";
}

// In Program.cs:
builder.Services.AddPerTenantOptions<TenantThemeOptions, TenantInfo>((options, tenant) =>
{
    options.PrimaryColor = tenant.Name == "acme" ? "#FF0000" : "#00FF00";
});
```

---

## Recipe 11: OpenTelemetry Tracing & Metrics

```csharp
using EricksonLopez.MultiTenancy.OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(TenantActivitySource.ActivitySourceName)
        .AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter(TenantMetrics.MeterName));
```

---

## Recipe 12: Unit & Integration Testing Harnesses

```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Testing;
using Xunit;

public class OrderServiceTests
{
    [Fact]
    public async Task GetOrders_ShouldReturnTenantScopedData()
    {
        // 1. Arrange test doubles
        var tenantId = TenantId.NewId();
        var tenantContext = new TenantContextBuilder()
            .WithId(tenantId)
            .WithName("test-tenant")
            .Build();

        var fakeStore = new FakeTenantStore<TenantInfo>();
        fakeStore.Add(new TenantInfo(tenantId, "test-tenant", isActive: true));

        // 2. Act
        var sut = new OrderService(tenantContext);
        var result = await sut.GetOrdersForCurrentTenantAsync();

        // 3. Assert
        Assert.NotNull(result);
        Assert.Equal(tenantId, sut.CurrentTenantId);
    }
}
```
