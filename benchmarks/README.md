# Benchmarks Directory

This folder contains benchmark projects, baseline artifacts, and benchmark tooling scripts.

## Canonical Docs

- Consumer-facing comparison page: `docs-site/docs/performance/competitor-comparison.md`
- Dispatch benchmark suite guide: `benchmarks/Excalibur.Dispatch.Benchmarks/README.md`

## Canonical Runner

```bash
pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -ArtifactsPath ./BenchmarkDotNet.Artifacts.FullRefresh-20260219
```

This produces per-class logs and summary files in `BenchmarkDotNet.Artifacts.FullRefresh-20260219/results/`.

## Policy

- Do not commit `BenchmarkDotNet.Artifacts*` directories.
- Update docs only from a dated, reproducible matrix run.
- Keep MediatR-local parity and transport comparison interpretations separate.
