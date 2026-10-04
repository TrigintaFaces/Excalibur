# Test suite

This directory contains contributor tests for Dispatch and Excalibur. Support libraries
also live here; not every project under `tests/` is an executable test assembly.

## Navigation

| Path | Purpose |
| --- | --- |
| [unit/](unit/) | Unit tests grouped by source package or merged test project |
| [integration/](integration/) | Integration tests, including real databases and brokers |
| [functional/](functional/) | Application workflow tests |
| [conformance/](conformance/) | Provider and transport contract implementations |
| [contract/](contract/) | Contract checks |
| [smoke/](smoke/) | Smoke tests |
| [performance/](performance/) | Performance tests |
| [property/](property/) | Property-based tests |
| [architecture/Boundary.Tests/](architecture/Boundary.Tests/) | Architecture and repository boundary enforcement |
| [Shared/Tests.Shared/](Shared/Tests.Shared/) | Common bases, fixtures and conformance helpers |
| [Shared/Excalibur.Dispatch.Testing/](Shared/Excalibur.Dispatch.Testing/) | Dispatch testing support library |

The solution filters in [eng/ci/shards](../eng/ci/shards/) define CI project sets.
Source packages can map to merged test projects: ASP.NET Core hosting tests, for
example, live in [Excalibur.Dispatch.Hosting.Tests](unit/Excalibur.Dispatch.Hosting.Tests/).
Consult the [critical package test matrix](../eng/governance/framework-governance.json)
and shard files when moving projects. Update solution membership and validation
manifests together.

## Running tests

Run commands from the repository root. Install the SDK pinned in
[global.json](../global.json); `rollForward: disable` requires that exact version.
The [build entry point](../eng/build.ps1) composes restore, build, test settings and
result checks for local and CI use.

```powershell
# One source-aligned test project.
pwsh ./eng/build.ps1 -Test -Project tests/unit/Excalibur.Dispatch.Hosting.Tests/Excalibur.Dispatch.Hosting.Tests.csproj

# The existing core shard, filtered to unit tests.
pwsh ./eng/build.ps1 -Test -Project eng/ci/shards/UnitTests-Core.slnf -TestFilter "Category=Unit"

# Architecture project: run its population instead of assuming a common trait.
pwsh ./eng/build.ps1 -Test -Project tests/architecture/Boundary.Tests/Boundary.Tests.csproj

# Full solution. Required integration scenarios need their infrastructure available.
pwsh ./eng/build.ps1 -Test -Project Excalibur.sln
```

Pass a project, solution or solution filter. `tests/unit` and `tests/integration` are
organizational directories, not aggregate test projects. A category filter only selects
tests in the supplied projects; it does not establish whole-repository coverage.
Use `-NoBuild -NoRestore` only after building the same candidate and configuration.
Empty discovery, skipped fixtures and unavailable infrastructure are not passing tests.
Preserve test identities, counts, TRX files and candidate/package identities as required
by the relevant [CI gate](../docs/ci-gates.md).

## Traits and fixtures

Use the required `Category`, `Component` and `Pattern` traits according to the
[test standards](../docs/testing/test-standards.md) and
[filtering guide](../docs/testing/ci-filtering-guide.md). Constants live in
[TestCategories.cs](Shared/Tests.Shared/Categories/TestCategories.cs).
`UnitTestBase`, `IntegrationTestBase` and `FunctionalTestBase` provide their respective
category traits; inheriting a category does not supply all other required traits.

Use the existing [container collections](Shared/Tests.Shared/Fixtures/ContainerCollections.cs)
and their fixtures rather than allocating a container per test. Follow the
[container setup](../docs/testing/testcontainers-setup.md) and
[fixture guidance](../docs/testing/test-fixtures.md) for prerequisites and cleanup.
Shared resources must preserve tenant, database and message isolation.

Parallelism depends on each assembly's `xunit.runner.json`, collection definitions and
run settings. A collection name alone does not specify whether parallelization with
other collections is disabled. Check the applicable configuration.

Elapsed time depends on the selected population, platform, container startup and runner
capacity. Historical whole-suite targets are not measured completion guarantees. Keep
performance assertions and timeout budgets tied to current retained evidence.

## More guidance

- [Testing index](../docs/testing/README.md)
- [Test organization](../docs/testing/test-organization.md)
- [Architecture checks](../docs/testing/architecture-tests.md)
- [Transport conformance](../docs/testing/transport-conformance.md)
- [Quarantine policy](../docs/testing/flaky-test-quarantine.md)

All required tests must pass for release acceptance. A focused local run proves only
its selected population, not completion of the full release matrix.
