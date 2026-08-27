# Per-Tenant Authentication & Options — EricksonLopez.MultiTenancy

> **Packages:** `EricksonLopez.MultiTenancy.Authentication`, `EricksonLopez.MultiTenancy.AspNetCore`

In complex SaaS environments, different tenants often require customized authentication providers (e.g., Tenant A uses Azure AD / Entra ID, while Tenant B uses Okta or local cookies) and customized configuration settings (e.g., branding, connection timeouts, feature flags).

---

## 1. Per-Tenant Authentication Schemes

The `EricksonLopez.MultiTenancy.Authentication` package allows routing authentication requests dynamically based on the resolved `ITenantContext`.

```csharp
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMultiTenancy();
builder.Services.AddAspNetCoreMultiTenancy();

// Configure standard authentication schemes
builder.Services.AddAuthentication()
    .AddCookie("AcmeCookie", options => { options.Cookie.Name = "Acme_Auth"; })
    .AddCookie("GlobexCookie", options => { options.Cookie.Name = "Globex_Auth"; });

// Register dynamic per-tenant scheme selector
builder.Services.AddPerTenantAuthentication<TenantInfo>(options =>
{
    options.DefaultSchemeSelector = tenant => tenant.Name switch
    {
        "acme" => "AcmeCookie",
        "globex" => "GlobexCookie",
        _ => "DefaultScheme"
    };
});
```

---

## 2. Per-Tenant Cookie Validation Events

`TenantCookieAuthenticationEvents<TTenant>` validates that the tenant stored in the user's cookie claims matches the actively resolved `ITenantContext`. If a user attempts to access Tenant B using a valid session cookie issued for Tenant A, the event automatically rejects the principal.

```csharp
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.EventsType = typeof(TenantCookieAuthenticationEvents<TenantInfo>);
    });

builder.Services.AddScoped<TenantCookieAuthenticationEvents<TenantInfo>>();
```

---

## 3. Per-Tenant Options Pattern (`TenantOptionsCache`)

`TenantOptionsCache<TOptions, TTenant>` provides isolated, cached configurations per tenant without allocating new option instances per request.

```csharp
public class TenantThemeOptions
{
    public string PrimaryColor { get; set; } = "#007ACC";
    public bool EnableBetaFeatures { get; set; }
}

// In Program.cs:
builder.Services.AddPerTenantOptions<TenantThemeOptions, TenantInfo>((options, tenant) =>
{
    if (tenant.Name == "acme")
    {
        options.PrimaryColor = "#FF0000";
        options.EnableBetaFeatures = true;
    }
});
```
