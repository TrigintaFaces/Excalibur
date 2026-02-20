Public API Baselines

Purpose
- Track shipped public APIs per package to detect breaking changes over time.

Initial target packages (incremental):
- Excalibur.Dispatch.Abstractions → eng/api/Excalibur.Dispatch.Abstractions.PublicAPI.Shipped.txt
- Excalibur.Dispatch (current assembly name) → eng/api/Excalibur.Dispatch.PublicAPI.Shipped.txt
- Excalibur.Dispatch.Patterns → eng/api/Excalibur.Dispatch.Patterns.PublicAPI.Shipped.txt

Workflow (report-only → enforce):
1) Generate initial baselines locally using dotnet format or PublicApiAnalyzers tooling.
2) Commit the baseline files under eng/api/.
3) Add ApiCompat check in CI (report-only by default; controlled via API_ENFORCE).
4) After cleanup, flip API_ENFORCE=true to block breaking changes.

Notes
- Keep baselines current when intentional breaking changes are approved (SemVer bump).
- Prefer minimal public surface in core packages; keep abstractions stable.

