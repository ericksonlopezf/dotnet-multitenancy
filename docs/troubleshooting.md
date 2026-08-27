# Troubleshooting Guide — EricksonLopez.MultiTenancy

This guide provides diagnostics and solutions for common configuration errors, resolution conflicts, and database isolation issues.

---

## 1. Resolution & Lifecycle Exceptions

### A. `InvalidOperationException`
- **Symptom:** HTTP 400 Bad Request returned with message `Resolution conflict detected between strategies`.
- **Root Cause:** Two or more configured strategies resolved differing tenant IDs during the same request (e.g. JWT claims resolved Tenant A, but the `X-Tenant-ID` header resolved Tenant B).
- **Fix (ADR-008):** Ensure clients do not supply conflicting headers. If headers are used for service-to-service calls, do not pass conflicting JWT tokens.

---

### B. `InvalidOperationException: ITenantContextAccessor has already been set`
- **Symptom:** Exception thrown when setting `tenantContextAccessor.TenantContext = ...`.
- **Root Cause:** Attempting to re-assign or mutate tenant context within an active request scope. `ScopedTenantContextAccessor` is write-once per scope.
- **Fix:** Do not re-assign tenant context. If running a background operation for a different tenant, create a new DI scope via `ITenantScopeFactory.CreateScope(targetTenant)`.

---

### C. `TenantNotFoundException` or `TenantInactiveException`
- **Symptom:** HTTP 401 Unauthorized or HTTP 404 Not Found returned on endpoints marked with `.RequireTenant()`.
- **Root Cause:** The resolved tenant identifier does not exist in the configured `ITenantStore` or has `IsActive = false`.
- **Fix:** Verify tenant records in your store (`ITenantStore`) and verify that `tenant.IsActive` is set to `true`.

---

## 2. Roslyn Analyzer Errors

### `ELMT001`: Static `TenantContext` Field
- **Error:** `ELMT001: ITenantContext must not be stored in a static field.`
- **Fix:** Replace static field storage with scoped dependency injection into class constructors.

### `ELMT002`: `TenantContext` in Singleton Service
- **Error:** `ELMT002: Cannot inject Scoped ITenantContext into Singleton service.`
- **Fix:** Change the service lifetime to `Scoped`, or inject `ITenantScopeFactory` / `IServiceProvider` to resolve the context on-demand.

### `ELMT003`: Dapper Query Without Tenant Parameter
- **Warning:** `ELMT003: Dapper query executed without tenant parameter helper.`
- **Fix:** Use `tenantContext.CreateTenantParameters()` or `parameters.WithTenant(tenantContext)`.

---

## 3. Database Isolation Issues

### PostgreSQL Query Returns 0 Rows Unexpectedly
- **Symptom:** Query executes successfully but returns no rows even when data exists in the table.
- **Root Cause:** PostgreSQL RLS is enabled, but `SET LOCAL app.current_tenant_id` was not executed, causing `current_setting('app.current_tenant_id', true)` to return `NULL`.
- **Fix:** Ensure queries execute within `connection.BeginTenantTransactionAsync(tenantContext)` referencing the active transaction wrapper.

### PostgreSQL RLS Bypassed
- **Symptom:** Queries return data belonging to other tenants.
- **Root Cause:** Application connection string connects using a PostgreSQL superuser role (e.g. `postgres`).
- **Fix:** Connect using an unprivileged application user (e.g. `app_user`) and ensure tables execute `ALTER TABLE ... FORCE ROW LEVEL SECURITY`.
