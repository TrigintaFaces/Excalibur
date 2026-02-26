# Changelog

All notable changes to Excalibur and Excalibur.Dispatch are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Post-`3.0.0-alpha.18` stabilization and release-candidate hardening.

## [3.0.0-alpha.18] - 2026-02-25

First pre-release of the `3.0.0` line with major architecture, packaging, governance, API, and documentation updates.

### Added

- **Ultra-local dispatch API** via `IDirectLocalDispatcher` with `ValueTask`/`ValueTask<T>` paths for local success scenarios.
- **Direct local context profiles** (`DirectLocalContextInitializationProfile.Lean` and `.Full`) to control local hot-path initialization.
- **Precompiled middleware chain pathing** and no-middleware fast path improvements for dispatch routing.
- **Expanded transport conformance and CI quality gates** (shards, governance checks, conformance-focused validation paths).
- **Documentation-site platform capabilities** including versioning-ready structure and announcement-bar support.
- **Governance artifacts** for framework boundary ownership, package/test mapping, and release checks.

### Changed

- **Major namespace and package structure cleanup** to align with .NET naming/engineering conventions.
- **Dispatcher/local-bus hot paths** refactored with direct-path optimizations and reduced context overhead in local mode.
- **MessageContext behavior** updated with pooled defaults and lazy `Items` usage to reduce hot-path allocations.
- **Endpoint mapping surface** aligned to action semantics (`Dispatch*Action` naming) and stricter cancellation propagation.
- **CI/CD release model** moved to explicit SemVer/tag-driven release workflow (pre-release and stable support).
- **Public API governance** strengthened:
  - baseline pair enforcement for shipping projects,
  - `PublicAPI.Unshipped.txt` pending entry blocking in governance audit.

### Fixed

- **HTTP cancellation propagation** from ASP.NET endpoint routes through dispatch execution paths.
- **Governance script compatibility** for Windows PowerShell environments lacking `ConvertFrom-Json -Depth`.
- **Multiple test and conformance gaps** identified during core refactor/performance workstreams.

### Removed

- Legacy/internal comparison and workflow assumptions that were incompatible with the new `3.0.0` architecture and governance model.
- Reliance on prior versioning workflow conventions from older release automation.

### Breaking Changes

- Namespace and package layout changes require source updates for existing consumers.
- Endpoint extension methods renamed from `Dispatch*Message` to `Dispatch*Action`.
- Stricter cancellation-token handling expectations for HTTP-initiated dispatch flows.
- CI/release expectations changed to the new governance and sharded validation model.

### Migration Notes

- Review migration docs before upgrading:
  - `docs/migration/1.0-migration.md`
  - `docs/migration/package-renames-and-deprecations.md`
- Update code references for renamed namespaces/packages.
- Update endpoint route extension method usage to `Dispatch*Action`.
- If adopting pre-release packages, pin to `3.0.0-alpha.*` explicitly.

### Documentation

- Contributor docs and docs-site were updated for:
  - architecture boundary clarity (Dispatch vs Excalibur),
  - performance benchmark interpretation and fairness notes,
  - release/governance process updates for v3.

## Legacy Releases

Releases prior to the `3.0.0` line (including `2.2.x`) are tracked in GitHub releases/history for the previous public baseline.

[Unreleased]: https://github.com/TrigintaFaces/Excalibur/compare/v3.0.0-alpha.18...HEAD
[3.0.0-alpha.18]: https://github.com/TrigintaFaces/Excalibur/releases/tag/v3.0.0-alpha.18
