# ADR 008: Adopt Fail-Closed Tenant Resolution Conflict Detection

**Date:** 2026-08-22  
**Status:** Accepted  
**Context:** Tenant Resolution Middleware

## Context

In multi-tenant applications, determining the current tenant for a request is critical for security and data isolation. `EricksonLopez.MultiTenancy` supports a pipeline of `ITenantResolutionStrategy` implementations (e.g., Claim, Header, Route, Host) to resolve the `TenantId`.

Previously, the `TenantResolutionMiddleware` evaluated strategies in order and short-circuited as soon as the first strategy successfully resolved a `TenantId`. This "first-wins" approach, while performant, introduced a subtle security risk:

If a request contains multiple conflicting tenant identifiers (e.g., an authenticated JWT claim for Tenant A, and a manipulated `X-Tenant-ID` header for Tenant B), the resolution outcome depends entirely on the registration order of the strategies in the DI container.

While ADR-003 dictates that authenticated claims must take precedence over headers, relying solely on DI registration order to enforce this security invariant is brittle. A developer might accidentally reorder strategy registrations, or a new strategy might be introduced before the claim strategy, inadvertently allowing an attacker to override the tenant context via an untrusted input.

## Decision

We will **abandon the "first-wins" short-circuiting logic** in the `TenantResolutionMiddleware` and **adopt a fail-closed conflict detection model**.

1. The middleware will evaluate *all* registered resolution strategies.
2. If more than one strategy successfully resolves a `TenantId`, and those IDs are *different*, the middleware will immediately abort the request and throw an `InvalidOperationException`.
3. If multiple strategies resolve the *same* `TenantId`, the request proceeds normally.
4. If no strategy resolves a `TenantId`, the request proceeds normally (allowing the `RequireTenantFilter` to enforce the requirement later).

## Consequences

### Positive

* **Security by Design:** Enforces strict consistency. It is mathematically impossible for an attacker to spoof a tenant context by injecting a secondary identifier (like a header or route parameter) if the primary identifier (like an authenticated JWT claim) points to a different tenant. The conflict will trigger a fail-closed abort.
* **Resilience to Misconfiguration:** The security of the resolution pipeline no longer depends on the exact DI registration order of the strategies.
* **Auditability:** Conflicts indicate either a malicious attempt or a severely misconfigured client/gateway, both of which are highly actionable events that will now be explicitly logged as errors rather than silently ignored.

### Negative

* **Performance Impact:** All strategies in the pipeline are now evaluated for every request, whereas previously, resolution stopped at the first success. Given that strategies are typically very fast (reading a header or claim), the impact is minimal. However, we must ensure that no `ITenantResolutionStrategy` performs blocking or expensive I/O operations (like database queries) during the `ResolveTenantIdAsync` phase.
* **Reduced Flexibility:** Clients can no longer legitimately send conflicting identifiers and expect the server to "pick the right one". This is a desired constraint, but it may break existing clients that relied on this loose behavior.

## Alternatives Considered

* **Weighted Strategies:** Assigning explicit priority weights to strategies (e.g., Claim = 100, Header = 10) instead of relying on DI order. This solves the DI order problem but still silently discards the conflicting input, which might hide underlying application logic errors or malicious probing.
* **Warning on Conflict:** Logging a warning when a conflict occurs but still using the highest-priority strategy's result. This fails to provide a strong security guarantee and relies on active log monitoring to detect spoofing attempts.

## See Also
* ADR-003: Reject Header-First Precedence Policy
