# ADR-D02: Rejection of Unbounded In-Memory Tenant Caching

## Status
Rejected (Discard Record)

## Date
2026-09-04

## Context
In multi-tenant SaaS systems, tenant stores frequently resolve tenant metadata from relational databases, Redis, or external identity providers. Storing resolved tenant metadata directly in unbounded in-memory dictionaries (`ConcurrentDictionary<string, TenantInfo>` without TTL or eviction) is common practice in basic libraries.

## Rationale for Rejection
1. **Memory Exhaustion (Denial of Service)**: Attackers can send HTTP requests with random, non-existent hostnames or tenant headers, forcing the store to populate unbounded cache dictionaries until the host process experiences Out-Of-Memory (OOM) crashes.
2. **Stale Security Invariants**: If a tenant is suspended, disabled, or has their subscription downgraded, an unbounded non-evicting in-memory cache will continue serving active tenant contexts indefinitely across distributed instances.
3. **High Cardinality Scaling Bottleneck**: In platforms serving 100,000+ tenants, caching all tenant records in RAM on every worker node wastes server memory.

## Enforced Alternative
1. **Bounded LRU / Sliding Expiration Caching**: Utilizing `IMemoryCache` with explicit sliding expiration, maximum size limits, and deterministic eviction policies.
2. **Direct Relational Database Lookup**: Direct parameter-bound queries against `ITenantStore` backing stores with index optimizations.
