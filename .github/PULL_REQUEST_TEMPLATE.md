## Description
Briefly describe the changes introduced by this pull request, the problem it solves, or the feature it implements.

---

## Affected Packages
Select all packages modified in this PR:

- [ ] `EricksonLopez.MultiTenancy.Abstractions`
- [ ] `EricksonLopez.MultiTenancy` (Core)
- [ ] `EricksonLopez.MultiTenancy.Analyzers`
- [ ] `EricksonLopez.MultiTenancy.AspNetCore`
- [ ] `EricksonLopez.MultiTenancy.Authentication`
- [ ] `EricksonLopez.MultiTenancy.Configuration`
- [ ] `EricksonLopez.MultiTenancy.Dapper`
- [ ] `EricksonLopez.MultiTenancy.MariaDb`
- [ ] `EricksonLopez.MultiTenancy.MySql`
- [ ] `EricksonLopez.MultiTenancy.OpenTelemetry`
- [ ] `EricksonLopez.MultiTenancy.Oracle`
- [ ] `EricksonLopez.MultiTenancy.PostgreSql`
- [ ] `EricksonLopez.MultiTenancy.Sqlite`
- [ ] `EricksonLopez.MultiTenancy.SqlServer`
- [ ] `EricksonLopez.MultiTenancy.Testing`
- [ ] `EricksonLopez.MultiTenancy.Showcase` (Sample)
- [ ] Documentation / CI/CD Pipelines

---

## Quality Gate Checklist
Before submitting, please ensure:

- [ ] Solution compiles cleanly with zero warnings (`dotnet build EricksonLopez.MultiTenancy.slnx --configuration Release`).
- [ ] All unit, integration, and architecture tests pass (`dotnet test EricksonLopez.MultiTenancy.slnx`).
- [ ] Line coverage remains at 100% across modified components.
- [ ] Stryker mutation testing passes with score ≥ 95% (break threshold).
- [ ] Native AOT and Trimming compatibility is preserved (`<IsAotCompatible>true</IsAotCompatible>`).
- [ ] XML documentation is provided for all new or modified public API members.
- [ ] Architectural Decision Records (ADRs) are added or updated if modifying core contracts.
- [ ] Commit messages follow the [Conventional Commits](https://www.conventionalcommits.org/) specification.
