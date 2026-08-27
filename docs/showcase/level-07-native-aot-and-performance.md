# Level 07: Native AOT Compilation & High-Throughput Optimization

`EricksonLopez.MultiTenancy` is built Native AOT-first (`IsAotCompatible=true`, `EnableTrimAnalyzer=true`).

## Key AOT Principles

1. **Zero Unannotated Reflection:** No runtime type emission, `MakeGenericType`, or dynamic expression trees in resolution paths.
2. **Compile-Time JSON Serialization:** Uses source-generated `JsonConverter` for `TenantId` (`Serialization.TenantIdJsonConverter`).
3. **Compiler Trimming Guarantees:** Zero warnings under `TreatWarningsAsErrors=true` and `WarningLevel=5`.

## Publishing as Native AOT

```bash
dotnet publish MyApp.csproj -c Release -r linux-x64 --self-contained -p:PublishAot=true
```
