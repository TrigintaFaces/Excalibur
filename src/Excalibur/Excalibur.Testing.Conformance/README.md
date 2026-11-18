# Excalibur.Testing.Conformance

Conformance test kits for Excalibur infrastructure implementations. Provides reusable abstract test suites that verify provider implementations conform to the expected contracts for IEventStore, ISnapshotStore, IOutboxStore, ISagaStore, and other infrastructure interfaces.

## Installation

```bash
dotnet add package Excalibur.Testing.Conformance
```

## Purpose

When implementing a custom provider (e.g., a new database backend for event sourcing), use these conformance test kits to verify your implementation meets all contract requirements. Each test kit provides a comprehensive set of tests covering happy paths, edge cases, and error handling.

## Use an exclusively owned test scope

These kits issue real writes through the provider you supply, including inserts, updates, claims, deletes, cache clears, key rotation, and counter or lease transitions. Use isolated resources exclusively owned by the test fixture, with production data and writers outside that scope. A dedicated persistent test database, table/container, queue, or key namespace is supported; disposable containers are not required. Quiesce other writers in the selected test scope before running a kit. A tenant or partition alone is not sufficient isolation for arms that exercise estate-wide administration.

Your provider factories, registration hooks, and cleanup overrides determine the actual backing resources and their cleanup. Some default cleanup hooks do nothing; the package does not snapshot or universally restore existing data. The outbox kit restores the fencing high-water it captured, but that forced restoration is not a concurrent-production rollback. If another writer advances that high-water during the run, the forced restoration can lower its legitimate advance; keep that test scope exclusive/quiesced.

## Available Test Kits

The package includes these commonly implemented kits:

| Test Kit | Interface Under Test |
|----------|---------------------|
| `EventStoreConformanceTestKit` | `IEventStore` |
| `SnapshotStoreConformanceTestKit` | `ISnapshotStore` |
| `OutboxStoreConformanceTestKit` | `IOutboxStore` |
| `InboxStoreConformanceTestKit` | `IInboxStore` |
| `SagaStoreConformanceTestKit` | `ISagaStore` |
| `DeadLetterStoreConformanceTestKit` | `IDeadLetterStore` |
| `LeaderElectionConformanceTestKit` | `ILeaderElection` |
| `FencingTokenProviderConformanceTestKit` | `IFencingTokenProvider` |
| `EncryptionProviderConformanceTestKit` | `IEncryptionProvider` |
| `AuditStoreConformanceTestKit` | `IAuditStore` |
| `TransportConformanceTestKit<TSender, TReceiver>` | transport sender/receiver pairs |
| `DbConformanceTestKit` | `IDb` |
| `PersistenceProviderConformanceTestKit` | persistence providers |

| `PositionedProjectionStoreConformanceTestKit` | `IPositionedProjectionStore<T>` |

### Run the positioned-projection kit against real infrastructure

`PositionedProjectionStoreConformanceTestKit` is worth singling out, because the thing it checks is
decided entirely by your database and not by any C# you can inspect. It states the rule that makes a
re-delivered event refusable: for a projection with stored position `P`, the stored state is the fold
of exactly those events at or below `P`. A write must advance FROM the position it read at (which
orders concurrent writers) AND advance FORWARDS (which refuses a re-delivery), and a store enforcing
only one of those passes for the wrong reason.

**Do not run it against a mocked client.** Every interesting arm is about what the engine does when two
writers collide, and a mock returns what it was told. Running these arms against real services found
four providers that could not execute their own conditional write at all — a parameter the planner
could not type, an expression value the service rejected, a date type the driver could not map, and a
JSON type the client could not construct. All four compiled, and all four passed their mocked unit
tests.

Also included, covering scheduling, CDC, claim-check, caching, deduplication, retry, workflow, key
management, and compliance surfaces:

`CacheTagTrackerConformanceTestKit`, `CdcProviderConformanceTestKit`,
`ClaimCheckProviderConformanceTestKit`, `ComplianceAlertHandlerConformanceTestKit`,
`ComplianceMetricsConformanceTestKit`, `ControlValidationServiceConformanceTestKit`,
`ControlValidatorConformanceTestKit`, `CronJobStoreConformanceTestKit`,
`DataInventoryStoreConformanceTestKit`, `DeduplicatorConformanceTestKit`,
`EncryptionMigrationServiceConformanceTestKit`, `EncryptionProviderRegistryConformanceTestKit`,
`EncryptionTelemetryConformanceTestKit`, `ErasureStoreConformanceTestKit`,
`FipsDetectorConformanceTestKit`, `KeyCacheConformanceTestKit`,
`KeyEscrowServiceConformanceTestKit`, `KeyManagementProviderConformanceTestKit`,
`KeyRotationAlertHandlerConformanceTestKit`, `KeyRotationSchedulerConformanceTestKit`,
`LegalHoldStoreConformanceTestKit`, `MasterKeyBackupServiceConformanceTestKit`,
`MinimalWiringConformanceTestKit<TBuilderExtension>`, `RetryPolicyConformanceTestKit`,
`ScheduleStoreConformanceTestKit`, `SchedulerConformanceTestKit`,
`Soc2ReportGeneratorConformanceTestKit`, `Soc2ReportStoreConformanceTestKit`,
`StreamingHandlerConformanceTestKit`, `WorkflowConformanceTestKit`.

## Quick Start

Derive from the kit for the interface you implement and register your provider using its **own public
registration extension**. The kit resolves the service under test from a real container built from those
registrations, so every assertion runs against the object a consumer actually gets.

```csharp
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Testing.Conformance;
using Microsoft.Extensions.DependencyInjection;

// Managed/non-trimmed test hosts only, as required by the base kit.
[RequiresUnreferencedCode("Event-store conformance deserializes consumer event types; a trimmed test host is unsupported.")]
public sealed class SqlServerEventStoreConformanceTests(string connectionString, Func<Task> cleanupOwnedTestScope)
    : EventStoreConformanceTestKit
{
    protected override void ConfigureProvider(IServiceCollection services, IJsonTypeInfoResolver? eventTypeInfoResolver)
    {
        // The same shipped provider registration is used for reflection and supplied-resolver arms.
        services.AddExcalibur(x => x.AddEventSourcing(es => es.UseSqlServer(sql =>
        {
            sql.ConnectionString(connectionString);
            if (eventTypeInfoResolver is not null)
            {
                sql.EventTypeInfoResolver(eventTypeInfoResolver);
            }
        })));
    }

    // The runner supplies real cleanup for its exclusively owned, provisioned SQL test scope.
    protected override Task CleanupAsync() => cleanupOwnedTestScope();
}
```

This is a provider-registration skeleton for a managed, non-trimmed test host, not a complete runner adapter or an executed conformance result. Supply a connection string for an exclusively owned SQL test database with the provider schema installed, and a real cleanup callback for that scope. Connect the kit lifecycle and all applicable arms to your test runner; the callback is not a substitute for those assertions.

The kit never accepts an already-constructed store. Registering the store by hand inside
`ConfigureProvider` defeats the point: it would certify an instance the test author assembled rather than
the one your registration produces.

## What a green run covered

Some arms exercise an optional capability of the contract — an outbox or dead-letter store's administrative
facet, for example. An arm whose capability is unavailable used to return early, which every test runner
reports exactly as it reports an arm that ran and passed.

`ConformanceArmLedger` now records both: `Executed` lists the arms that ran their bodies, `Skipped` lists
those that did not with the capability and reason, and `Describe()` formats both. It is process-wide and
additive, so call `Reset()` before a run you intend to read.

```csharp
ConformanceArmLedger.Reset();
// ... run your derived kit ...
Console.WriteLine(ConformanceArmLedger.Describe());
```

A run that was green before may now report skips. That is not a regression: the coverage is unchanged and
the reporting no longer overstates it. The ledger records; you decide what an unverified arm means. Override
`ConformanceTestKit.OnArmSkipped` to surface skips in your runner (`Assert.Skip` on xUnit v3, `Assert.Ignore`
on NUnit) or to throw, which certifies that every capability your component provides was reached.

**If your store is decorated:** both outbox and dead-letter kits discover optional capabilities through
`GetService(Type)`. Forward the selected capability through your decorator so the kit exercises the actual
provider facet. `DeadLetterStoreConformanceTestKit.ResolveAdminFacet` is overridable for custom facet
resolution; its default already uses the store's `GetService(Type)`.

### Implement a fencing-token provider fixture

`FencingTokenProviderConformanceTestKit` checks the token-provider contract independently of leader-election lifecycle tests. Derive directly from it. The kit has no xUnit or Shouldly dependency: call `InitializeAsync`/`DisposeAsync` from your runner’s per-test lifecycle (xUnit v3 adapters implement `IAsyncLifetime`), override `CreateProviderAsync` to resolve a real provider, and override `CleanupAsync` to dispose only resources owned by that fixture. Every case gets a fresh resource ID. Override `TestCancellationToken` with your runner’s token; the default is `CancellationToken.None`.

The complete xUnit scaffold is [the in-memory provider suite](../../../tests/unit/Excalibur.LeaderElection.Tests/InMemory/InMemoryFencingTokenProviderConformanceShould.cs). It shows public registration, disposal, direct inheritance, all nine thin method wrappers, the six explicit never-issued theory values and the normal `ConformanceSuite_ShouldWireEveryArm` wrapper. Copy the complete wrapper set rather than a subset. The wiring check covers the eight no-argument arms; it does not certify parameterized data, so preserve the theory cases `long.MinValue`, `-1`, `0`, `1`, `2`, `long.MaxValue` and verify the runner discovers all fourteen behavioral cases. The wiring check adds its own fifteenth case.

The fixture hooks are:

| Hook | Consumer responsibility |
|---|---|
| `CreateProviderAsync()` | Return the actual selected `IFencingTokenProvider`, not a fake of the interface. |
| `CleanupAsync()` | Await disposal of fixture-owned resources after each case. |
| `TestCancellationToken` | Supply the runner's current case token. |
| `EstablishTokenAsync(resourceId)` | Default requires the actual provider to implement `IFencingTokenIssuer` and calls its real mint; native reader-only providers override it to drive a real leadership transition. |
| `ProviderMintsItsOwnTokens` | Set false only for native-counter providers, and provide separate substrate concurrency evidence. |
| `StoreCanHoldAnUnadvancedMark` / `CreateUnadvancedMarkAsync(resourceId)` | Declare and construct the actual reachable unadvanced artifact when supported; retain the provider's exact resource naming. |

The existing unadvanced/native-counter applicability branches remain explicit declarations, not evidence that an unavailable substrate scenario ran. Never-issued, unadvanced, established and unreadable states are distinct. Reads must return the exact established mark; validation admits the pinned incumbent and rejects a superseded token. Self-minting providers additionally race eight establishments. These are example-based provider contract controls, not an all-infrastructure or formal split-brain proof.

## Which testing kit do I reach for?

This package answers one question: **"Does my custom provider implementation obey the framework
contract?"** — use a conformance kit when you write a new backend for an infrastructure interface
(a new `IEventStore`, `IOutboxStore`, `ISagaStore`, `ILeaderElection`, and so on) and want to prove
it satisfies every contract requirement.

It is **not** a resilience/fault-injection kit. To prove your **handlers and sagas** stay idempotent
and eventually consistent under adverse transport conditions — duplicate delivery, message
reordering, broker disconnects, consumer crash-and-restart, slow consumers — use the chaos /
fault-injection test kit instead. The two kits share the same underlying test infrastructure and
container fixtures, so there is no duplicated surface: conformance verifies a provider matches a
contract; chaos verifies business logic survives faults.

## Documentation

Detailed guides live in the [Excalibur repository](https://github.com/TrigintaFaces/Excalibur). Each kit's
XML documentation describes the members it requires and the contract each arm asserts.

## License

This package is part of the Excalibur framework. The full licence text ships inside the package and is
shown on the package listing's License tab.
