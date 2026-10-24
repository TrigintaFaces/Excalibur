# Contributing to Excalibur

Thank you for your interest in contributing to Excalibur! This document provides guidelines and information for contributors.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Development Environment Setup](#development-environment-setup)
- [Building the Project](#building-the-project)
- [Running Tests](#running-tests-locally)
- [Project Governance](#project-governance)
- [Code Style Guidelines](#code-style-guidelines)
- [Contribution Licensing](#contribution-licensing)
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

| Requirement                | Version | Purpose                                      |
| -------------------------- | ------- | -------------------------------------------- |
| .NET SDK                   | 10.0+   | Building and running the project             |
| Visual Studio 2022 / Rider | Latest  | IDE (optional but recommended)               |
| Docker Desktop             | Latest  | Running TestContainers for integration tests |
| Git                        | 2.x+    | Version control                              |
| PowerShell                 | 7.x+    | Running build scripts                        |

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

### Package Reference Modes

The repository supports two build modes:

| Mode                 | Default | Use Case                                    |
| -------------------- | ------- | ------------------------------------------- |
| **ProjectReference** | Yes     | Development (fast iteration, debugging)     |
| **PackageReference** | No      | CI validation (package composition testing) |

**Development Mode (Default):**

```bash
# Fast builds using ProjectReference - use for everyday development
dotnet build Excalibur.sln -p:BuildExamplesAndTests=true
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

**Target Framework:**

All shipping packages target `net10.0` (single-target; .NET 8 and .NET 9 multi-targeting was dropped — project is .NET 10-only).

```bash
# Build
dotnet build

# Pack produces single-TFM packages
dotnet pack src/Dispatch/Excalibur.Dispatch/Excalibur.Dispatch.csproj
# Creates: Excalibur.Dispatch.10.0.0-alpha.0.<height>.nupkg with lib/net10.0/
# The version is derived from git tags, so a local build below the release tag
# carries a development version rather than the released one.
```

### Local Package Validation

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

### Warning Policy & Analyzer Gates

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
// CA1031 suppression - Quartz jobs must catch all exceptions
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

### Choosing Rule Enforcement

For C# rules, prefer Roslyn symbols, syntax trees, and semantic analysis over hand-written regex parsers. Choose the first suitable mechanism:

1. Existing .NET analyzers and MSBuild validation.
2. Compiled architecture or behavioral tests.
3. Custom Roslyn analyzers for repository-specific rules.
4. Scripts for orchestration, artifacts, and external services.

Preserve the policy when replacing its implementation. Keep namespace depth limits: prefer at most four segments, justify five, and reject six or more. A replacement must exercise the real source or compiled population and include controls that reject violations and accept valid cases. Missing inputs or unexecuted tests must not produce a passing verdict.

### Sample Validation

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

- Core: `DispatchOnly`, `InteractiveDemo`, `HelloDispatch`
- Feature: `MultiBusSample`, `RemoteBusSample`, `EventSourcingIntro`, `SagaOrchestration`
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
- **Samples:** Keep Dispatch-only samples free of Excalibur package references so consumers can see the minimal footprint. Mirror those samples with Excalibur siblings (e.g., `DispatchOnly` vs `EventSourcingIntro`) to document the upgrade path.

### Where Does My Code Belong?

**Dispatch** = Minimal hooks, messaging middleware, transport adapters
**Excalibur** = Full hosting/CQRS, stateful persistence, domain patterns

| Question                                                 | If Yes →                      |
| -------------------------------------------------------- | ----------------------------- |
| Is this about message routing or pipeline?               | Dispatch                      |
| Is this cross-cutting middleware (compliance, audit)?    | Dispatch                      |
| Is this a serverless trigger → message flow adapter?     | Excalibur.Dispatch.Hosting.\* |
| Is this about storing/loading domain state?              | Excalibur                     |
| Is this stateful persistence (event store, projections)? | Excalibur.EventSourcing.\*    |
| Is this full-stack CQRS/ES infrastructure?               | Excalibur.Hosting.\*          |

### Ownership Quick Reference

| Package Category            | Owner | Rationale                                                                         |
| --------------------------- | ----- | --------------------------------------------------------------------------------- |
| **Hosting (13 packages)**   | Split | Dispatch = trigger adapters; Excalibur = full infrastructure                      |
| **Compliance/Audit (10+1)** | Split | Dispatch = stateless middleware; Excalibur = stateful storage                     |
| **Caching**                 | Split | Excalibur.Dispatch.Caching = generic; Excalibur.Caching.Projections = ES-specific |

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

3. **For shipping packages**, add required metadata:

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

Excalibur is available under your choice of the Excalibur License 1.1, AGPL-3.0-or-later, or
SSPL-1.0 — see [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE).
```

The licence line states the **choice**, and the link is absolute. A package README renders on
nuget.org, where a relative `LICENSE` link resolves to nothing — and a reader who is not told the
terms are a choice will assume whichever one they happen to find first.

### Project Classifications

| Directory     | Classification | Packable |
| ------------- | -------------- | -------- |
| `src/`        | Shipping       | Yes      |
| `tests/`      | Test           | No       |
| `samples/`    | Sample         | No       |
| `benchmarks/` | Benchmark      | No       |

---

## Build & Test (Recommended)

```
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release
```

## Writing Tests

All tests MUST follow the naming conventions and trait system set out below.

### Test Naming Conventions

| Test Type       | Naming Pattern                          | Example                              |
| --------------- | --------------------------------------- | ------------------------------------ |
| **Unit**        | `{Class}Should.cs` or `{Class}Tests.cs` | `MessageDispatcherShould.cs`         |
| **Integration** | `{Feature}IntegrationShould.cs`         | `EventSourcingIntegrationShould.cs`  |
| **Conformance** | `{Pattern}ConformanceTests.cs`          | `OutboxStoreConformanceTests.cs`     |
| **Functional**  | `{Workflow}FunctionalShould.cs`         | `OrderProcessingFunctionalShould.cs` |

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

- Never commit secrets. The `secret-scan.yml` workflow runs [Gitleaks](https://github.com/gitleaks/gitleaks) on every push and PR.
- False-positive allowlists are managed in **`.gitleaks.toml`** using path-regex and secret-regex rules (not line numbers). This ensures allowlists survive file moves, line shifts, and refactors.
- To investigate a new false positive:
    1. Run locally: `gitleaks detect --source . --no-git --config .gitleaks.toml -v --report-format json --report-path findings.json`
    2. Inspect the finding in `findings.json` to confirm it is not a real secret.
    3. Add a path regex or secret-value regex to the `[allowlist]` section in `.gitleaks.toml`.
    4. Re-run gitleaks to verify the finding is suppressed.
- The `.gitleaksignore` file is reserved for emergency overrides only; prefer `.gitleaks.toml` for all allowlist changes.

---

## Project Governance

All projects in the repository are tracked by a **project manifest** (`eng/governance/project-manifest.yaml`). CI validates governance rules automatically.

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

| Script                            | Purpose                                                |
| --------------------------------- | ------------------------------------------------------ |
| `eng/inventory-projects.ps1`      | Scan filesystem and regenerate `project-manifest.yaml` |
| `eng/validate-solution.ps1`       | Validate manifest ↔ filesystem ↔ solution consistency  |
| `eng/add-missing-to-solution.ps1` | Bulk-add missing projects to the solution              |
| `eng/audit-warnings.ps1`          | Audit build warnings in shipping code                  |

### Warning Policy

The solution enforces `TreatWarningsAsErrors` for all shipping code. Only `NU5118` is suppressed in `src/Directory.Build.props` (NuGet packaging noise). Non-shipping code (tests, benchmarks, samples) may suppress additional diagnostics in their individual csproj files.

---

## Code Style Guidelines

This project follows strict .NET coding standards.

### Key Guidelines

| Rule                          | Correct                                              | Incorrect                                          |
| ----------------------------- | ---------------------------------------------------- | -------------------------------------------------- |
| No `.Core.` namespace segment | `Dispatch.Messaging`                                 | `Excalibur.Dispatch.Messaging`                     |
| DI extensions namespace       | `namespace Microsoft.Extensions.DependencyInjection` | `namespace Excalibur.Dispatch.DependencyInjection` |
| CancellationToken required    | `Task FooAsync(CancellationToken ct)`                | `CancellationToken ct = default`                   |
| ConfigureAwait in libraries   | `await task.ConfigureAwait(false)`                   | `await task`                                       |

### Namespace Conventions

All namespaces must follow the conventions set out below.

#### DI Extension Classes

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

#### Blocked Namespace Patterns

The following namespace patterns are **blocked** and will fail the build:

| Pattern                               | Reason                                                        | Use Instead                             |
| ------------------------------------- | ------------------------------------------------------------- | --------------------------------------- |
| `.Core.`                              | Meaningless implementation-layer segment                      | The owning package or feature namespace |
| `Excalibur.Dispatch.Patterns.Sagas.*` | Moved to Excalibur                                            | `Excalibur.Saga` namespace              |
| Redundant `.Abstractions` segments    | Exposes package organization rather than the consumer concept | The owning feature namespace            |

#### Provider Package Pattern

Provider packages follow `Excalibur.Dispatch.{Feature}.{Provider}` or `Excalibur.{Feature}.{Provider}`:

```csharp
// CORRECT
namespace Excalibur.Dispatch.Transport.Azure;
namespace Excalibur.Compliance.Aws;
namespace Excalibur.Dispatch.LeaderElection;

```

#### Namespace Depth

Organize namespaces by stable product and related feature groups and avoid redundant segments.
For production code, prefer at most four segments; five requires architectural justification;
six or more fails the namespace-validation workflow. This repository's depth budget keeps the public
surface discoverable. Preserve published namespaces and review any necessary API migration explicitly.

See [Microsoft's namespace design guidance](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces).

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

## Contribution Licensing

**Read this before opening a pull request — submitting one is how you agree to it.**

By submitting a contribution to this project you agree that:

- your contribution is licensed to the Excalibur Project under the **Excalibur License 1.1**; and
- you grant the Excalibur Project a perpetual, irrevocable right to distribute your contribution under
  **any** of the project’s outbound licences — Excalibur License 1.1, AGPL-3.0-or-later or SSPL-1.0 —
  and to license it under other terms, including commercial terms.

The second point is the one worth reading twice: it is a re-licensing grant, so a contribution you
make here may be distributed to others under a licence you did not choose. That is deliberate — the
project offers consumers a choice of licences, and may offer commercial terms, and cannot do either without it.

The governing text is the **Contribution Licensing** section of [LICENSE](LICENSE); this section
summarises it and does not replace it. If the two ever disagree, LICENSE controls.

### Notices you must not remove

The licence requires that anyone receiving any part of the software also receives its terms, and
that modified copies carry a prominent notice saying they were modified. So do not alter, remove or
obscure the licensing and copyright notices already in a file you are editing, and keep them on any
file you copy or move.

### Adding a dependency

A new dependency has to be compatible with **all three** outbound licences, not just the one you
have in mind. Anything that cannot be carried into the Excalibur License 1.1, AGPL-3.0-or-later and
SSPL-1.0 alike takes the choice away from consumers, so it will be declined however convenient it
is. Copyleft terms reaching beyond the dependency itself are the usual blocker. Raise a dependency
you are unsure about in the pull request, before it lands.

When you add one, record the licence it actually ships in `eng/ci/package-licenses.json` and
regenerate the notices file:

```powershell
pwsh ./eng/ci/notices-generate.ps1
```

The generator will not emit a row with an empty licence cell. A package you have not recorded fails
the run with `No licence recorded for: <package>`, naming what to add. Read the licence in the
package itself rather than the vendor's usual one — they differ more often than you would expect.

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
- [ ] Code follows the [style guidelines](#code-style-guidelines)
- [ ] **Namespace conventions followed** - DI extensions in correct namespace, no blocked patterns
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

| Label         | Description                         |
| ------------- | ----------------------------------- |
| `P0-critical` | Showstopper, blocks release         |
| `P1-high`     | Important, should be in next sprint |
| `P2-medium`   | Normal priority                     |
| `P3-low`      | Nice to have                        |

### Type Labels

| Label           | Description                   |
| --------------- | ----------------------------- |
| `bug`           | Something isn't working       |
| `enhancement`   | New feature request           |
| `documentation` | Documentation improvement     |
| `question`      | Further information requested |
| `performance`   | Performance improvement       |
| `security`      | Security-related issue        |

### Status Labels

| Label          | Description                    |
| -------------- | ------------------------------ |
| `needs-triage` | Needs initial review           |
| `needs-design` | Requires architecture decision |
| `ready`        | Ready for implementation       |
| `in-progress`  | Being worked on                |
| `blocked`      | Blocked by external factor     |

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

| Role             | Authority                                              |
| ---------------- | ------------------------------------------------------ |
| **Maintainers**  | Full release authority (create tags, trigger pipeline) |
| **Contributors** | Can request releases via issues                        |

### Requesting a Release

Contributors can request a release by:

1. Open an issue with the `release-request` label
2. Include:
    - **Proposed version**: Following SemVer (e.g., `1.1.0`)
    - **Changes to include**: List of PRs/commits
    - **Urgency**: Normal, high (security fix), critical (production issue)

### Release Cadence

| Type      | Frequency | Trigger                     |
| --------- | --------- | --------------------------- |
| **Patch** | As needed | Bug fixes, security patches |
| **Minor** | ~Monthly  | Feature batches             |
| **Major** | Rare      | Breaking API changes        |

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
