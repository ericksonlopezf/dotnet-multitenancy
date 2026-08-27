# Level 06: Per-Tenant Authentication Schemes

In multi-tenant systems, different tenants often use distinct Identity Providers (e.g. Okta, Azure Entra ID, Auth0).

## Configuration with `EricksonLopez.MultiTenancy.Authentication`

```csharp
builder.Services.AddMultiTenancy()
    .WithPerTenantAuthentication(options =>
    {
        options.ConfigureJwtBearerForTenant = (tenant, jwtOptions) =>
        {
            jwtOptions.Authority = tenant.Properties["IdpAuthority"];
            jwtOptions.Audience = tenant.Properties["IdpAudience"];
        };
    });
```

This dynamically binds token validation parameters per-request based on the resolved tenant!
