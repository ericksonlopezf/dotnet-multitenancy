# Benchmark Plan & Performance Budgets — EricksonLopez.MultiTenancy

> **Tool**: BenchmarkDotNet v0.15.8  
> **Runtimes**: .NET 8.0, .NET 9.0, .NET 10.0  
> **Hardware Target**: x64 Linux / Windows (Bare metal & GitHub Actions Ubuntu Runners)  
> **Allocation Profile**: Zero unnecessary heap allocations on resolution and query hot paths  

---

## 1. Executive Summary & Philosophy

In modern multi-tenant enterprise architectures, tenant resolution occurs on **every single inbound HTTP request, message bus consumption, and database query**. Any non-zero allocation, thread synchronization lock, or dynamic reflection lookup compounded across 50,000 req/sec directly manifests as:
1. Increased GC Gen0/Gen1 pause times.
2. Latency spikes (p99 degradation).
3. Cloud compute inflation.

`EricksonLopez.MultiTenancy` enforces a **Zero-Tolerance Latency & Allocation Budget**.

---

## 2. Performance Budgets

| Operation | Max Allowed Latency (Mean) | Max Allowed Allocation | Target Metric |
|---|---|---|---|
| `TenantId.Create(Guid)` | $\le 3.5\text{ ns}$ | **0 B** | Instant struct value wrapping |
| `TenantId.TryCreate(string, out id)` | $\le 18.0\text{ ns}$ | **0 B** | Zero allocation UUID parser |
| `TenantId == TenantId` (Equality) | $\le 0.5\text{ ns}$ | **0 B** | Direct 128-bit SIMD comparison |
| `ITenantContextAccessor.TenantContext` | $\le 2.0\text{ ns}$ | **0 B** | Direct field dereference |
| `HeaderTenantResolutionStrategy` | $\le 45.0\text{ ns}$ | $\le 32\text{ B}$ | String extraction from header dictionary |
| `InMemoryTenantStore.GetTenantAsync` (Hit) | $\le 35.0\text{ ns}$ | **0 B** | Pre-allocated `ValueTask` return |
| `Dapper.CreateTenantParameters()` | $\le 60.0\text{ ns}$ | $\le 112\text{ B}$ | Single parameter bag allocation |
| `PostgreSql.SetTenantRlsContextAsync` | $\le 85.0\text{ ns}$ | $\le 128\text{ B}$ | Transaction-scoped `SET LOCAL` execution |

---

## 3. Benchmark Suites Inventory

### 1. `TenantResolutionBenchmarks`
- `TenantId_Create_FromGuid`: Measures struct instantiation overhead.
- `TenantId_TryCreate_FromString`: Measures string-to-Guid parsing without boxing.
- `TenantId_Equality`: Measures struct comparison efficiency.
- `ContextAccessor_GetContext`: Measures scoped context accessor retrieval.

### 2. `MultiStrategyResolutionBenchmarks`
- `SingleStrategy_Header_Resolve`: Single header-based resolution.
- `SingleStrategy_Claim_Resolve`: Single claim-based extraction.
- `SingleStrategy_BasePath_Resolve`: URL base path segment parser.
- `MultiStrategy_Sequential_Fallback`: Multi-source fallback evaluation.
- `MultiStrategy_FailClosed_ConflictDetection`: Conflict detection across multiple sources with fail-closed security guarantees.

### 3. `ConcurrentTenantStoreBenchmarks`
- `Parallel_GetTenantById_10K`: 10,000 multi-threaded concurrent lookups by `TenantId`.
- `Parallel_GetTenantByIdentifier_10K`: 10,000 multi-threaded concurrent lookups by string identifier.
- `Concurrent_WhenAll_BatchedLookup`: Asynchronous batched lookup under `Task.WhenAll`.

### 4. `RlsSessionContextBenchmarks`
- `Dapper_CreateTenantParameters`: Direct Dapper dynamic parameter creation.
- `Dapper_WithTenant_Extension`: Fluently chaining tenant parameters to existing bags.
- `PostgreSql_SetLocal_RlsContext`: PostgreSQL `SET LOCAL app.current_tenant_id` session setup.
- `SqlServer_SetSessionContext`: SQL Server `sp_set_session_context` parameterization.

### 5. `CompetitiveResolutionBenchmarks`
- Compares EricksonLopez.MultiTenancy struct/scoped models against legacy reflection-based, ambient `AsyncLocal<Dictionary>`, and regex host extraction patterns.

---

## 4. Automated CI Regression Gate

Benchmark regression verification is enforced automatically on all Pull Requests via `.github/workflows/benchmark-regression-gate.yml`. Any pull request exceeding the `+10%` regression threshold compared to the baseline captured on `main` fails the build automatically.
