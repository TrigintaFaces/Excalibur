CI Enforcement Flags

These environment variables control report-only vs enforce modes for the checks that honor them.
Defaults are report-only (unset or false). Each flag below is backed by real, non-vacuous enforcement
code — there is no "appears-enforced but isn't" scaffolding here (see Sprint 849 / kc148w / FR-A2.5).

**Enforced in CI by default** (the workflow sets the flag true):

- ARCH_ENFORCE=true
  - Fails the architecture boundary tests (`tests/architecture/Boundary.Tests`) when a banned
    dependency / boundary violation / package-map drift is detected.
  - Set true in `.github/workflows/quality-gates.yml` (job `architecture`).
- API_ENFORCE=true
  - Fails on Public API baseline diffs (`eng/ci/api-compat-run.ps1`).
  - Set true in `.github/workflows/governance.yml` (job `api-compatibility`).
- PACK_ENFORCE=true
  - Fails packaging validation (metadata/symbols/SourceLink + transitive provider bloat) when issues
    are found (`eng/ci/pack-report.ps1`, `eng/ci/transitive-bloat-report.ps1`).
  - Set true in `.github/workflows/governance.yml` (job `pack-validation`).

**Available opt-in** (honoring check runs report-only in CI; set the flag true to enforce locally or in a job):

- LICENSE_ENFORCE=true
  - Fails license header verification when missing headers are detected (`eng/ci/license-headers-verify.ps1`,
    run report-only in `.github/workflows/security.yml`).
- WARNINGS_ENFORCE=true
  - Fails the warnings scan when warnings are detected (`eng/ci/warnings-scan.ps1`, run report-only in
    `.github/workflows/governance.yml`).

> **Trim/AOT and coverage are enforced by dedicated mechanisms, NOT by an `_ENFORCE` env flag** — do not
> add `TRIM_ENFORCE`/`COVERAGE_ENFORCE` flags (removed in kc148w as un-implemented scaffolding):
> - **Trim/AOT:** `.github/workflows/aot-validation.yml` (AOT suppression/warning baseline audits via
>   `eng/ci/Invoke-Aot*.ps1` — fails on new unapproved suppressions/warnings).
> - **Coverage:** `.github/workflows/coverage.yml` (`coverage-enforce` job, threshold via
>   `COVERAGE_THRESHOLD` + `eng/ci/coverage-threshold.ps1`).

Local runs (report-only opt-out — leave ARCH_ENFORCE unset):
  dotnet test tests/architecture/Boundary.Tests

CI configuration (enforcing — the gating step sets ARCH_ENFORCE=true; see
.github/workflows/quality-gates.yml job "architecture"):
  - name: Run boundary tests
    env:
      ARCH_ENFORCE: "true"
    run: |
      dotnet test tests/architecture/Boundary.Tests --configuration Release
