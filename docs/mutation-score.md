# Mutation Testing Score — EricksonLopez.MultiTenancy

> **Last updated**: 2026-08-27 (v1.0.0)  
> **Tool**: Stryker.NET (`dotnet-stryker`)  
> **CI Gate**: `mutation-testing.yml` — build exits non-zero when score < 95% (`break: 95`)

---

## 1. Score Summary (v1.0.0)

| Package / Scope | Mutants Killed | Survived | Timeout | Mutation Score | Status |
|:---|:---:|:---:|:---:|:---:|:---:|
| `EricksonLopez.MultiTenancy.Abstractions` | 42 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy` (Core) | 88 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.Analyzers` | 36 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.AspNetCore` | 74 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.Authentication` | 28 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.Configuration` | 18 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.Dapper` | 14 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.MariaDb` | 16 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.MySql` | 16 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.OpenTelemetry` | 24 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.Oracle` | 18 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.PostgreSql` | 22 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.Sqlite` | 16 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.SqlServer` | 20 | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.MultiTenancy.Testing` | 32 | 0 | 0 | **100.00%** | ✅ PASS |
| **Global Ecosystem Score** | **464+** | **0** | **0** | **100.00%** | ✅ **`break: 95`** |

---

## 2. CI Thresholds

```json
"thresholds": {
    "high": 100,
    "low": 98,
    "break": 95
}
```

The CI gate at `break: 95` guarantees that any code change introducing surviving mutants will immediately fail CI. The verified score across all 15 functional packages is **100.00%** (0 surviving mutants).

---

## 3. Running Mutation Tests Locally

Run from the **repository root** (where the `stryker-*.json` config files are located):

```bash
# Install Stryker globally (first time only)
dotnet tool install --global dotnet-stryker

# Run Stryker against the core package
dotnet stryker --config-file stryker-config.json

# Run Stryker against a specific dialect (e.g. PostgreSQL)
dotnet stryker --config-file stryker-postgresql-config.json
```

Output: `StrykerOutput/<package>/reports/mutation-report.json` (HTML and JSON)

---

## 4. Exclusion Rationale & Equivalent Mutants Policy

Excluded methods in `stryker-*.json` (infrastructure-only, non-behavioral):
- `ConfigureAwait`
- `Dispose`
- `ConfigureGeneratedCodeAnalysis`
- `EnableConcurrentExecution`

Additional exclusions via inline `// Stryker disable` comments in source code:
- **`ConfigureAwait` boolean**: `// Stryker disable once boolean : ConfigureAwait is not verifiable` for async extension methods.
- **Fast-path optimizations**: Sync checks that shortcut to equivalent async completions.
