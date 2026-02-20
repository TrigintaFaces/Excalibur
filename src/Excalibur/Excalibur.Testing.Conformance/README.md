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

## Documentation

See the [testing documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides.

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
