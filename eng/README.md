# Engineering Scripts and CI Utilities

This folder contains all build, validation, and CI scripts for the repository. It follows the Microsoft `eng/` convention (matching dotnet/runtime and other .NET repos).

## Directory Structure

| Path | Purpose |
|------|---------|
| `eng/*.ps1` | Top-level build and validation scripts |
| `eng/ci/` | CI-only pipeline scripts |
| `eng/tools/` | Index reconciliation and sync utilities |
| `eng/compliance/` | Compliance evidence collection and reporting |
| `eng/hooks/` | Git hooks |
| `eng/api/` | Public API baselines for compatibility |
| `eng/banned/` | Banned API symbol lists |
| `eng/license/` | License header templates |

## Key Scripts

### Build & Pack

| Script | Purpose |
|--------|---------|
| `build.ps1` | Master build orchestrator |
| `pack-local.ps1` | Pack NuGet packages to local feed (`artifacts/_packages/`) |
| `smoke-test-packages.ps1` | Packaging smoke tests |

### Validation

| Script | Purpose |
|--------|---------|
| `validate-solution.ps1` | Validate manifest / filesystem / solution consistency |
| `validate-samples.ps1` | Validate certified samples (build + smoke profiles from governance matrix) |
| `validate-package-composition.ps1` | End-to-end PackageReference validation |
| `validate-architecture-boundaries.ps1` | Architecture boundary enforcement |
| `validate-serializer-policy.ps1` | Serializer policy compliance |
| `validate-aot-trim.ps1` | AOT/trimming compatibility |
| `validate-api-compatibility.ps1` | API compatibility checks |
| `verify-banned-apis.ps1` | Banned API usage checks |
| `verify-layout.ps1` | Repository layout verification |
| `verify-providers.ps1` | Provider registration verification |

### Audit & Reporting

| Script | Purpose |
|--------|---------|
| `audit-warnings.ps1` | Build warning audit for shipping code |
| `audit-package-metadata.ps1` | Package metadata completeness |
| `inventory-projects.ps1` | Regenerate `project-manifest.yaml` |
| `add-missing-to-solution.ps1` | Bulk-add missing projects to the solution |
| `scan-vulnerabilities.ps1` | Dependency vulnerability scanning |

### Testing

| Script | Purpose |
|--------|---------|
| `run-coverage.ps1` | Code coverage collection (Windows) |
| `run-coverage.sh` | Code coverage collection (Linux/macOS) |
| `test-templates.ps1` | `dotnet new` template validation |
| `check-benchmark-regression.ps1` | Benchmark regression detection |
| `run-comparative-benchmarks.ps1` | Comparative benchmark suite (MediatR/Wolverine/MassTransit) with quiet framework logs by default |
| `run-benchmark-matrix.ps1` | Comparative + diagnostics class matrix, per-class logs, and summary artifact generation |
| `validate-performance-gates.ps1` | Enforce MediatR local parity and transport comparison benchmark thresholds |

### Compliance

| Script | Purpose |
|--------|---------|
| `compliance/collect-evidence.ps1` | Collect compliance evidence (Windows) |
| `compliance/collect-evidence.sh` | Collect compliance evidence (Linux/macOS) |
| `compliance/export-audit-samples.sh` | Export audit log samples |
| `compliance/generate-evidence-package.ps1` | Generate evidence package |
| `compliance/generate-ropa-template.sh` | Generate ROPA template |

## CI Integration

CI workflows reference scripts from this directory. See `.github/workflows/ci.yml` and `.github/workflows/quality-gates.yml` for usage.
