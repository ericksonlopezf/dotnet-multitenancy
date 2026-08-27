# Level 02: Tenant Resolution Strategies & Precedence

`EricksonLopez.MultiTenancy.AspNetCore` offers multiple resolution strategies with deterministic fail-closed precedence.

## Resolution Strategies

1. **Claims Strategy (`WithClaimStrategy`):** Resolves tenant identifier from authenticated JWT/cookie claims (e.g. `tenant_id`).
2. **Host Strategy (`WithHostStrategy`):** Resolves tenant from subdomain (e.g. `tenant1.app.domain.com`).
3. **Route Strategy (`WithRouteStrategy`):** Resolves tenant from URL path segment (e.g. `/api/{tenant}/orders`).
4. **Header Strategy (`WithHeaderStrategy`):** Resolves tenant from request header (e.g. `X-Tenant-Id`).

## Precedence & Conflict Detection (ADR-008)

When multiple strategies are configured:

```csharp
builder.Services.AddMultiTenancy()
    .WithClaimStrategy("tenant_id")
    .WithHeaderStrategy("X-Tenant-Id");
```

- **Claims Precedence:** Authenticated user claims always take precedence over client-supplied headers.
- **Fail-Closed Conflict Detection:** If an authenticated request provides header `X-Tenant-Id: B` but the user's JWT specifies `tenant_id: A`, the request is **immediately rejected (400/403)** to prevent tenant spoofing attacks!
