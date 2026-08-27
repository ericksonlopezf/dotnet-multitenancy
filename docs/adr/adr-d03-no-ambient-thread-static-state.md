# ADR-D03: Rejection of Ambient ThreadStatic State

## Status
Rejected (Discard Record)

## Context
A legacy design in older .NET multi-tenancy frameworks is to store the resolved tenant identifier in a `[ThreadStatic]` field or static accessor to make the tenant ID globally accessible across all static methods without constructor injection.

## Rationale for Rejection
1. **Async/Await Context Loss**: `[ThreadStatic]` does not flow across asynchronous `await` continuations. When an async operation resumes on a different thread pool worker, the `[ThreadStatic]` value is either empty or contains the tenant ID of another unrelated thread.
2. **Hidden Ambient State**: Static ambient access creates hidden dependencies, making unit testing impossible without global state manipulation.
3. **Severe Concurrency Leaks**: Recycled threads in high-concurrency ASP.NET Core pipelines retain dirty thread-static values, causing silent cross-tenant data corruption.

## Enforced Alternative
1. **Scoped Write-Once Accessor**: `ScopedTenantContextAccessor` registered with `ServiceLifetime.Scoped`, ensuring tenant context is bound strictly to the active `IServiceScope`.
2. **Explicit Constructor Injection**: Requiring all domain and application services to explicitly receive `ITenantContext` via dependency injection.
