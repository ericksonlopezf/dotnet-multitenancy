# Contributing to EricksonLopez.MultiTenancy

Thank you for your interest in contributing to `EricksonLopez.MultiTenancy`. This document outlines the technical guidelines, development setup, coding standards, and pull request workflow.

---

## 1. Prerequisites

- **.NET SDK:** .NET 9.0 SDK or newer (source packages target `net8.0` and `net9.0`; analyzers target `netstandard2.0`).
- **Node.js:** Node.js 18+ (required for running mutation quality gate verification scripts).
- **IDE:** Visual Studio 2022 (v17.12+), JetBrains Rider 2024.3+, or VS Code with the C# Dev Kit.
- **Git:** Git 2.40+ configured with signing keys (recommended).

---

## 2. Solution Structure

The repository uses the `.slnx` solution format containing 15 production libraries, 1 showcase sample, and 17 test projects:

```
src/
  ├── EricksonLopez.MultiTenancy.Abstractions/
  ├── EricksonLopez.MultiTenancy/
  ├── EricksonLopez.MultiTenancy.Analyzers/
  ├── EricksonLopez.MultiTenancy.AspNetCore/
  ├── EricksonLopez.MultiTenancy.Authentication/
  ├── EricksonLopez.MultiTenancy.Configuration/
  ├── EricksonLopez.MultiTenancy.Dapper/
  ├── EricksonLopez.MultiTenancy.MariaDb/
  ├── EricksonLopez.MultiTenancy.MySql/
  ├── EricksonLopez.MultiTenancy.OpenTelemetry/
  ├── EricksonLopez.MultiTenancy.Oracle/
  ├── EricksonLopez.MultiTenancy.PostgreSql/
  ├── EricksonLopez.MultiTenancy.Sqlite/
  ├── EricksonLopez.MultiTenancy.SqlServer/
  └── EricksonLopez.MultiTenancy.Testing/
samples/
  └── EricksonLopez.MultiTenancy.Showcase/
tests/
  └── [17 test projects covering unit, integration, architecture, analyzers, and AOT]
```

---

## 3. Build and Test Commands

### Restore Dependencies
```bash
dotnet restore EricksonLopez.MultiTenancy.slnx
```

### Build Solution (Release)
```bash
dotnet build EricksonLopez.MultiTenancy.slnx --configuration Release
```

### Run Unit and Architecture Tests
```bash
dotnet test EricksonLopez.MultiTenancy.slnx --configuration Release --no-build
```

### Run Tests with Code Coverage Collection
```bash
dotnet test EricksonLopez.MultiTenancy.slnx \
  --configuration Release \
  --no-build \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover,cobertura
```

### Run Mutation Testing (Stryker)
To run mutation testing on a specific package:
```bash
dotnet tool restore
dotnet stryker --config-file stryker-config.json
```

---

## 4. Architectural Invariants

All contributions must adhere to the core architectural principles:

1. **Native AOT & Trimming First:** Zero dynamic reflection or unbounded code generation in hot paths. All source packages must preserve `<IsAotCompatible>true</IsAotCompatible>`.
2. **Defense-in-Depth:** Application-level tenant checks (`WHERE tenant_id = @TenantId`) and database-level isolation (`SET LOCAL` RLS / `SESSION_CONTEXT`) are both required. Neither replaces the other.
3. **No Static Context State:** `ITenantContextAccessor` is Scoped. Never introduce static `AsyncLocal` state without scope lifecycle cleanup.
4. **Precedence Policy:** Cryptographically verified claims (JWT) always override unauthenticated headers or routes.
5. **No Implicit SQL Rewriting:** SQL queries must remain explicit and auditable (ADR-007).

---

## 5. Branching and Commit Conventions

### Branch Strategy
- `main`: Production-ready release branch. Releases are tagged and published from here.
- `develop`: Integration branch for ongoing development and feature branches.
- `feat/*`, `fix/*`, `docs/*`, `refactor/*`: Short-lived feature and fix branches.

### Conventional Commits
All commit messages must follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

- `feat: add base path tenant resolution strategy`
- `fix: prevent potential context leakage during scope disposal`
- `docs: update PostgreSQL RLS integration guide`
- `test: add mutation tests for ScopedTenantContextAccessor`
- `refactor: optimize TenantId struct memory footprint`

---

## 6. Pull Request Guidelines

Before submitting a Pull Request:

1. Ensure the solution compiles with zero warnings (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
2. Verify all unit, integration, and architecture tests pass (`dotnet test`).
3. Ensure line coverage remains at 100% and Stryker mutation score satisfies the break threshold (≥ 95%).
4. Update or add XML documentation comments for any new public API members (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`).
5. Fill out the complete checklist in `.github/PULL_REQUEST_TEMPLATE.md`.

---

## 7. Community & Code of Conduct

Please review our [Code of Conduct](CODE_OF_CONDUCT.md) and [Security Policy](SECURITY.md) before participating.
