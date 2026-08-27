# Level 05: ASP.NET Core Middleware, Filters & Endpoint Routing

Discover deep integration features with ASP.NET Core Minimal APIs and MVC Controllers.

## Endpoint Filters for Tenant Enforcement

```csharp
app.MapGroup("/api/admin")
    .RequireTenant() // Returns 401/400 if tenant is not resolved
    .MapGet("/users", async (IUserRepository repo) =>
    {
        return Results.Ok(await repo.GetAllAsync());
    });
```

## Background Job Scoping (`ITenantScopeFactory`)

For background tasks, Hangfire jobs, or queue consumers:

```csharp
public class OrderProcessingWorker
{
    private readonly ITenantScopeFactory _scopeFactory;

    public OrderProcessingWorker(ITenantScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task ProcessOrderAsync(TenantId tenantId, Guid orderId)
    {
        // Creates an isolated IServiceScope with the tenant pre-configured
        using var scope = _scopeFactory.CreateScope(tenantId);
        
        var processor = scope.ServiceProvider.GetRequiredService<IOrderProcessor>();
        await processor.ProcessAsync(orderId);
    }
}
```
