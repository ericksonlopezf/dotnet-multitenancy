# ADR-D01: Rejection of Dynamic SQL Text Rewriting

## Status
Rejected (Discard Record)

## Date
2026-09-04

## Context
Several multi-tenancy libraries and EF Core interceptors attempt to enforce tenant isolation by dynamically parsing raw SQL strings at runtime (via regex or AST rewriters) and automatically injecting `AND tenant_id = 'xxx'` into `WHERE` clauses.

## Rationale for Rejection
1. **SQL Injection Vulnerabilities**: String-manipulation-based query rewriting is brittle and prone to bypasses via complex subqueries, CTEs (`WITH` clauses), `UNION` operators, stored procedure invocations, and database functions.
2. **Extreme CPU Overhead & Heap Allocations**: Parsing SQL strings and constructing new strings on every single query execution adds massive GC pressure and latency penalties.
3. **Impedance with Native AOT**: Complex SQL AST parsers often rely on dynamic reflection or regex engines with large runtime footprints.
4. **False Sense of Security**: Dynamic rewriting fails silently if the parser encounters unsupported SQL dialect syntax, leaving the query without tenant filters.

## Enforced Alternative
1. **Explicit Parameterization (Layer 1)**: Explicitly binding `@TenantId` in Dapper queries via `WithTenant()` or `CreateTenantParameters()`, verified at compile time by Roslyn Analyzer `ELMT003`.
2. **Database Engine Row Level Security (Layers 3 & 4)**: Offloading tenant filtering to PostgreSQL Row Level Security (`SET LOCAL`) and SQL Server `SESSION_CONTEXT`, where the relational database kernel guarantees data isolation.
