# Testing Strategy & Quality Gates — EricksonLopez.MultiTenancy

This document describes the testing philosophy, quality metrics, test suite composition, and mutation testing gate mechanics for the `EricksonLopez.MultiTenancy` ecosystem.

> For CI/CD pipeline architecture and workflow automation, see [ci-cd-and-quality.md](ci-cd-and-quality.md).

---

## 1. Quality Objectives & Target Metrics

The goal of the `EricksonLopez.MultiTenancy` testing strategy is to guarantee that **every architectural invariant, boundary, and public API is exhaustively verified** with deterministic execution, zero flaky tests, and zero unhandled mutation vectors.

| Metric | Target | Current Status | Gate Enforcement |
| :--- | :---: | :---: | :---: |
| **Line Coverage** | **100.00%** | **100.00%** (15/15 packages) | Required before PR merge |
| **Branch Coverage** | **100.00%** | **100.00%** (15/15 packages) | Required before PR merge |
| **Method Coverage** | **100.00%** | **100.00%** (15/15 packages) | Required before PR merge |
| **Mutation Score (Stryker)** | **>= 95.00%** | **100.00%** (Break: <95%, Warn: >=95%, Low: >=98%, High: 100%) | Verified in `publish.yml` & `mutation-testing.yml` |
| **Native AOT Smoke Test** | **100% Pass** | **100% Pass** (`AotSmokeTest`) | Verified in CI on .NET SDK 10.0.x |

---

## 2. Package Quality Tracking Matrix

| Component | Type | Line Cov | Branch Cov | Method Cov | Stryker Score | Status |
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

### 3.1 Unit Tests (15 Packages)

Each source package has a corresponding test project in `/tests/`. Tests cover:
- All public API surface methods and extension points.
- Edge cases for `TenantId` parsing, equality, and JSON serialization.
- Resolution strategy priority and conflict detection.
- Accessor write-once enforcement.
- Store operations (get, lookup, caching, eviction).

### 3.2 Architecture & Boundary Tests (`ArchitectureTests`)

- Verified with **ArchUnitNET** and **NetArchTest.Rules**.
- Enforces zero dependency leakage from `Abstractions` to database SDKs (`Npgsql`, `Microsoft.Data.SqlClient`).
- Ensures `ITenantContextAccessor` cannot be registered as a Singleton.
- Enforces Native AOT rules (no unannotated reflection, no `MakeGenericType` in request pipelines).
- Verifies that L5 Dialect packages do not reference ASP.NET Core Http.

### 3.3 Analyzer Verification (`Analyzers.Tests`)

Uses `Microsoft.CodeAnalysis.CSharp.Workspaces` to verify diagnostics and code fixes for:
- `ELMT001`: Static tenant context field detection.
- `ELMT002`: Captive tenant context in singleton service detection.
- `ELMT003`: Dapper execution without tenant parameter detection.

### 3.4 Native AOT Smoke Test (`AotSmokeTest`)

- Validates that the full ecosystem compiles and executes under Native AOT on the current .NET SDK.
- Exercises core DI registration, accessor, and scope factory without reflection fallbacks.

---

## 4. Mutation Testing — Stryker.NET Configuration

### Threshold Policy

| Level | Score Threshold | Meaning |
| :--- | :---: | :--- |
| **High** | >= 100% | All mutants killed |
| **Low** | >= 98% | Acceptable |
| **Warning** | >= 95% | Approaching break |
| **Break** | < 95% | Gate fails — blocks publish |

### Execution

Mutation testing runs across all 15 packages in parallel via a GitHub Actions matrix (see `mutation-testing.yml`). Results are aggregated by `scripts/record-stryker-result.js` into per-package JSON summary files and evaluated by `scripts/verify-mutation-gate.js` which blocks package publishing in `publish.yml` if any package score falls below 95%.

### Running Locally

```bash
# Install Stryker globally
dotnet tool install --global dotnet-stryker

# Build solution first
dotnet build EricksonLopez.MultiTenancy.slnx --configuration Release

# Run mutation testing for a specific package (e.g., Core)
dotnet stryker --config-file stryker-config.json

# Run for a specific dialect package
dotnet stryker --config-file stryker-postgresql-config.json
```

---

## 5. Coverage Collection

Code coverage is collected using **Coverlet** (`coverlet.msbuild` and `coverlet.collector`) with dual format output:

- `opencover` — consumed by SonarCloud
- `cobertura` — consumed by Codecov

```bash
dotnet test EricksonLopez.MultiTenancy.slnx `
  --configuration Release `
  --no-build `
  --collect:"XPlat Code Coverage" `
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover,cobertura
```

Coverage reports are uploaded to [Codecov](https://codecov.io/gh/ericksonlopezf/dotnet-multitenancy) on every CI run.
