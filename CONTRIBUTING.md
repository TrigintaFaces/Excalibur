# Contributing to Excalibur

Thank you for your interest in contributing to Excalibur! This document provides guidelines and information for contributors.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Development Environment Setup](#development-environment-setup)
- [Building the Project](#building-the-project)
- [Running Tests](#running-tests)
- [Project Governance](#project-governance)
- [Code Style Guidelines](#code-style-guidelines)
- [Pull Request Process](#pull-request-process)
- [Issue Triage Labels](#issue-triage-labels)
- [Getting Help](#getting-help)
- [Releasing](#releasing)

---

## Code of Conduct

This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers.

---

## Development Environment Setup

### Prerequisites

| Requirement | Version | Purpose |
|-------------|---------|---------|
| .NET SDK | 10.0+ | Building and running the project |
| Visual Studio 2022 / Rider | Latest | IDE (optional but recommended) |
| Docker Desktop | Latest | Running TestContainers for integration tests |
| Git | 2.x+ | Version control |
| PowerShell | 7.x+ | Running build scripts |

### Quick Start

```bash
# 1. Clone the repository
git clone https://github.com/TrigintaFaces/Excalibur.git
cd Excalibur.Dispatch

# 2. Restore dependencies
dotnet restore

# 3. Build the solution
dotnet build

# 4. Run tests (ensure Docker is running for integration tests)
dotnet test
```

### IDE Setup

**Visual Studio 2022:**
- Open `Excalibur.sln`
- Ensure .NET 10 SDK is installed
- EditorConfig is automatically applied

**JetBrains Rider:**
- Open the solution folder
- Enable EditorConfig in Settings → Editor → Code Style

---

## Building the Project

```bash
# Development build
dotnet build

# Release build
dotnet build -c Release

# Build specific project
dotnet build src/Dispatch/Excalibur.Dispatch/Excalibur.Dispatch.csproj
```

### Package Reference Modes (AD-327-1, AD-327-2)

The repository supports two build modes:

| Mode | Default | Use Case |
|------|---------|----------|
| **ProjectReference** | Yes | Development (fast iteration, debugging) |
| **PackageReference** | No | CI validation (package composition testing) |

**Development Mode (Default):**

```bash
# Fast builds using ProjectReference - use for everyday development
dotnet build Excalibur.sln
dotnet build eng/ci/shards/ShippingOnly.slnf
```

**CI Validation Mode:**

```bash
# 1. Pack Dispatch projects to local NuGet feed
pwsh eng/pack-local.ps1

# 2. Build with PackageReference to validate package composition
dotnet build eng/ci/shards/ShippingOnly.slnf -p:UsePackageReferences=true
```

The local feed is created at `artifacts/_packages/`. This validates that:
- Package metadata is correct
- Dependencies resolve properly
- No circular package references exist

**Multi-Targeting:**

All shipping packages target `net8.0`, `net9.0`, and `net10.0`:

```bash
# Build for specific framework
dotnet build -f net8.0
dotnet build -f net9.0
dotnet build -f net10.0

# Pack produces multi-TFM packages
dotnet pack src/Dispatch/Excalibur.Dispatch/Excalibur.Dispatch.csproj
# Creates: Excalibur.Dispatch.1.0.0.nupkg with lib/net8.0/, lib/net9.0/, and lib/net10.0/
```

### Local Package Validation (AD-328-1, AD-328-2)

Before pushing changes, you can validate package composition locally using the same process as CI:

```bash
# Full validation (build, pack, test composition)
pwsh eng/validate-package-composition.ps1

# Skip build if already built
pwsh eng/validate-package-composition.ps1 -SkipBuild

# Use a specific version
pwsh eng/validate-package-composition.ps1 -Version 0.2.0-test
```

This script:
1. Builds Dispatch projects
2. Packs them to `artifacts/_packages/` local feed
3. Clears NuGet cache to avoid stale packages
4. Builds Excalibur with `UsePackageReferences=true`
5. Optionally validates sample compilation

**CI automatically runs package composition validation** on every PR via the `package-composition` job in `.github/workflows/ci.yml`. This job:
- Packs Dispatch with a CI-specific version (`0.1.0-ci-<run_number>`)
- Builds Excalibur projects against the packaged dependencies
- Validates at least one sample compiles against packages

### Warning Policy & Analyzer Gates (AD-329)

All shipping code must build **warning-free**. The repository enforces `TreatWarningsAsErrors=true` globally in `Directory.Build.props`.

**Before pushing changes**, audit warnings locally:

```bash
# Basic audit - shows warning summary
pwsh eng/audit-warnings.ps1

# Detailed breakdown by file and warning code
pwsh eng/audit-warnings.ps1 -Detailed

# Export to CSV for tracking
pwsh eng/audit-warnings.ps1 -ExportCsv warnings.csv
```

**If you must suppress a warning:**

1. Use `#pragma warning disable/restore` (not `<NoWarn>` in csproj)
2. Include a comment explaining why the suppression is necessary
3. Reference the sprint/task that added the suppression for traceability

Example:
```csharp
// Sprint 329: CA1031 suppression - Quartz jobs must catch all exceptions
// to prevent the scheduler from terminating the job thread
#pragma warning disable CA1031
catch (Exception ex)
{
    _logger.LogError(ex, "Job failed");
}
#pragma warning restore CA1031
```

**Approved suppression categories:**
- **CA1031** (catch general exceptions): Quartz jobs must not throw
- **CA1005** (generic type parameters): Required for type-safe builder patterns
- **CA1863** (CompositeFormat): Exception paths where caching adds no value
- **IDE0060** (unused parameter): Interface requires parameter but implementation doesn't use it

### Sample Validation (AD-336)

All P0/P1 sample projects must build successfully. CI enforces this via the `sample-validation` job.

**Before pushing changes that affect samples**, validate locally:

```bash
# Run sample validation
pwsh eng/validate-samples.ps1

# Skip restore (faster if already restored)
pwsh eng/validate-samples.ps1 -SkipRestore

# Show detailed output
pwsh eng/validate-samples.ps1 -Detailed
```

**P0/P1 Samples (14):** These are validated and must always build:
- Core: `DispatchMinimal`, `QuickDemo`, `MinimalSample`
- Feature: `MultiBusSample`, `RemoteBusSample`, `ExcaliburCqrs`, `SagaOrchestration`
- BackgroundServices: `AtLeastOnceWithInbox`, `MinimizedWindow`, `PerformanceComparison`, `TransactionalWhenApplicable`
- Jobs: `MinimalJobSample`, `MultiTransport`
- Contracts: `WebWorkerSample/Contracts`

**P2 Samples (9):** These are skipped due to known issues and will be fixed in future sprints:
- `ECommerceSample`, `CdcAntiCorruption`, `JobWorkerSample`, `SessionManagement`
- `Dispatch.Versioning.Examples/*`, `ClaimCheck`, `WebWorkerSample/WebHost`

**If a P0/P1 sample fails:**
1. Fix the sample to build successfully, OR
2. Move it to P2 with a documented issue for later resolution

---

## Architecture & Package Boundaries

- **Dispatch vs Excalibur:** Dispatch owns the messaging pipeline, while Excalibur layers CQRS + hosting. Before adding a feature. Dispatch's `Excalibur.Dispatch.Hosting.AspNetCore` MUST remain a thin bridge; advanced hosting belongs in `Excalibur.Hosting.*`.
- **Event type names:** All event stores must persist serializer-generated type names. Use `EventTypeNameHelper.GetEventTypeName(type)` (or the `IEventSerializer.GetTypeName` wrapper) instead of storing `@event.EventType` directly. This keeps replay consistent across providers.
- **Samples:** Keep Dispatch-only samples free of Excalibur package references so consumers can see the minimal footprint. Mirror those samples with Excalibur siblings (e.g., `DispatchMinimal` vs `ExcaliburCqrs`) to document the upgrade path.

### Where Does My Code Belong?

**Dispatch** = Minimal hooks, messaging middleware, transport adapters
**Excalibur** = Full hosting/CQRS, stateful persistence, domain patterns

| Question | If Yes → |
|----------|----------|
| Is this about message routing or pipeline? | Dispatch |
| Is this cross-cutting middleware (compliance, audit)? | Dispatch |
| Is this a serverless trigger → message flow adapter? | Excalibur.Dispatch.Hosting.* |
| Is this about storing/loading domain state? | Excalibur |
| Is this stateful persistence (event store, projections)? | Excalibur.EventSourcing.* |
| Is this full-stack CQRS/ES infrastructure? | Excalibur.Hosting.* |

### Ownership Quick Reference (Sprint 330-332)

| Package Category | Owner | Rationale |
|-----------------|-------|-----------|
| **Hosting (13 packages)** | Split | Dispatch = trigger adapters; Excalibur = full infrastructure |
| **Compliance/Audit (10+1)** | Split | Dispatch = stateless middleware; Excalibur = stateful storage |
| **Caching** | Split | Excalibur.Dispatch.Caching = generic; Excalibur.Caching.Projections = ES-specific |

**Critical Rule:** Dispatch does NOT depend on Excalibur. No exceptions.

## Adding New Projects (Required)

All projects in the repository are governed. When adding a new project, follow the governance checklist:

### Quick Checklist

1. **Create in correct directory**:
   - `src/` for shipping NuGet packages
   - `tests/` for test projects
   - `samples/` for example applications
   - `benchmarks/` for performance tests

2. **Add to solution**:
   ```bash
   dotnet sln Excalibur.sln add <path-to-csproj>
   ```

3. **For shipping packages**, add required metadata (AD-326-1):
   ```xml
   <PropertyGroup>
     <!-- Required: 1-2 sentence package purpose -->
     <Description>Brief description of the package</Description>

     <!-- Required: NuGet discoverability tags -->
     <PackageTags>dispatch;excalibur;messaging;your-domain-tags</PackageTags>

     <!-- Required: README for NuGet.org display -->
     <PackageReadmeFile>README.md</PackageReadmeFile>
   </PropertyGroup>

   <!-- Required: Include README in package -->
   <ItemGroup>
     <None Include="README.md" Pack="true" PackagePath="/" />
   </ItemGroup>
   ```

4. **Create README.md** for shipping packages (see template below)

5. **Run inventory validation**:
   ```bash
   pwsh eng/inventory-projects.ps1 -Strict
   ```

### Package README Template

Each shipping project requires a `README.md` file:

```markdown
# {PackageId}

{Description - same as csproj}

## Installation

\`\`\`bash
dotnet add package {PackageId}
\`\`\`

## Quick Start

\`\`\`csharp
// Brief usage example
services.Add{Feature}();
\`\`\`

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur/docs).

## License

This package is part of the Excalibur framework. See [LICENSE](LICENSE) for details.
```

### Project Classifications

| Directory | Classification | Packable |
|-----------|---------------|----------|
| `src/` | Shipping | Yes |
| `tests/` | Test | No |
| `samples/` | Sample | No |
| `benchmarks/` | Benchmark | No |

---

## Build & Test (Recommended)

```
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release
```

## Writing Tests

All tests MUST follow the naming conventions and trait system established in Sprint 165.

### Test Naming Conventions

| Test Type | Naming Pattern | Example |
|-----------|----------------|---------|
| **Unit** | `{Class}Should.cs` or `{Class}Tests.cs` | `MessageDispatcherShould.cs` |
| **Integration** | `{Feature}IntegrationShould.cs` | `EventSourcingIntegrationShould.cs` |
| **Conformance** | `{Pattern}ConformanceTests.cs` | `OutboxStoreConformanceTests.cs` |
| **Functional** | `{Workflow}FunctionalShould.cs` | `OrderProcessingFunctionalShould.cs` |

### Required xUnit Traits

All tests MUST include appropriate xUnit traits for CI filtering:

**Unit Tests**:
```csharp
[Trait("Category", "Unit")]
[Trait("Component", "YourComponent")]
public class YourClassShould
{
    [Fact]
    public void MethodName_Scenario_ExpectedBehavior() { }
}
```

**Integration Tests**:
```csharp
[Trait("Category", "Integration")]
[Trait("Component", "YourComponent")]
public class YourFeatureIntegrationShould
{
    [Fact]
    public async Task Scenario_ExpectedOutcome() { }
}
```

**Conformance Tests** (verify interface contracts):
```csharp
[Trait("Category", "Integration")]
[Trait("Component", "Core|Compliance|Domain")]
[Trait("Pattern", "STORE|PROVIDER|SERVICE|GENERATOR|etc")]
public class YourImplementationConformanceTests : YourInterfaceConformanceTestKit
{
    protected override async Task<IYourInterface> CreateStoreAsync() { }
}
```

### Available Trait Values

**Category**: `Unit`, `Integration`, `Functional`

**Component**: `Core`, `Compliance`, `Domain`, `API`, `Messaging`, etc.

**Pattern** (conformance tests only): `STORE`, `PROVIDER`, `SERVICE`, `GENERATOR`, `VALIDATOR`, `ALERT-HANDLER`, `SCHEDULER`, `METRICS`, `DETECTION`, `TELEMETRY`, `REGISTRY`, `CACHE`, `DEDUPLICATOR`, `LEADER-ELECTION`

**Database** (future): `SqlServer`, `Postgres`, `Redis`

### Running Tests Locally

```bash
# Run unit tests only (fast feedback)
dotnet test --filter "Category=Unit"

# Run integration tests (543 conformance tests)
dotnet test --filter "Category=Integration"

# Run specific component tests
dotnet test --filter "Component=Compliance"
dotnet test --filter "Component=Core"

# Run specific pattern tests
dotnet test --filter "Pattern=STORE"
dotnet test --filter "Pattern=PROVIDER"

# Run all tests
dotnet test
```

## Security Hygiene

- Never commit secrets. A scheduled workflow (`security-secrets-scan.yml`) scans for common patterns and uploads results.
- If a false-positive appears, open an issue with the path and rationale.

---

## Project Governance

All projects in the repository are tracked by a **project manifest** (`management/governance/project-manifest.yaml`). CI validates governance rules automatically.

### Rules

1. **Every `.csproj` in a governed directory** (`src/`, `tests/`, `samples/`, `benchmarks/`, `load-tests/`) must appear in the manifest.
2. **All Shipping, Test, and Benchmark projects** must be included in `Excalibur.sln`.
3. **Excluded directories** (`templates/`, `labs/`, `tools/`) are not governed.
4. **Shipping projects** must have a `framework_owner` field (`Dispatch` or `Excalibur`).

### Adding a New Project

1. Create the `.csproj` in the appropriate governed directory.
2. Add it to the solution: `dotnet sln add <path-to-csproj>`.
3. Regenerate the manifest: `pwsh eng/inventory-projects.ps1`.
4. Verify governance: `pwsh eng/validate-solution.ps1`.

### Governance Scripts

| Script | Purpose |
|--------|---------|
| `eng/inventory-projects.ps1` | Scan filesystem and regenerate `project-manifest.yaml` |
| `eng/validate-solution.ps1` | Validate manifest ↔ filesystem ↔ solution consistency |
| `eng/add-missing-to-solution.ps1` | Bulk-add missing projects to the solution |
| `eng/audit-warnings.ps1` | Audit build warnings in shipping code |

### Warning Policy

The solution enforces `TreatWarningsAsErrors` for all shipping code. Only `NU5118` is suppressed in `src/Directory.Build.props` (NuGet packaging noise). Non-shipping code (tests, benchmarks, samples) may suppress additional diagnostics in their individual csproj files.

---

## Code Style Guidelines

This project follows strict .NET coding standards.

### Key Guidelines

| Rule | Correct | Incorrect |
|------|---------|-----------|
| No `.Core.` namespace segment | `Dispatch.Messaging` | `Excalibur.Dispatch.Messaging` |
| DI extensions namespace | `namespace Microsoft.Extensions.DependencyInjection` | `namespace Excalibur.Dispatch.DependencyInjection` |
| CancellationToken required | `Task FooAsync(CancellationToken ct)` | `CancellationToken ct = default` |
| ConfigureAwait in libraries | `await task.ConfigureAwait(false)` | `await task` |

### Namespace Conventions (AD-317)

All namespaces must follow the W6 Namespace Modernization guidelines established in Sprint 310-317.

#### DI Extension Classes (AD-317-1)

All `*ServiceCollectionExtensions` classes **MUST** be in the `Microsoft.Extensions.DependencyInjection` namespace:

```csharp
// CORRECT
namespace Microsoft.Extensions.DependencyInjection;

public static class DispatchServiceCollectionExtensions
{
    public static IServiceCollection AddDispatch(this IServiceCollection services) { }
}

// INCORRECT - will fail build
namespace Excalibur.Dispatch.DependencyInjection;

public static class DispatchServiceCollectionExtensions { }
```

#### Blocked Namespace Patterns (AD-317-2)

The following namespace patterns are **blocked** and will fail the build:

| Pattern | Reason | Use Instead |
|---------|--------|-------------|
| `Excalibur.Dispatch.*` | Eliminated in W6 | Flat `Dispatch.*` namespaces |
| `Excalibur.Dispatch.Patterns.Sagas.*` | Moved to Excalibur | `Excalibur.Saga` namespace |
| `Dispatch.*.Abstractions.*.*` | Deep hierarchy | Flat `.Abstractions` namespace |

#### Provider Package Pattern

Provider packages follow the convention `Dispatch.{Feature}.{Provider}`:

```csharp
// CORRECT
namespace Excalibur.Dispatch.Transport.Azure;
namespace Excalibur.Dispatch.Compliance.Aws;
namespace Excalibur.Dispatch.LeaderElection;

// INCORRECT - too deep
namespace Excalibur.Dispatch.Transport.Azure.ServiceBus.Configuration;
```

#### Namespace Depth

Maximum namespace depth is 4 segments:

```csharp
// CORRECT (3-4 segments)
namespace Excalibur.Dispatch.Messaging;
namespace Excalibur.Dispatch.Transport.Azure;
namespace Excalibur.Dispatch.Hosting.AspNetCore;

// INCORRECT (5+ segments)
namespace Excalibur.Dispatch.Messaging.Delivery.Scheduling.Internal;
```

#### Verification

The build automatically validates namespaces. To validate architecture boundaries manually:

```powershell
# Run architecture boundary validation
pwsh eng/validate-architecture-boundaries.ps1
```

### EditorConfig

- Respect `.editorconfig` (warnings = errors)
- Keep changes small and focused

### Commit Messages

Use conventional commit format:

```
type(scope): description

[optional body]

[optional footer]
```

**Types:** `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`

**Examples:**
```
feat(outbox): add batch processing support
fix(dispatcher): resolve race condition in pipeline
docs(contributing): add code style guidelines
```

---

## Pull Request Process

### Before Submitting

1. **Create an issue first** (unless trivial fix)
2. **Fork and branch** from `main`
3. **Follow naming**: `feature/description` or `fix/description`
4. **Write tests** for new functionality
5. **Run all tests locally**:
   ```bash
   dotnet test
   ```
6. **Update documentation** if applicable

### PR Checklist

- [ ] Tests pass locally
- [ ] Code follows style guidelines (ADR-075)
- [ ] **Namespace conventions followed (AD-317)** - DI extensions in correct namespace, no blocked patterns
- [ ] Documentation updated (if applicable)
- [ ] Commit messages follow conventional format
- [ ] No secrets or sensitive data included
- [ ] Linked to issue (if applicable)

### Review Process

1. Submit PR with clear description
2. Automated CI runs tests
3. Maintainer reviews code
4. Address feedback
5. Merge after approval

---

## Issue Triage Labels

### Priority Labels

| Label | Description |
|-------|-------------|
| `P0-critical` | Showstopper, blocks release |
| `P1-high` | Important, should be in next sprint |
| `P2-medium` | Normal priority |
| `P3-low` | Nice to have |

### Type Labels

| Label | Description |
|-------|-------------|
| `bug` | Something isn't working |
| `enhancement` | New feature request |
| `documentation` | Documentation improvement |
| `question` | Further information requested |
| `performance` | Performance improvement |
| `security` | Security-related issue |

### Status Labels

| Label | Description |
|-------|-------------|
| `needs-triage` | Needs initial review |
| `needs-design` | Requires architecture decision |
| `ready` | Ready for implementation |
| `in-progress` | Being worked on |
| `blocked` | Blocked by external factor |

---

## Getting Help

- **Questions:** Open a [GitHub Discussion](https://github.com/TrigintaFaces/Excalibur/discussions)
- **Bugs:** Open an [Issue](https://github.com/TrigintaFaces/Excalibur/issues/new/choose)
- **Security vulnerabilities:** Use [GitHub Security Advisories](https://github.com/TrigintaFaces/Excalibur/security) (private disclosure)
- **Documentation:** Check the [docs site](https://docs.excalibur-dispatch.dev)

For full support policy details including response times, provider tiers, and EOL policy, see [SUPPORT.md](SUPPORT.md).

---

## Releasing

The release process is documented in [RELEASE.md](RELEASE.md).

### Who Can Release

| Role | Authority |
|------|-----------|
| **Maintainers** | Full release authority (create tags, trigger pipeline) |
| **Contributors** | Can request releases via issues |

### Requesting a Release

Contributors can request a release by:

1. Open an issue with the `release-request` label
2. Include:
   - **Proposed version**: Following SemVer (e.g., `1.1.0`)
   - **Changes to include**: List of PRs/commits
   - **Urgency**: Normal, high (security fix), critical (production issue)

### Release Cadence

| Type | Frequency | Trigger |
|------|-----------|---------|
| **Patch** | As needed | Bug fixes, security patches |
| **Minor** | ~Monthly | Feature batches |
| **Major** | Rare | Breaking API changes |

### Quick Reference

```bash
# Local dry run (before requesting release)
dotnet pack -c Release --output ./release-test/
Get-ChildItem ./release-test/*.nupkg | Measure-Object  # Should be ~93

# Maintainers: Create release
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0
```

See [RELEASE.md](RELEASE.md) for the complete release process, including pre-release checklist, pipeline details, and troubleshooting.

---

## First-Time Contributors

New to open source? We welcome first-time contributors! Look for issues labeled `good-first-issue`.

### Your First Pull Request

1. Find an issue labeled `good-first-issue`
2. Comment to claim it
3. Fork the repository
4. Create a branch
5. Make your changes
6. Submit a PR

Don't worry about making mistakes - we're here to help!

---

Thank you for contributing!
