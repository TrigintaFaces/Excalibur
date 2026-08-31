# Excalibur.Testing.Conformance

Conformance test kits for Excalibur infrastructure implementations. Provides reusable abstract test suites that verify provider implementations conform to the expected contracts for IEventStore, ISnapshotStore, IOutboxStore, ISagaStore, and other infrastructure interfaces.

## Installation

```bash
dotnet add package Excalibur.Testing.Conformance
```

## Purpose

When implementing a custom provider (e.g., a new database backend for event sourcing), use these conformance test kits to verify your implementation meets all contract requirements. Each test kit provides a comprehensive set of tests covering happy paths, edge cases, and error handling.

## Available Test Kits

42 kits ship in this package. The most commonly implemented:

| Test Kit | Interface Under Test |
|----------|---------------------|
| `EventStoreConformanceTestKit` | `IEventStore` |
| `SnapshotStoreConformanceTestKit` | `ISnapshotStore` |
| `OutboxStoreConformanceTestKit` | `IOutboxStore` |
| `InboxStoreConformanceTestKit` | `IInboxStore` |
| `SagaStoreConformanceTestKit` | `ISagaStore` |
| `DeadLetterStoreConformanceTestKit` | `IDeadLetterStore` |
| `LeaderElectionConformanceTestKit` | `ILeaderElection` |
| `EncryptionProviderConformanceTestKit` | `IEncryptionProvider` |
| `AuditStoreConformanceTestKit` | `IAuditStore` |
| `TransportConformanceTestKit<TSender, TReceiver>` | transport sender/receiver pairs |
| `DbConformanceTestKit` | `IDb` |
| `PersistenceProviderConformanceTestKit` | persistence providers |

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
public class MyCustomEventStoreConformanceTests : EventStoreConformanceTestKit
{
    private readonly MyProviderFixture _fixture;

    // The only member you must implement. Call your shipped registration extension and nothing else.
    protected override void ConfigureProvider(IServiceCollection services) =>
        services.AddExcalibur(x => x.AddEventSourcing(es => es.UseMyProvider(_fixture.ConnectionString)));

    // Optional: override to reset state between test runs.
    protected override async Task CleanupAsync() => await _fixture.CleanupAsync();
}
```

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

**If your store is decorated:** the outbox kit discovers capabilities through `GetService(Type)`, which a
decorator forwards. `IDeadLetterStore` exposes no such method, so its kit resolves the admin facet from the
store's own type — sound for a store handed to the kit directly, unsound for a wrapped one. Override
`DeadLetterStoreConformanceTestKit.ResolveAdminFacet` to return the facet the wrapper holds; otherwise those
arms report as skips.

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
