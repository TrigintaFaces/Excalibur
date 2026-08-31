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

## What a green run actually covered

Some kit arms exercise an **optional** capability of the contract — the outbox and dead-letter kits both
have arms that need the store's administrative facet, for example. An arm whose capability is unavailable
has to do something, and the obvious thing — return — is the one thing it must not do silently: every test
runner reports an arm that returned early exactly as it reports an arm that ran and passed.

The kits now record that distinction instead of erasing it. `ConformanceArmLedger` collects every arm that
ran its body and every arm that did not, with the capability it needed and why it was unavailable:

```csharp
using Excalibur.Testing.Conformance;

ConformanceArmLedger.Reset();          // process-wide and additive; reset before a run you intend to read

// ... run your derived kit ...

foreach (var arm in ConformanceArmLedger.Executed)   // "Suite.Arm" keys
{
    Console.WriteLine($"verified: {arm}");
}

foreach (var skip in ConformanceArmLedger.Skipped)   // ConformanceArmSkip records
{
    Console.WriteLine($"NOT verified: {skip.Suite}.{skip.Arm} — {skip.Capability?.Name} — {skip.Reason}");
}

Console.WriteLine(ConformanceArmLedger.Describe());  // both lists, formatted
```

:::caution A run that was green before may now report skips
This is not a regression, and it does not mean your provider got worse. An arm that reported a pass while
never reaching its assertions now reports as unverified. The skip is the kit declining to overstate what it
checked — the coverage is the same as it always was, and only the reporting has changed. Read the skips and
decide, per capability, whether an unverified arm is acceptable for your certification.
:::

The ledger is a **reporting surface, not an assertion**: it records, and you decide what an unverified arm
means. To surface skips natively in your runner — or to make one a failure — override `OnArmSkipped` on the
kit. The hook comes from `ConformanceTestKit`, which the capability-gated kits derive from —
`OutboxStoreConformanceTestKit` and `DeadLetterStoreConformanceTestKit` today:

```csharp
public sealed class MyOutboxConformanceTests : OutboxStoreConformanceTestKit
{
    // Surface the skip in the runner instead of letting the arm complete quietly.
    protected override void OnArmSkipped(ConformanceArmSkip skip) =>
        Assert.Skip($"{skip.Arm}: {skip.Reason}");   // xUnit v3; Assert.Ignore under NUnit

    // Or certify that every capability your store provides was actually reached:
    // protected override void OnArmSkipped(ConformanceArmSkip skip) =>
    //     throw new InvalidOperationException($"unverified arm {skip.Arm}: {skip.Reason}");
}
```

### If your store is decorated

The outbox kit discovers capabilities through `GetService(Type)`, which the contract requires and which a
well-behaved decorator forwards — so a decorated outbox store is discovered correctly.

**`IDeadLetterStore` has no capability-resolution method**, so its kit can only discover the administrative
facet from the store's own type. That is sound for a store handed to the kit directly and unsound for a
decorated one: a wrapper's type does not carry the capabilities of what it wraps, so the facet becomes
invisible and every arm needing it is skipped. If you certify a decorated dead-letter store, override
`ResolveAdminFacet` to return the facet the wrapper holds:

```csharp
public sealed class MyDeadLetterConformanceTests : DeadLetterStoreConformanceTestKit
{
    protected override IDeadLetterStoreAdmin? ResolveAdminFacet(IDeadLetterStore store) =>
        store is MyDeadLetterDecorator decorator
            ? decorator.Inner as IDeadLetterStoreAdmin
            : base.ResolveAdminFacet(store);
}
```

Without the override the arms still report — as skips, in the ledger, rather than as silent passes — but a
recorded absence is still an uncertified capability.

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
