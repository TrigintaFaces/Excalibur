# Excalibur.Testing.Conformance

Conformance test kits for Excalibur infrastructure implementations. Provides reusable abstract test suites that verify provider implementations conform to the expected contracts for IEventStore, ISnapshotStore, IOutboxStore, ISagaStore, and other infrastructure interfaces.

## Installation

```bash
dotnet add package Excalibur.Testing.Conformance
```

## Purpose

When implementing a custom provider (e.g., a new database backend for event sourcing), use these conformance test kits to verify your implementation meets all contract requirements. Each test kit provides a comprehensive set of tests covering happy paths, edge cases, and error handling.

## Available Test Kits

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

## Quick Start

```csharp
public class MyCustomEventStoreConformanceTests : EventStoreConformanceTestKit
{
    protected override IEventStore CreateEventStore()
    {
        // Return your custom implementation
        return new MyCustomEventStore(connectionString);
    }
}
```

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

See the [testing documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides.

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
