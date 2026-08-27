# Level 03: Database-Per-Tenant Isolation Pattern

In high-compliance or enterprise multi-tenant systems, each tenant may require a physically isolated database instance or file.

## Connection String Provider Pattern

```csharp
public interface ITenantConnectionFactory
{
    IDbConnection CreateConnection(TenantId tenantId);
}

public class TenantConnectionFactory : ITenantConnectionFactory
{
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;

    public TenantConnectionFactory(ITenantContext tenantContext, IConfiguration configuration)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    public IDbConnection CreateConnection(TenantId tenantId)
    {
        var connectionString = _configuration.GetConnectionString($"Tenant_{tenantId}");
        return new NpgsqlConnection(connectionString);
    }
}
```

## SQLite DB-per-Tenant

With `EricksonLopez.MultiTenancy.Sqlite`, SQLite connection paths can be automatically scoped to tenant-specific `.db` files:

```csharp
builder.Services.AddMultiTenancy()
    .WithSqliteDatabasePerTenant(options =>
    {
        options.BasePath = "/data/tenants";
        options.FileNamePattern = "{tenant_id}.db";
    });
```
