# ADR-012: Observability & OpenTelemetry Tenant Enrichment

## Status
Accepted

## Date
2026-09-04

## Context
In distributed multi-tenant microservices, tracing, logging, and metrics must be partitioned by tenant identity to allow site reliability engineers (SREs) and security teams to monitor tenant-specific SLA compliance, latency distributions, and isolate noisy neighbors.

However, naive observability implementations introduce:
1. **PII and Sensitive Data Leakage**: Inadvertently logging secret connection strings, credentials, or PII.
2. **High Memory Overhead & String Allocations**: Allocating new activity tags and baggage strings on every span.
3. **Trace Discontinuity**: Failing to propagate tenant context across asynchronous message queues or gRPC boundaries.

## Decision
In `EricksonLopez.MultiTenancy.OpenTelemetry`, we establish:
1. **Standardized Semantic Attributes**: Using `tenant.id`, `tenant.name`, and `tenant.tier` adhering strictly to OpenTelemetry Semantic Conventions.
2. **Deterministic Baggage Propagation**: Injecting `tenant.id` into `Baggage.SetBaggage()` to ensure W3C trace context automatically crosses HTTP and messaging boundaries.
3. **Metrics Tagging**: Recording metrics (e.g., `multitenancy.resolutions`, `multitenancy.store.lookups`) enriched with low-cardinality tags (`tenant.status`, `resolution.strategy`). High-cardinality unbounded strings in metrics are prohibited to prevent metric explosion.
4. **Safety Filter**: Prohibiting tenant custom properties or connection details from entering OpenTelemetry trace baggage or span attributes.

## Consequences
### Positive
- Unified telemetry across distributed traces, logs, and Prometheus/OTel metric collectors.
- Zero PII leakage in observability streams.
- Seamless correlation between tenant resolution, database query execution, and downstream HTTP calls.

### Negative
- Developers must explicitly whitelist any additional custom tenant properties intended for trace tags.
