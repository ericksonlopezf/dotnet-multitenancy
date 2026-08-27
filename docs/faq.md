# Frequently Asked Architectural Questions (FAQ) — EricksonLopez.MultiTenancy

Answers to core architectural, security, and design questions about `EricksonLopez.MultiTenancy`.

---

## 1. Why was Entity Framework Core support removed?
**Answer:** See [ADR-001](adr/adr-001-remove-entityframeworkcore-package.md).  
Entity Framework Core integrations relied heavily on runtime reflection and dynamic LINQ expression tree rewriting, which broken Native AOT compilation. More importantly, global query filters in EF Core only apply within the application process; any direct SQL execution, report query, or Dapper script bypassed those filters completely. We chose to enforce isolation at the database level via PostgreSQL RLS and explicit Dapper queries.

---

## 2. Is `EricksonLopez.MultiTenancy` compatible with Native AOT and Trimming?
**Answer:** Yes.  
Every package in the ecosystem is compiled with `<IsAotCompatible>true</IsAotCompatible>` and `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`. We avoid runtime reflection, dynamic proxy generation, and unbounded assembly scanning.

---

## 3. Why reject implicit SQL query rewriting?
**Answer:** See [ADR-007](adr/adr-007-reject-implicit-tenant-sql-rewriting.md).  
Dynamic SQL string parsing (via regex or SQL AST parsers) introduces severe runtime allocation overhead, degrades database query plan caching, and can silently omit `WHERE tenant_id` clauses on complex CTEs, subqueries, or window functions. Explicit SQL parameterization combined with database RLS provides true, auditable Defense-in-Depth.

---

## 4. How does the library prevent connection pool state leakage?
**Answer:** See [ADR-009](adr/adr-009-reject-set-session-rls.md).  
In PostgreSQL, setting session state with `SET` persists across requests on the same physical pooled connection. `EricksonLopez.MultiTenancy.PostgreSql` exclusively uses `SET LOCAL app.current_tenant_id = :id` within explicit database transactions. When the transaction commits or rolls back, PostgreSQL automatically resets the setting, returning the connection to the pool in a clean state.

---

## 5. Why is `ITenantContextAccessor` Scoped rather than Singleton?
**Answer:** See [ADR-002](adr/adr-002-redesign-async-local-accessor.md).  
Static `AsyncLocal<T>` accessors registered as Singletons risk leaking tenant state across threads when thread pool workers are recycled. By making `ITenantContextAccessor` Scoped, the context lifetime is strictly tied to the HTTP request or background DI scope.

---

## 6. How do platform admins execute cross-tenant operations?
**Answer:** See [ADR-004](adr/adr-004-reject-null-tenantid-bypass.md).  
Passing `TenantId = null` or `TenantId.Empty` to bypass isolation filters is strictly forbidden. Cross-tenant operations (such as billing aggregation or maintenance) must use dedicated elevated database roles (`BYPASSRLS`) and explicit administrative contexts with immutable audit logging.

---

## 7. What happens when multiple resolution strategies detect conflicting tenants?
**Answer:** See [ADR-008](adr/adr-008-adopt-fail-closed-tenant-resolution-conflict-detection.md).  
The pipeline fails closed immediately, throwing `InvalidOperationException` and returning HTTP 400 Bad Request. Mismatches between claims and client headers are treated as potential tenant spoofing attacks.
