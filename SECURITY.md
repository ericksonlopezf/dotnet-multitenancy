# Security Policy

## Supported Versions

We provide security updates and patches for the current major version.

| Version | Supported Target Frameworks | Security Support Status |
| :--- | :--- | :--- |
| **1.0.x** | `.NET 8.0`, `.NET 9.0` | :white_check_mark: Supported (Current) |
| **< 1.0.0** | All | :x: Unsupported (Pre-release) |

---

## Reporting a Vulnerability

We take the security of `EricksonLopez.MultiTenancy` seriously. If you discover a vulnerability or potential cross-tenant data leakage vector, please follow these steps:

1. **Do NOT disclose the issue publicly** (e.g., via GitHub Issues, Discussions, or social media).
2. Send an email to [ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com) with the subject: `[SECURITY] Potential vulnerability in EricksonLopez.MultiTenancy`.
3. Include in your report:
   - Package name and version.
   - Target framework and runtime environment.
   - Minimal reproduction project or proof-of-concept code.
   - Detailed impact analysis (e.g., tenant spoofing, async context bleed, connection pool leakage).
4. You will receive an initial response acknowledging receipt within 48 hours.
5. Once verified, a patched release will be published and credited in our security advisories.

---

## Supply Chain Security

The `EricksonLopez.MultiTenancy` ecosystem implements modern DevSecOps supply chain security standards:

- **Sigstore Provenance Attestation:** All published packages include SLSA-compliant build provenance attestations generated via `actions/attest-build-provenance` in GitHub Actions.
- **NuGet Trusted Publishing (OIDC):** Package publishing uses passwordless OpenID Connect (OIDC) authentication directly with NuGet.org, eliminating long-lived API keys.
- **Strong Name Signing:** Assemblies are signed with a dedicated Strong Name Key (`.snk`) to ensure binary integrity and identity.
- **Central Package Management (CPM):** All NuGet dependency versions are centrally locked in `Directory.Packages.props`.
- **Deterministic & SourceLink Builds:** Packages include SourceLink metadata, embedded symbol packages (`.snupkg`), and continuous integration flags for verifiable reproduction.

---

## Security Boundaries and Invariants

When using `EricksonLopez.MultiTenancy`, understand the following security model boundaries:

1. **Application Context Is Not Isolation:** Resolving a tenant in .NET does not guarantee isolation at the data tier. Always configure database-level isolation (e.g., PostgreSQL Row Level Security with `SET LOCAL`).
2. **Claims Precedence Policy:** Authenticated JWT claims always take precedence over client-provided headers (`X-Tenant-ID`) or route parameters. Conflicting tenant identifiers trigger a fail-closed exception (ADR-008).
3. **Async Context Scoping:** `ITenantContextAccessor` is registered as **Scoped** to prevent ambient state leakage across threads and recycled connection pools.
4. **No Implicit SQL Rewriting:** Queries must explicitly filter by `tenant_id` alongside database RLS policies (ADR-007).
