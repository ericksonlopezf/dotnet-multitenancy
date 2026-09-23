---
name: Feature Request
about: Suggest an idea or architectural improvement for EricksonLopez.MultiTenancy
title: "[FEAT] "
labels: ["enhancement"]
assignees: ["ericksonlopezf"]
---

### Problem Statement
Is your feature request related to a problem or limitation? Please describe clearly (e.g. *I need to resolve tenants based on a custom token claims structure without allocating delegates*).

### Proposed Solution / API Design
Describe the proposed API addition or change:

```csharp
// Example API proposal
builder.Services.AddCustomTenantStrategy<MyStrategy>();
```

### Architectural Impact & Invariants
- **AOT Compatibility:** Does this require reflection or dynamic code generation?
- **Defense-in-Depth:** How does this affect tenant isolation and data boundaries?
- **Performance / Allocations:** What is the allocation impact on the hot path?

### Alternatives Considered
Describe any alternative solutions or workarounds you have considered.
