# Capability Migration Map

This map records capability placements that were consolidated during architecture remediation so maintainers have a single migration reference.

Use this together with:

- `docs/architecture/dispatch-excalibur-boundary.md`
- `docs/architecture/capability-ownership-matrix.md`
- `eng/governance/framework-governance.json`

## Migration Table

| Capability Area | Previous Placement | Canonical Placement | Action |
|---|---|---|---|
| Local dispatch + message pipeline | Mixed guidance across Dispatch/Excalibur docs | `Excalibur.Dispatch`, `Excalibur.Dispatch.Abstractions` | Keep implementation in Dispatch only; Excalibur composes |
| ASP.NET Core minimal bridge helpers | Hosting discussions mixed with rich host guidance | `Excalibur.Dispatch.Hosting.AspNetCore` | Keep bridge thin and feature-limited |
| CQRS aggregate orchestration | Mentioned as both Dispatch and Excalibur in legacy docs | `Excalibur.Domain`, `Excalibur.Application` | Move ownership narrative to Excalibur only |
| Event sourcing + outbox + saga orchestration | Overlapping references in contributor docs | `Excalibur.EventSourcing.*`, `Excalibur.Outbox.*`, `Excalibur.Saga.*` | Excalibur-only ownership; Dispatch remains transport/pipeline core |
| Compliance providers | Ambiguous wording around Dispatch ownership | `Excalibur.Compliance.*` + Dispatch abstractions/hooks | Dispatch keeps contracts/hooks only; providers stay in Excalibur |
| Provider naming (`Postgres`/`PostgreSql`) | Inconsistent package naming in docs and references | `Postgres` canonical | Enforce canonical naming via governance matrix + CI |

## Migration Rules

1. If a feature can run without CQRS/domain persistence, it belongs in Dispatch.
2. If a feature is opinionated for domain workflows or host orchestration, it belongs in Excalibur.
3. Excalibur wrappers compose Dispatch APIs and must not fork equivalent runtime implementations.
4. Any new ownership change must update:
   - this migration map,
   - `framework-governance.json`,
   - boundary docs and related tests.

## CI Enforcement

The governance gate validates ownership and docs parity:

```bash
pwsh eng/ci/validate-framework-governance.ps1 -Mode Governance -Enforce:$true
```

This is release-blocking.
