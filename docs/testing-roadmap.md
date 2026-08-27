# Framework Testing Roadmap & Quality Assurance

> **Note:** This file is a summary reference document. The canonical testing strategy, quality tracking matrix, and mutation gate mechanics are maintained in [`testing-strategy.md`](testing-strategy.md).

## 1. Quality Objectives & Target Metrics

The goal of the `EricksonLopez.MultiTenancy` testing strategy is to guarantee that **every architectural invariant, boundary, and public API is exhaustively verified** with deterministic execution, zero flaky tests, and zero unhandled mutation vectors.

| Metric | Target | Current Status | Gate Enforcement |
| :--- | :---: | :---: | :---: |
| **Line Coverage** | **100.00%** | **100.00%** (15/15 packages) | Required before PR merge |
| **Branch Coverage** | **100.00%** | **100.00%** (15/15 packages) | Required before PR merge |
| **Method Coverage** | **100.00%** | **100.00%** (15/15 packages) | Required before PR merge |
| **Mutation Score (Stryker)** | **≥ 95.00%** | **100.00%** (Break: <95%, Warn: ≥95%, Low: ≥98%, High: 100%) | Verified in `publish.yml` & `mutation-testing.yml` |
| **Native AOT Smoke Test** | **100% Pass** | **100% Pass** (`AotSmokeTest`) | Verified in CI on .NET SDK 10.0.x |

---

## 2. Package Quality Tracking Matrix

| Component | Type | Line Cov | Branch Cov | Method Cov | Stryker Mutation Score | Status |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| `EricksonLopez.MultiTenancy.Abstractions` | Public API / Contract | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy` (Core) | Core Engine / Scope Factory | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.Analyzers` | Roslyn Analyzers | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.AspNetCore` | Web Resolution / Middleware | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.Authentication` | Per-Tenant Auth Schemes | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.Configuration` | IConfiguration Store | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.Dapper` | Parameter Helpers | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.OpenTelemetry` | Distributed Tracing & Metrics | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.PostgreSql` | PostgreSQL RLS / SET LOCAL | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.SqlServer` | SQL Server SESSION_CONTEXT | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.MySql` | MySQL Session Variables | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.MariaDb` | MariaDB Session Variables | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.Oracle` | Oracle VPD / DBMS_SESSION | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.Sqlite` | SQLite DB-Per-Tenant / Schema | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.Testing` | Test Doubles & Harnesses | 100.00% | 100.00% | 100.00% | 100.00% | **PASSED** |
| `EricksonLopez.MultiTenancy.ArchitectureTests` | Architecture & Boundaries | N/A | N/A | N/A | N/A | **PASSED** |

---

## 3. Test Suites & Architecture Rules

1. **Architecture & Boundary Tests (`ArchitectureTests`):**
   - Verified with `ArchUnitNET` and `NetArchTest.Rules`.
   - Enforces zero dependency leakage from `Abstractions` to database SDKs (`Npgsql`, `Microsoft.Data.SqlClient`).
   - Ensures `ITenantContextAccessor` cannot be registered as a Singleton.
   - Enforces Native AOT rules (no unannotated reflection, no `MakeGenericType` in request pipelines).

2. **Analyzer Verification (`Analyzers.Tests`):**
   - Uses `Microsoft.CodeAnalysis.CSharp.Workspaces` to verify diagnostics and code fixes for `ELMT001`, `ELMT002`, and `ELMT003`.

3. **Mutation Testing Execution (`scripts/verify-mutation-gate.js`):**
   - Runs Stryker.NET in parallel across all 15 matrix jobs.
   - Evaluates consolidated mutation scores in CI.
   - Blocks package publishing in `publish.yml` if any package score falls below 95%.
