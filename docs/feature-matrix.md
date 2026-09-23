# Feature Status Matrix — EricksonLopez.MultiTenancy

> **Audit Version:** 2.0.0  
> **Status:** Active

---

## 1. Core Tenant Identity (`EricksonLopez.MultiTenancy.Abstractions`)

| Feature ID | Feature Description | Status | Security Value | Architectural Value | Native AOT | Decision |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **C-01** | `TenantId` strongly-typed struct | Implemented | 🔴 Critical | High | ✅ Safe | **REDESIGN** |
| **C-02** | `TenantId` Guid-backed | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **C-03** | `TenantId.Empty` sentinel | Implemented | 🟠 High | High | ✅ Safe | **KEEP** |
| **C-04** | `TenantId.NewId()` factory | Implemented | 🟢 Low | Medium | ✅ Safe | **KEEP** |
| **C-05** | `TenantId.TryCreate()` safe parsing | Implemented | 🟠 High | High | ✅ Safe | **KEEP** |
| **C-06** | `ITenantInfo` metadata contract | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **C-07** | `TenantInfo` default record | Implemented | 🟡 Medium | High | ✅ Safe | **KEEP** |
| **C-08** | `ITenantInfo.ConnectionString` optional | Implemented | 🟡 Medium | High | ✅ Safe | **KEEP** |

---

## 2. Tenant Context & Lifetime (`EricksonLopez.MultiTenancy`)

| Feature ID | Feature Description | Status | Security Value | Architectural Value | Native AOT | Decision |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **T-01** | `ITenantContext` immutable contract | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **T-02** | `TenantContext<TTenant>` generic | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **T-03** | `TenantContext.Empty` sentinel | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **T-04** | `ITenantContextAccessor` write-once | Implemented | 🔴 Critical | High | ✅ Safe | **REDESIGN** |
| **T-05** | `ScopedTenantContextAccessor` | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **T-06** | Static `AsyncLocal` accessor | Rejected | 🔴 Critical | None | ❌ Risk | **REMOVE (ADR-002)** |
| **T-07** | `TenantResolutionSource` audit enum | Implemented | 🟠 High | Medium | ✅ Safe | **KEEP** |
| **T-08** | `ITenantContext.RequiredTenant` | Implemented | 🟠 High | High | ✅ Safe | **KEEP** |

---

## 3. Resolution Strategies (`EricksonLopez.MultiTenancy.AspNetCore`)

| Feature ID | Feature Description | Status | Security Value | Architectural Value | Native AOT | Decision |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **R-01** | `ITenantResolutionStrategy` contract | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **R-02** | `ClaimTenantResolutionStrategy` (JWT) | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **R-03** | `HostNameTenantResolutionStrategy` | Implemented | 🟠 High | Medium | ✅ Safe | **KEEP** |
| **R-04** | `RouteTenantResolutionStrategy` | Implemented | 🟡 Medium | Medium | ✅ Safe | **KEEP** |
| **R-05** | `BasePathTenantResolutionStrategy` | Implemented | 🟡 Medium | Medium | ✅ Safe | **KEEP** |
| **R-06** | `HeaderTenantResolutionStrategy` (Opt-in) | Implemented | 🟠 High | Medium | ✅ Safe | **KEEP (ADR-003)** |
| **R-07** | Claims-first precedence & conflict check | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP (ADR-008)** |
| **R-08** | `TenantResolutionMiddleware` | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **R-09** | `RequireTenantFilter` (`.RequireTenant()`) | Implemented | 🟠 High | Medium | ✅ Safe | **KEEP** |
| **R-10** | `DelegateTenantResolutionStrategy` | Implemented | 🟢 Low | Medium | ✅ Safe | **KEEP** |
| **R-11** | `StaticTenantResolutionStrategy` | Implemented | 🟢 Low | Medium | ✅ Safe | **KEEP** |

---

## 4. Tenant Stores (`Core`, `Configuration`, `PostgreSql`)

| Feature ID | Feature Description | Status | Security Value | Architectural Value | Native AOT | Decision |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **S-01** | `ITenantStore` / `ITenantLookupStore` | Implemented | 🟠 High | High | ✅ Safe | **KEEP** |
| **S-02** | `InMemoryTenantStore<TTenant>` | Implemented | 🟢 Low | High | ✅ Safe | **KEEP** |
| **S-03** | `CachedTenantStore<TTenant>` (`IMemoryCache`) | Implemented | 🟡 Medium | High | ✅ Safe | **KEEP** |
| **S-04** | `HttpRemoteTenantStore<TTenant>` | Implemented | 🟠 High | High | ✅ Safe | **KEEP** |
| **S-05** | `ConfigurationTenantStore<TTenant>` | Implemented | 🟠 High | High | ✅ Safe | **KEEP** |
| **S-06** | `PostgreSqlTenantStore<TTenant>` | Implemented | 🟠 High | High | ✅ Safe | **KEEP** |
| **S-07** | `MultiTenancyHealthCheck` | Implemented | 🟢 Low | Medium | ✅ Safe | **KEEP** |

---

## 5. Background Jobs (`EricksonLopez.MultiTenancy`)

| Feature ID | Feature Description | Status | Security Value | Architectural Value | Native AOT | Decision |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **B-01** | `ITenantScopeFactory` contract | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **B-02** | `DefaultTenantScopeFactory` | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **B-03** | `ITenantScope` disposable scope | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |

---

## 6. Database Dialects (`PostgreSql`, `SqlServer`, `MySql`, `MariaDb`, `Oracle`, `Sqlite`)

| Feature ID | Feature Description | Status | Security Value | Architectural Value | Native AOT | Decision |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **D-01** | PostgreSQL RLS via `SET LOCAL` | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP (ADR-009)** |
| **D-02** | SQL Server `SESSION_CONTEXT` | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **D-03** | MySQL Session Variables (`@app_tenant_id`) | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **D-04** | MariaDB Session Variables (`@app_tenant_id`) | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **D-05** | Oracle VPD (`DBMS_SESSION.SET_IDENTIFIER`) | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **D-06** | SQLite DB-Per-Tenant Connection Factory | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |

---

## 7. Observability & Quality (`OpenTelemetry`, `Analyzers`, `Testing`)

| Feature ID | Feature Description | Status | Security Value | Architectural Value | Native AOT | Decision |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **O-01** | `TenantActivitySource` & W3C Baggage | Implemented | 🟡 Medium | High | ✅ Safe | **KEEP** |
| **O-02** | `TenantMetrics` System.Diagnostics.Metrics | Implemented | 🟢 Low | Medium | ✅ Safe | **KEEP** |
| **A-01** | Roslyn Analyzer `ELMT001` (Static Field) | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **A-02** | Roslyn Analyzer `ELMT002` (Singleton Captivity) | Implemented | 🔴 Critical | High | ✅ Safe | **KEEP** |
| **A-03** | Roslyn Analyzer `ELMT003` (Dapper Without Tenant) | Implemented | 🟠 High | High | ✅ Safe | **KEEP** |
| **K-01** | `FakeTenantStore` / `FakeTenantResolutionStrategy` | Implemented | 🟢 Low | High | ✅ Safe | **KEEP** |
| **K-02** | `TenantContextBuilder` / `TestTenantContext` | Implemented | 🟢 Low | High | ✅ Safe | **KEEP** |
| **K-03** | `FakeDbInfrastructure` in-memory test doubles | Implemented | 🟢 Low | High | ✅ Safe | **KEEP** |

---

## 8. Package Compatibility Matrix

Target framework support per package, derived from `.csproj` `<TargetFrameworks>` elements.

| Package | `net8.0` | `net9.0` | `netstandard2.0` | NativeAOT | Trimming |
| :--- | :---: | :---: | :---: | :---: | :---: |
| `EricksonLopez.MultiTenancy.Abstractions` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy` (Core) | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.Analyzers` | — | — | ✅ | N/A | N/A |
| `EricksonLopez.MultiTenancy.AspNetCore` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.Authentication` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.Configuration` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.Dapper` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.PostgreSql` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.SqlServer` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.MySql` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.MariaDb` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.Oracle` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.Sqlite` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.OpenTelemetry` | ✅ | ✅ | — | ✅ | ✅ |
| `EricksonLopez.MultiTenancy.Testing` | ✅ | ✅ | — | ✅ | ✅ |

> NativeAOT and Trimming compatibility verified by `<IsAotCompatible>true</IsAotCompatible>` and `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>` in `Directory.Build.props`, and confirmed by `EricksonLopez.MultiTenancy.AotSmokeTest`.

---

## 9. Central Package Management — Pinned Dependency Versions

All NuGet dependency versions are centrally locked in [`Directory.Packages.props`](../Directory.Packages.props). Direct production dependencies per layer:

### Production Dependencies

| Package | Version | Consumer Layer |
| :--- | :--- | :--- |
| `EricksonLopez.Result` | `2.0.0` | Abstractions (L0) |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `8.0.2` | Core (L1) |
| `Microsoft.Extensions.DependencyInjection` | `8.0.1` | Core (L1) |
| `Microsoft.Extensions.Options` | `8.0.2` | Core (L1), Configuration (L3) |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `8.0.0` | Configuration (L3) |
| `Microsoft.Extensions.Configuration.Abstractions` | `8.0.0` | Configuration (L3) |
| `Microsoft.Extensions.Configuration.Binder` | `8.0.2` | Configuration (L3) |
| `Microsoft.Extensions.Caching.Abstractions` | `8.0.0` | Core (L1) |
| `Microsoft.Extensions.Caching.Memory` | `8.0.0` | Core (L1) |
| `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` | `9.0.2` | Core (L1) |
| `Microsoft.AspNetCore.Http.Abstractions` | `2.3.0` | AspNetCore (L3) |
| `Microsoft.AspNetCore.Http` | `2.3.0` | AspNetCore (L3) |
| `OpenTelemetry.Api` | `1.10.0` | OpenTelemetry (L6) |
| `Dapper` | `2.1.35` | Dapper (L4) |
| `Npgsql` | `9.0.3` | PostgreSql (L5) |
| `Microsoft.Data.SqlClient` | `5.2.2` | SqlServer (L5) |
| `MySqlConnector` | `2.4.0` | MySql (L5), MariaDb (L5) |
| `Oracle.ManagedDataAccess.Core` | `23.7.0` | Oracle (L5) |
| `Microsoft.Data.Sqlite` | `9.0.2` | Sqlite (L5) |
| `SQLitePCLRaw.bundle_e_sqlite3` | `2.1.11` | Sqlite (L5) |
| `SQLitePCLRaw.core` | `2.1.11` | Sqlite (L5) |

### Test & Tooling Dependencies

| Package | Version | Usage |
| :--- | :--- | :--- |
| `xunit` | `2.9.3` | Test framework |
| `xunit.runner.visualstudio` | `2.8.2` | VS Test runner |
| `Microsoft.NET.Test.Sdk` | `17.14.1` | MSBuild test adapter |
| `AwesomeAssertions` | `9.5.0` | Fluent assertion library |
| `NSubstitute` | `5.3.0` | Mocking framework |
| `coverlet.msbuild` | `10.0.1` | Code coverage collection |
| `coverlet.collector` | `6.0.4` | Code coverage collector |
| `TngTech.ArchUnitNET.xUnit` | `0.13.3` | Architecture tests |
| `NetArchTest.Rules` | `1.3.2` | Architecture enforcement rules |
| `Microsoft.CodeAnalysis.CSharp` | `4.10.0` | Roslyn Analyzer tests |
| `Microsoft.CodeAnalysis.CSharp.Workspaces` | `4.10.0` | Roslyn Analyzer tests |
| `Microsoft.CodeAnalysis.Analyzers` | `3.3.4` | Roslyn meta-analyzers |
