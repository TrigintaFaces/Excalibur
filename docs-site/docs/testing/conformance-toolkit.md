---
sidebar_position: 9
title: Consumer Conformance Toolkit
description: Exercise your own provider implementations against the shipped conformance test kits and opt-in TestContainers fixtures
---

# Consumer Conformance Toolkit

When you implement one of Excalibur's provider contracts — a custom `IEventStore`, `IOutboxStore`,
`ISagaStore`, `ILeaderElection`, `ICdcStateStore`, `IDataRequestRetryPolicy`, and so on — the
**conformance toolkit** gives you a starting suite that exercises your implementation against
the behaviors we test our own providers with.

The kits encode our current understanding of each contract. They are not a specification of it, and
a kit can be incomplete or wrong. A passing kit means your implementation agrees with ours on the
behaviors that kit exercises. It is not certification. Where a contract carries a security or isolation
guarantee, verify that guarantee against the contract's own documentation and your own tests — a kit can
pass an implementation that does not hold it.

If a kit fails your implementation, check the contract's documentation before changing your code. A kit
can encode a rule the contract does not require — and at least one currently does, in a way a correct
implementation cannot satisfy.

:::caution Known gap
The in-memory data-inventory store registered by `AddInMemoryDataInventoryStore()` is not
tenant-isolated: reads are not scoped to the calling tenant. Use a database-backed provider where tenant
isolation matters.
:::

Two packages make up the toolkit:

| Package | What it provides |
|---------|------------------|
| `Excalibur.Testing.Conformance` | Abstract `{Contract}ConformanceTestKit` base classes — framework-agnostic, no Docker dependency. |
| `Excalibur.Testing.Containers` | Opt-in TestContainers fixtures (`SqlServerContainerFixture`, `PostgresContainerFixture`) for running kits against a real backend. |

The Containers package is kept separate so that if you only test in-memory, you never pull a Docker or
TestContainers dependency.

## Testing your implementation against a kit

Each conformance kit is an `abstract class` with a small number of factory hooks you implement, plus a set
of `public virtual` conformance methods. Inherit the kit, supply your implementation through the hooks,
and expose the checks to your test framework:

```csharp
using Excalibur.Data.Resilience;
using Excalibur.Testing.Conformance;
using Xunit;

public sealed class MyRetryPolicyConformanceTests : RetryPolicyConformanceTestKit
{
    protected override IDataRequestRetryPolicy CreatePolicy(int maxRetryAttempts) =>
        new MyRetryPolicy(maxRetryAttempts);

    protected override Exception CreateRetryableException() => new TimeoutException();
    protected override Exception CreateNonRetryableException() => new ArgumentException();

    [Fact] public void MaxRetryAttempts_Match() => MaxRetryAttempts_ShouldMatchConfiguredValue();
    [Fact] public void ShouldRetry_Transient() => ShouldRetry_WithRetryableException_ReturnsExpectedResult();
    [Fact] public void ShouldRetry_Permanent() => ShouldRetry_WithNonRetryableException_ReturnsFalse();
}
```

The kits carry no `[Fact]`/`[Theory]` attributes themselves, so they work with xUnit, NUnit, MSTest, or any
runner — you add the attributes on the thin overrides. A failing check throws
`TestFixtureAssertionException` with a message describing the contract violation.

## Testing against a real backend

For providers backed by a database, run the same kit against a real engine with an opt-in fixture:

```csharp
using Excalibur.Testing.Containers;
using Xunit;

public sealed class MySqlServerOutboxConformanceTests
    : OutboxStoreConformanceTestKit, IClassFixture<SqlServerContainerFixture>
{
    private readonly SqlServerContainerFixture _fixture;

    public MySqlServerOutboxConformanceTests(SqlServerContainerFixture fixture) => _fixture = fixture;

    protected override IOutboxStore CreateStore() => new MySqlServerOutboxStore(_fixture.ConnectionString);
}
```

`SqlServerContainerFixture` (and `PostgresContainerFixture`) derive from `ContainerFixtureBase`, which
manages the Docker container lifecycle — startup with a bounded timeout, transient-failure retry, and
best-effort teardown. Container timeouts scale with the `TEST_TIMEOUT_MULTIPLIER` environment variable.

## A runnable example

A complete, runnable worked example lives at `samples/09-advanced/ConsumerConformanceExample`. It runs a
conformance kit against a custom retry policy (no Docker needed) and, with `--with-container`, spins up a
real SQL Server fixture:

```bash
dotnet run                     # conformance checks against a custom implementation
dotnet run -- --with-container # also start a real SQL Server (requires Docker)
```

## Related

- [Aggregate Testing](./aggregate-testing.md) — Given-When-Then unit testing for event-sourced aggregates.
- [Transport Test Doubles](./transport-test-doubles.md) — in-memory transport harnesses for fast handler tests.
