# Benchmark Results & Competitive Evidence — EricksonLopez.MultiTenancy

> **Benchmark Tool**: BenchmarkDotNet v0.15.8  
> **Host OS**: Linux Ubuntu / Windows 11  
> **Target Frameworks**: .NET 8.0, .NET 9.0, .NET 10.0  
> **Job**: ShortRunJob / DefaultJob (Weekly)  

---

## 1. Competitive Resolution & Allocation Profile

Comparison of `EricksonLopez.MultiTenancy` against conventional ambient dictionary and reflection-based multitenancy resolution models:

| Method | Mean | Error | StdDev | Ratio | Gen0 | Allocated |
|---|---:|---:|---:|---:|---:|---:|
| **`EricksonLopez_StronglyTypedId_Create`** | **1.85 ns** | 0.04 ns | 0.03 ns | **1.00** | **-** | **0 B** |
| `EricksonLopez_ScopedAccessor_GetContext` | 1.12 ns | 0.02 ns | 0.01 ns | 0.61 | - | **0 B** |
| `EricksonLopez_TenantId_StructEquality` | 0.42 ns | 0.01 ns | 0.01 ns | 0.23 | - | **0 B** |
| `Conventional_AmbientAsyncLocal_DictionaryLookup` | 18.94 ns | 0.35 ns | 0.28 ns | 10.24 | 0.0038 | 24 B |
| `Conventional_ConcurrentDictionary_CacheLookup` | 24.15 ns | 0.42 ns | 0.38 ns | 13.05 | 0.0076 | 48 B |
| `Conventional_Regex_SubdomainExtraction` | 142.80 ns | 2.10 ns | 1.85 ns | 77.19 | 0.0229 | 144 B |
| `Conventional_String_Boxing_And_Concatenation` | 88.50 ns | 1.25 ns | 1.10 ns | 47.84 | 0.0305 | 192 B |

---

## 2. Multi-Strategy Resolution & Conflict Detection

Execution timings for single vs multi-strategy resolution chains:

| Method | Mean | Ratio | Gen0 | Allocated |
|---|---:|---:|---:|---:|
| **`SingleStrategy_Header_Resolve`** (Baseline) | **32.4 ns** | **1.00** | **-** | **0 B** |
| `SingleStrategy_Claim_Resolve` | 28.1 ns | 0.87 | - | **0 B** |
| `SingleStrategy_BasePath_Resolve` | 21.6 ns | 0.67 | - | **0 B** |
| `MultiStrategy_Sequential_Fallback` | 34.2 ns | 1.05 | - | **0 B** |
| `MultiStrategy_FailClosed_ConflictDetection` | 48.7 ns | 1.50 | - | **0 B** |

---

## 3. High-Concurrency Tenant Store (1,000 Tenants)

Under `Parallel.For` simulating 10,000 requests across 1,000 tenants:

| Method | Mean | Ratio | Gen0 | Allocated |
|---|---:|---:|---:|---:|
| **`Parallel_GetTenantById_10K`** (Baseline) | **0.82 ms** | **1.00** | **-** | **0 B** |
| `Parallel_GetTenantByIdentifier_10K` | 1.14 ms | 1.39 | - | **0 B** |
| `Concurrent_WhenAll_BatchedLookup` (100 batched) | 12.80 μs | 0.015 | 0.0153 | 96 B |

---

## 4. Relational Database Session & Parameter Injection

Overhead of setting up Row Level Security and Dapper parameters:

| Method | Mean | Ratio | Gen0 | Allocated |
|---|---:|---:|---:|---:|
| **`Dapper_CreateTenantParameters`** (Baseline) | **42.1 ns** | **1.00** | 0.0178 | 112 B |
| `Dapper_WithTenant_Extension` | 46.5 ns | 1.10 | 0.0178 | 112 B |
| `PostgreSql_SetLocal_RlsContext` | 68.4 ns | 1.62 | 0.0204 | 128 B |
| `SqlServer_SetSessionContext` | 64.2 ns | 1.52 | 0.0204 | 128 B |

---

## 5. Architectural Key Takeaways

1. **Zero-Allocation Hot Path**: Strongly-typed `TenantId` creation and context accessor lookups have **zero allocations (0 B)** and execute in under **2 nanoseconds**.
2. **Fail-Closed Security at Minimal Cost**: Multi-strategy conflict detection adds only **16.3 ns** of total overhead while providing complete defense against tenant spoofing and routing ambiguity.
3. **High-Throughput Concurrency**: 10,000 parallel lookups complete in under **1 millisecond** with zero GC allocations.
