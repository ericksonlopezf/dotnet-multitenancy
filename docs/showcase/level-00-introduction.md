# Level 00: Multi-Tenancy Foundations & Architectural Overview

Welcome to the `EricksonLopez.MultiTenancy` Showcase series!

## What is Multi-Tenancy?
In modern SaaS architectures, multi-tenancy allows a single application deployment to serve multiple distinct organizations (tenants) while guaranteeing strict data isolation, tenant-specific customizations, and resource efficiency.

## Core Architectural Pillars of `EricksonLopez.MultiTenancy`:

1. **Strongly-Typed Identifiers (`TenantId`):**
   Immutable 16-byte `readonly record struct` wrapping a `Guid`, eliminating string comparison bugs and allocation overhead.
2. **Fail-Closed Resolution:**
   Resolution strategies operate under strict precedence (Claims > Host > Route > Header). Conflicting tenant inputs trigger an immediate fail-closed rejection.
3. **Database-Level Isolation:**
   First-class support for transaction-scoped PostgreSQL Row-Level Security (`SET LOCAL`), SQL Server `SESSION_CONTEXT`, Oracle VPD, MySQL/MariaDB session variables, and SQLite DB-per-tenant.
4. **Native AOT & Trimming Support:**
   Zero runtime reflection, zero dynamic code generation, and trimmed-safe APIs.
5. **Zero Context Leakage:**
   Scoped accessors tied to `IServiceScope` lifecycles ensure zero cross-request contamination in async and connection-pooled environments.
