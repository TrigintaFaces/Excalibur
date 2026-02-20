CI Enforcement Flags

These environment variables control report-only vs enforce modes in CI. Defaults are report-only (unset or false).

- ARCH_ENFORCE=true
  - Fails ArchitectureTests when banned dependencies are detected.
- API_ENFORCE=true
  - Fails on Public API baseline diffs (when wired).
- TRIM_ENFORCE=true
  - Fails on trim/AOT warnings (when wired).
- COVERAGE_ENFORCE=true
  - Fails when coverage threshold is not met (when wired).
- PACK_ENFORCE=true
  - Fails packaging validation (metadata/symbols/SourceLink) when issues are found (when wired).

Local runs (report-only):
  dotnet test tests/ArchitectureTests

CI suggestion (GitHub Actions example):
  - name: Architecture checks (report-only)
    run: |
      $env:ARCH_ENFORCE='false'
      dotnet test tests/ArchitectureTests

