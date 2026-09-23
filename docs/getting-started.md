# Getting Started Guide — EricksonLopez.MultiTenancy

A step-by-step guide to adopting `EricksonLopez.MultiTenancy` in your .NET applications.

---

## 1. Installation

Install the core package alongside your chosen web hosting and database integration packages:

```bash
# Core primitives and ASP.NET Core middleware
dotnet add package EricksonLopez.MultiTenancy
dotnet add package EricksonLopez.MultiTenancy.AspNetCore

# Data access with Dapper
dotnet add package EricksonLopez.MultiTenancy.Dapper

# Database dialect (e.g. PostgreSQL)
dotnet add package EricksonLopez.MultiTenancy.PostgreSql
```

---

## 2. Dependency Injection Setup (`Program.cs`)

Configure the multi-tenancy pipeline in your application's bootstrap file:

```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Core MultiTenancy services
builder.Services.AddMultiTenancy();

// 2. Register ASP.NET Core integration (Claims strategy enabled by default)
builder.Services.AddAspNetCoreMultiTenancy();

// 3. Register resolution strategies
builder.Services.AddHostNameTenantStrategy();
builder.Services.AddRouteTenantStrategy("tenantId");

// 4. Configure tenant metadata store
builder.Services.AddInMemoryTenantStore<TenantInfo>(store =>
{
    store.AddOrUpdate(new TenantInfo(
        id: TenantId.Create("11111111-1111-1111-1111-111111111111"),
        name: "acme-corp",
        isActive: true));
    store.AddOrUpdate(new TenantInfo(
        id: TenantId.Create("22222222-2222-2222-2222-222222222222"),
        name: "globex-corp",
        isActive: true));
});

var app = builder.Build();

// 5. Activate resolution middleware
app.UseMultiTenancy();

// 6. Secure endpoints
app.MapGet("/api/profile", (ITenantContext context) =>
{
    var tenant = context.RequiredTenant;
    return Results.Ok(new { Tenant = tenant.Name, Id = tenant.Id.ToString() });
}).RequireTenant();

app.Run();
```

---

## 3. Data Tier Integration with Dapper

Implement repositories with explicit tenant parameterization:

```csharp
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;
using System.Data.Common;

public class ProductRepository
{
    private readonly ITenantContext _tenantContext;
    private readonly DbConnection _connection;

    public ProductRepository(ITenantContext tenantContext, DbConnection connection)
    {
        _tenantContext = tenantContext;
        _connection = connection;
    }

    public async Task<IEnumerable<Product>> GetProductsAsync()
    {
        var parameters = _tenantContext.CreateTenantParameters();
        return await _connection.QueryAsync<Product>(
            "SELECT * FROM products WHERE tenant_id = @TenantId",
            parameters);
    }
}
```

---

## 4. Enabling Database-Level Isolation (PostgreSQL RLS)

Enforce database-level Row Level Security:

```csharp
using EricksonLopez.MultiTenancy.PostgreSql;
using Npgsql;

public class OrderService
{
    private readonly NpgsqlConnection _connection;
    private readonly ITenantContext _tenantContext;

    public OrderService(NpgsqlConnection connection, ITenantContext tenantContext)
    {
        _connection = connection;
        _tenantContext = tenantContext;
    }

    public async Task CreateOrderAsync(Order order)
    {
        // Atomically opens connection and sets SET LOCAL app.current_tenant_id = :tenantId
        await using var transaction = await _connection.BeginTenantTransactionAsync(_tenantContext);

        var parameters = new DynamicParameters(order);
        parameters.WithTenant(_tenantContext);

        await _connection.ExecuteAsync(
            "INSERT INTO orders (id, tenant_id, amount) VALUES (@Id, @TenantId, @Amount)",
            parameters,
            transaction: transaction);

        await transaction.CommitAsync();
    }
}
```

---

## 5. Next Steps

- Explore the **[Cookbook](cookbook.md)** for 12 production-ready recipes.
- Review **[PostgreSQL Row Level Security](rls.md)** for database schema setup.
- Read **[Background Jobs](background-jobs.md)** for non-HTTP scoping patterns.
