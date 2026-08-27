# ADR-002: REDESIGN — AsyncLocalTenantContextAccessor (Static Field Bug)

## Status
Accepted

## Context
`AsyncLocalTenantContextAccessor` uses a `static readonly AsyncLocal<ITenantContext?>` field and is registered as a Singleton via `TryAddSingleton<ITenantContextAccessor, AsyncLocalTenantContextAccessor>()`.

While `AsyncLocal<T>` is semantically correct for async context propagation (values flow to child tasks), the combination of:
1. A **static** `AsyncLocal<T>` field (process-wide slot)
2. **Singleton** DI registration
3. A **mutable** setter (context can be reassigned)

...creates the following risks:
- The static field persists across request boundaries in edge cases where code sets the context on the ambient execution context (not a child context)
- The mutable setter allows any code to silently overwrite the security-vetted, resolved tenant context
- No mechanism exists to verify or enforce that context is reset at scope disposal
- Singleton registration means all requests share one accessor instance, relying entirely on `AsyncLocal<T>` semantics for isolation

## Decision
Replace `AsyncLocalTenantContextAccessor` with `ScopedTenantContextAccessor`:
- Instance-based (no static fields)
- Registered as **Scoped** (one per DI scope / request)
- Write-once semantics: the `set` accessor throws `InvalidOperationException` if context is already set
- Context reference is cleared automatically when the DI scope is disposed

## Why
1. **Scoped DI alignment:** The tenant context is intrinsically scoped to a request or operation. The accessor should share that lifetime.
2. **Immutability after resolution:** Once the middleware resolves and sets the tenant context, it must not be reassignable. Any reassignment attempt is a bug or an attack.
3. **Disposal guarantees:** DI scope disposal clears the reference automatically — no explicit reset needed.
4. **Testability:** A scoped, instance-based accessor is easier to mock and reason about in tests.

## Alternatives Considered
- **Fix static field with child ExecutionContext:** Complex and error-prone. Consumers would need to know to always create child contexts. Not safely enforceable.
- **Keep Singleton with explicit reset middleware:** Requires every request pipeline to properly reset — fragile, not safe by default.
- **Use HttpContext.Items for storage:** Only works for HTTP requests. Not suitable for background jobs or message consumers.

## Security Impact
**Positive.** Eliminates T09 (async context leakage) and T12 (parallel request leakage) from the threat model.

## Architecture Impact
Breaking change:
- `AsyncLocalTenantContextAccessor` is removed from public API (internal implementation detail)
- `ITenantContextAccessor` DI registration changes from Singleton to Scoped
- Any code that depends on context reassignment will fail (intentionally — it was a bug)

## Performance Impact
Negligible — one additional heap allocation per DI scope (one object per request). No measurable throughput impact.

## AOT Impact
None — both implementations are AOT-safe. No reflection in either.

## Migration
1. Remove any direct construction or resolution of `AsyncLocalTenantContextAccessor`.
2. If upgrading from Singleton registration, verify no code depends on cross-request context sharing (it should not — that was the bug).
3. Remove any explicit Singleton registration of `ITenantContextAccessor`.
4. `AddMultiTenancy()` handles registration automatically.

## Related Components
- `ServiceCollectionExtensions` (updated registration)
- `TenantResolutionMiddleware` (calls accessor.set — now write-once)
- `ITenantScopeFactory` (creates new scope with fresh accessor per background operation)
