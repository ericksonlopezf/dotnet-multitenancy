# Quick Start Guide — EricksonLopez.MultiTenancy

Integrate secure, fail-closed multi-tenancy into your .NET application in 5 minutes.

---

## 1. Install Packages

```bash
dotnet add package EricksonLopez.MultiTenancy
dotnet add package EricksonLopez.MultiTenancy.AspNetCore
dotnet add package EricksonLopez.MultiTenancy.Dapper
dotnet add package EricksonLopez.MultiTenancy.PostgreSql
```

---

## 2. Configure Multi-Tenancy (`Program.cs`)

```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Register MultiTenancy Core & ASP.NET Core Middleware
builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

// 2. Add resolution strategies (Precedence: Claims > Host > Route)
builder.Services.AddHostNameTenantStrategy();
builder.Services.AddRouteTenantStrategy("tenantId");

// 3. Register In-Memory Store for testing
builder.Services.AddInMemoryTenantStore<TenantInfo>(store =>
{
    store.AddOrUpdate(new TenantInfo(
        id: TenantId.Create("11111111-1111-1111-1111-111111111111"),
        name: "acme-corp",
        isActive: true));
});

var app = builder.Build();

// 4. Activate resolution middleware
app.UseMultiTenancy();

// 5. Define protected endpoint
app.MapGet("/api/tenant", (ITenantContext context) =>
{
    var tenant = context.RequiredTenant;
    return Results.Ok(new { tenant.Id, tenant.Name });
}).RequireTenant();

app.Run();
```

---

## 3. Query with Dapper & Explicit Tenant Parameters

```csharp
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;
using System.Data.Common;

public class OrderRepository
{
    private readonly ITenantContext _context;
    private readonly DbConnection _connection;

    public OrderRepository(ITenantContext context, DbConnection connection)
    {
        _context = context;
        _connection = connection;
    }

    public async Task<IEnumerable<Order>> GetOrdersAsync()
    {
        // Explicit parameter injection guarantees Layer 1 defense
        var parameters = _context.CreateTenantParameters();
        return await _connection.QueryAsync<Order>(
            "SELECT * FROM orders WHERE tenant_id = @TenantId",
            parameters);
    }
}
```

---

## 4. Run and Verify

Start your application:
```bash
dotnet run
```

Send a request with a tenant host header:
```bash
curl -H "Host: acme-corp.localhost" http://localhost:5000/api/tenant
```

**Expected Response:**
```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "acme-corp"
}
```
