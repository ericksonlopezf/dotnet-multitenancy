# Level 01: Getting Started & Service Registration

Learn how to install and configure `EricksonLopez.MultiTenancy` in your .NET ASP.NET Core project.

## Installation

```bash
dotnet add package EricksonLopez.MultiTenancy
dotnet add package EricksonLopez.MultiTenancy.AspNetCore
```

## Basic Configuration (`Program.cs`)

```csharp
using EricksonLopez.MultiTenancy;

var builder = WebApplication.CreateBuilder(args);

// Register MultiTenancy core services and stores
builder.Services.AddMultiTenancy()
    .WithInMemoryStore(tenants =>
    {
        tenants.Add(new TenantInfo
        {
            Id = TenantId.Create(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            Name = "Acme Corp",
            Properties = new Dictionary<string, string>
            {
                ["Region"] = "US-East",
                ["Plan"] = "Enterprise"
            }
        });
    })
    .WithHeaderStrategy("X-Tenant-Id");

var app = builder.Build();

// Enable MultiTenancy Middleware in the HTTP Pipeline
app.UseMultiTenancy();

app.MapGet("/api/tenant-info", (ITenantContext context) =>
{
    if (!context.IsResolved)
    {
        return Results.BadRequest(new { error = "Unresolved tenant" });
    }

    return Results.Ok(new
    {
        TenantId = context.RequiredTenant.Id.ToString(),
        TenantName = context.RequiredTenant.Name,
        Properties = context.RequiredTenant.Properties
    });
});

app.Run();
```
