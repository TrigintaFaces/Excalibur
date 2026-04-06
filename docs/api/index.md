# Excalibur.Dispatch API Reference

Welcome to the API reference documentation for the Excalibur.Dispatch framework.

## Overview

Excalibur.Dispatch is a comprehensive .NET framework providing:

- **Message Dispatching** - Alternative to MediatR with middleware pipelines
- **Event Sourcing** - Patterns for aggregate persistence and event stores
- **Outbox Pattern** - Reliable messaging with at-least-once delivery
- **Domain-Driven Design** - Building blocks for DDD implementations
- **Compliance** - Encryption, audit logging, and data protection

## Framework Packages

### Dispatch (Messaging Framework)

The messaging framework handles **how messages flow through the system**.

| Package | Description |
|---------|-------------|
| [Dispatch](api-dispatch/Dispatch.html) | Core dispatcher, pipelines, middleware |
| [Excalibur.Dispatch.Abstractions](api-dispatch/Excalibur.Dispatch.Abstractions.html) | IDomainEvent, IIntegrationEvent, serialization |
| [Excalibur.Dispatch.Compliance](api-dispatch/Excalibur.Dispatch.Compliance.html) | Encryption, data protection |
| [Excalibur.Dispatch.Observability](api-dispatch/Excalibur.Dispatch.Observability.html) | OpenTelemetry metrics, tracing |
| [Excalibur.Dispatch.Caching](api-dispatch/Excalibur.Dispatch.Caching.html) | Caching infrastructure |
| [Dispatch.EventSourcing](api-dispatch/Dispatch.EventSourcing.html) | Event sourcing patterns |

### Excalibur (Application Framework)

The application framework handles **what gets persisted and domain modeling**.

| Package | Description |
|---------|-------------|
| [Excalibur.Domain](api-excalibur/Excalibur.Domain.html) | Aggregates, entities, domain building blocks |
| [Excalibur.Data.Abstractions](api-excalibur/Excalibur.Data.Abstractions.html) | IDataRequest, IDb, data access patterns |
| [Excalibur.EventSourcing](api-excalibur/Excalibur.EventSourcing.html) | Event stores, repositories, snapshots |
| [Excalibur.EventSourcing.SqlServer](api-excalibur/Excalibur.EventSourcing.SqlServer.html) | SQL Server event store |
| [Excalibur.Saga](api-excalibur/Excalibur.Saga.html) | Saga/Process manager abstractions |

## Key Interfaces

### Messaging

- `IDispatcher` - Message dispatch orchestration
- `IDispatchMiddleware` - Pipeline middleware
- `IDomainEvent` - Domain event contract
- `IIntegrationEvent` - Integration event contract

### Event Sourcing

- `IAggregateRoot` - Aggregate root interface
- `IEventStore` - Event persistence
- `ISnapshotStore` - Snapshot persistence
- `IProjection` - Read model projections

### Data Access

- `IDataRequest<TResult>` - Type-safe data requests
- `IPersistenceProvider` - Database connection factory
- `IEventSourcedRepository<T>` - Event-sourced aggregate persistence

### Observability

- `IDispatchTelemetryProvider` - Telemetry provider
- `IDispatchMetrics` - Metrics collection
- `ICircuitBreakerMetrics` - Circuit breaker metrics

## Generating Documentation

The API reference is generated using DocFX from XML documentation comments.

### Prerequisites

```bash
dotnet tool install -g docfx
```

### Build Documentation

```bash
cd docs/api
docfx docfx.json
```

### Serve Locally

```bash
docfx docfx.json --serve
```

## See Also

- [Developer Guides](../guides/README.md) - Step-by-step tutorials
- [Architecture Decisions](../../management/architecture/) - ADRs
- [Operations Runbooks](../operations/README.md) - Operational guidance
