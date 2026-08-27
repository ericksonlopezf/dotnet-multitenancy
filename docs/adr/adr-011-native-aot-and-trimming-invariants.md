# ADR-011: Native AOT and Trimming Invariants

## Status
Accepted

## Context
Cloud-native workloads increasingly rely on **Native AOT (`PublishAot=true`)** and aggressive assembly trimming (`PublishTrimmed=true`) to achieve sub-10ms cold starts and tiny container footprints (<30MB).

Legacy multi-tenancy frameworks rely heavily on unconstrained runtime reflection (`System.Reflection`, `Type.GetType`, `MakeGenericType`), runtime IL emission (`Reflection.Emit`), or dynamic configuration binders that fail under Native AOT with fatal trimming warnings (`IL2026`, `IL3050`).

## Decision
We enforce strict **Native AOT-First Invariants** across all `EricksonLopez.MultiTenancy` packages:
1. `IsAotCompatible=true` and `EnableTrimAnalyzer=true` are enforced across all source projects.
2. `TreatWarningsAsErrors=true` ensures any Roslyn trim or AOT diagnostic halts compilation immediately.
3. Zero usage of `System.Reflection.Emit`, `MakeGenericType`, `Activator.CreateInstance(Type)`, or dynamic expression compilation on execution hot paths.
4. JSON serialization in store implementations (e.g., PostgreSQL JSONB properties) must utilize compile-time `System.Text.Json.Serialization.JsonSerializerContext` source generation.
5. Automated CI validation via `.github/workflows/aot-smoke-test.yml` continuously compiles a native ELF binary under `PublishAot=true` and executes smoke test assertions.

## Consequences
### Positive
- Production-grade zero-warning Native AOT deployment.
- Predictable execution without JIT warm-up latency spikes.
- Absolute trimming safety without runtime reflection breakage.

### Negative
- Dynamic runtime type loading without compile-time registration is strictly impossible by design.
