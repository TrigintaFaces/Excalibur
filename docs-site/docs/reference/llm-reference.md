---
sidebar_position: 1
title: LLM Quick Reference
description: Compact reference for LLM coding agents helping developers use Excalibur.Dispatch
---

# Excalibur LLM Quick Reference

:::tip For AI Coding Agents
This page is optimized for LLM coding agents (Cursor, Copilot, Claude Code, etc.). It provides the essential information needed to help developers use this framework without reading all documentation pages. For the full docs, see the sidebar navigation.
:::

**Excalibur** is a .NET 8+ NuGet package framework (75+ packages) with focused package families:

- **`Excalibur.Dispatch.*`** — Messaging (MediatR alternative): dispatching, pipelines, middleware, transports
- **`Excalibur.Domain`** — Domain modeling (DDD): aggregates, entities, value objects
- **`Excalibur.EventSourcing.*`** — Persistence: event stores, snapshots, repositories
- **`Excalibur.Saga.*`** — Workflows: sagas, process managers, outbox

`Excalibur.Dispatch` is the foundation. All other families depend on it, but it depends on none of them.

## Package Map

| Install This | When You Need |
|---|---|
| `Excalibur.Dispatch` + `Excalibur.Dispatch.Abstractions` | In-process messaging (MediatR replacement) |
| `Excalibur.Dispatch.Transport.RabbitMQ` | RabbitMQ transport |
| `Excalibur.Dispatch.Transport.Kafka` | Kafka transport |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Azure Service Bus transport |
| `Excalibur.Dispatch.Transport.AwsSqs` | AWS SQS transport |
| `Excalibur.Dispatch.Transport.GooglePubSub` | Google Pub/Sub transport |
| `Excalibur.Domain` | Aggregates, entities, value objects (DDD building blocks) |
| `Excalibur.Application` | CQRS base classes: `CommandBase`, `QueryBase`, `ICommand`, `IQuery<T>` |
| `Excalibur.EventSourcing` + `Excalibur.EventSourcing.Abstractions` | Event store, repository, snapshots, upcasting |
| `Excalibur.EventSourcing.SqlServer` | SQL Server event store + snapshot store |
| `Excalibur.Outbox` | Transactional outbox pattern |
| `Excalibur.Saga` | Saga / process manager abstractions |
| `Excalibur.Data` + `Excalibur.Data.Abstractions` | Data access: `IDb`, `IDataRequest`, `IUnitOfWork` |
| `Excalibur.Dispatch.Observability` | OpenTelemetry metrics and tracing |
| `Excalibur.Dispatch.Resilience.Polly` | Circuit breakers, retries (Polly v8) |
| `Excalibur.Dispatch.Security` | Authentication, encryption, signing |
| `Excalibur.Dispatch.Testing` | Test utilities and fakes |

## Dispatch Core Types

All message types are in namespace `Excalibur.Dispatch.Abstractions`. Handler types are in `Excalibur.Dispatch.Abstractions.Delivery`.

| Type | Kind | Key Members |
|---|---|---|
| `IDispatchMessage` | Marker interface | Base for all messages. No members. |
| `IDispatchAction` | Command (no return) | Extends `IDispatchMessage` |
| `IDispatchAction<TResponse>` | Query (returns T) | Extends `IDispatchAction` |
| `IDispatchEvent` | Event (pub/sub) | Extends `IDispatchMessage`. Multiple handlers allowed. |
| `IDispatchDocument` | Document/batch | Extends `IDispatchMessage`. For ETL, bulk processing. |
| `IDomainEvent` | Domain event | Extends `IDispatchEvent`. Has `EventId`, `AggregateId`, `Version`, `OccurredAt`, `EventType`, `Metadata`. |
| `DomainEvent` | Base record | Abstract record implementing `IDomainEvent` with auto-generated `EventId` (UUID v7). Namespace: `Excalibur.Dispatch.Abstractions`. |
| `IActionHandler<TAction>` | Command handler | 1 method: `Task HandleAsync(TAction action, CancellationToken cancellationToken)` |
| `IActionHandler<TAction, TResult>` | Query handler | 1 method: `Task<TResult> HandleAsync(TAction action, CancellationToken cancellationToken)` |
| `IEventHandler<TEvent>` | Event handler | 1 method: `Task HandleAsync(TEvent eventMessage, CancellationToken cancellationToken)` |
| `IDispatcher` | Central dispatcher | 6 dispatch methods + `ServiceProvider` property |
| `IMessageResult` | Result wrapper | `Succeeded`, `IsSuccess`, `ErrorMessage`, `ProblemDetails` |
| `IMessageResult<T>` | Typed result | Adds `ReturnValue` property |

## Excalibur Domain Types

Domain building blocks are in namespace `Excalibur.Domain.Model`.

| Type | Kind | Key Members |
|---|---|---|
| `AggregateRoot<TKey>` | Aggregate base | `Id`, `Version`, `ETag`, `RaiseEvent(IDomainEvent)`, abstract `ApplyEventInternal(IDomainEvent)`, `LoadFromHistory(IEnumerable<IDomainEvent>)`, `LoadFromSnapshot(ISnapshot)`, `GetUncommittedEvents()`, `MarkEventsAsCommitted()` |
| `AggregateRoot` | String-key shorthand | Extends `AggregateRoot<string>` |
| `EntityBase<TKey>` | Entity base | Abstract `Key` property, equality by type + key |
| `EntityBase` | String-key shorthand | Extends `EntityBase<string>` |
| `ValueObjectBase` | Value object base | Abstract `GetEqualityComponents()`, component-based equality, `==`/`!=` operators |
| `DomainEventBase` | Domain event base | Abstract record. Auto-generates `EventId` (GUID), `OccurredAt` (UTC), `EventType` (class name). Override `AggregateId`. Namespace: `Excalibur.Domain.Model`. |
| `ISnapshot` | Snapshot interface | `SnapshotId`, `AggregateId`, `AggregateType`, `Version`, `CreatedAt`, `Data` |

:::note Two DomainEvent base types
- `DomainEvent` in `Excalibur.Dispatch.Abstractions` — constructor takes `(aggregateId, version)`, uses UUID v7 EventId
- `DomainEventBase` in `Excalibur.Domain.Model` — parameterless, override `AggregateId` property, uses standard GUID EventId

Both implement `IDomainEvent`. Use whichever fits your project.
:::

## Excalibur CQRS Types

CQRS types are in `Excalibur.Application`. They extend Dispatch's `IDispatchAction` with richer metadata.

| Type | Kind | Key Members |
|---|---|---|
| `ICommand` | Command interface | Extends `IDispatchAction`. Has `Id`, `MessageId`, `CorrelationId`, `TenantId`, `TransactionBehavior`. |
| `ICommand<TResult>` | Command with result | Extends `ICommand` + `IDispatchAction<TResult>` |
| `CommandBase` | Command base class | Constructor: `CommandBase(Guid correlationId, string? tenantId = null)`. Provides `Id`, `MessageId`, `Kind`, `Headers`, transaction control. |
| `CommandBase<TResponse>` | Command with result | Extends `CommandBase`, implements `ICommand<TResponse>` |
| `IQuery<TResult>` | Query interface | Extends `IDispatchAction<TResult>`. Has same metadata as `ICommand`. |
| `QueryBase<TResult>` | Query base class | Constructor: `QueryBase(Guid correlationId, string? tenantId = null)` |
| `ICommandHandler<TCommand>` | Command handler | Extends `IActionHandler<TCommand>` where `TCommand : ICommand` |
| `ICommandHandler<TCommand, TResult>` | Command+result handler | Extends `IActionHandler<TCommand, TResult>` |
| `IQueryHandler<TQuery, TResult>` | Query handler | Extends `IActionHandler<TQuery, TResult>` where `TQuery : IQuery<TResult>` |

## Excalibur Event Sourcing Types

Event sourcing types are in `Excalibur.EventSourcing.Abstractions`.

| Type | Kind | Key Members |
|---|---|---|
| `IEventStore` | Event persistence | 5 methods: `LoadAsync` (2 overloads), `AppendAsync` (optimistic concurrency via `expectedVersion`), `GetUndispatchedEventsAsync`, `MarkEventAsDispatchedAsync` |
| `IEventSourcedRepository<TAggregate, TKey>` | Repository | 7 methods: `GetByIdAsync`, `SaveAsync` (2 overloads: with/without ETag), `ExistsAsync`, `DeleteAsync`, `QueryAsync<TQuery>`, `FindAsync<TQuery>` |
| `IEventSourcedRepository<TAggregate>` | String-key shorthand | Extends `IEventSourcedRepository<TAggregate, string>` |
| `ISnapshotStore` | Snapshot persistence | `GetLatestAsync`, `SaveAsync` |
| `ISnapshotStrategy` | When to snapshot | `ShouldCreateSnapshot(aggregate)` |

## Message Type Decision Tree

```
Need to DO something (no result)?    → IDispatchAction  (or ICommand for CQRS)
Need to DO + GET a result?           → IDispatchAction<T> (or IQuery<T> / ICommand<T>)
Something HAPPENED (pub/sub)?        → IDispatchEvent
Domain event with sourcing metadata? → IDomainEvent (extends IDispatchEvent)
Processing documents/batches?        → IDispatchDocument
```

## DI Registration

### Dispatch (messaging only)

```csharp
// Minimal: register pipeline + handlers
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
```

The `AddDispatch(Action<IDispatchBuilder>?)` overload creates the pipeline, discovers handlers, and builds the configuration. Without a builder parameter, `AddDispatch()` scans the calling assembly.

### Excalibur Event Sourcing

```csharp
builder.Services.AddExcaliburEventSourcing(es =>
{
    es.AddRepository<OrderAggregate, Guid>(key => new OrderAggregate(key))
      .UseIntervalSnapshots(100)
      .UseEventStore<SqlServerEventStore>();
});
```

The `IEventSourcingBuilder` provides fluent configuration: `AddRepository`, `UseEventStore`, `UseIntervalSnapshots`, `UseTimeBasedSnapshots`, `UseNoSnapshots`, `UseEventSerializer`, `UseOutboxStore`, `AddUpcastingPipeline`, `AddSnapshotUpgrading`.

### Excalibur Outbox

```csharp
builder.Services.AddExcaliburOutbox(outbox =>
{
    // Configure outbox via IOutboxBuilder
});
```

### Excalibur Saga

```csharp
builder.Services.AddExcaliburSaga(saga =>
{
    // Configure saga via ISagaBuilder
});
```

### Excalibur Data Services

```csharp
builder.Services.AddExcaliburDataServices(); // Dapper + JSON config
builder.Services.AddExcaliburDataServicesWithPersistence(configuration); // + persistence providers
```

## Handler Patterns

### Dispatch Handlers (lightweight)

```csharp
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

// 1. Command (no return value)
public record CreateOrder(string CustomerId, List<string> Items) : IDispatchAction;

public class CreateOrderHandler : IActionHandler<CreateOrder>
{
    public async Task HandleAsync(CreateOrder action, CancellationToken cancellationToken)
    {
        // Process command...
    }
}

// 2. Query (with return value)
public record GetOrder(Guid OrderId) : IDispatchAction<Order>;

public class GetOrderHandler : IActionHandler<GetOrder, Order>
{
    public async Task<Order> HandleAsync(GetOrder action, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(action.OrderId, cancellationToken);
    }
}

// 3. Event (multiple handlers, pub/sub)
public record OrderPlaced(string OrderId) : IDispatchEvent;

public class OrderPlacedHandler : IEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced eventMessage, CancellationToken cancellationToken)
    {
        // React to event...
        return Task.CompletedTask;
    }
}
```

### CQRS Handlers (rich metadata)

```csharp
using Excalibur.Application.Requests.Commands;
using Excalibur.Application.Requests.Queries;

// Command with CQRS base class (adds CorrelationId, TenantId, transaction control)
public class PlaceOrder : CommandBase
{
    public PlaceOrder(string customerId, Guid correlationId)
        : base(correlationId) => CustomerId = customerId;

    public string CustomerId { get; }
}

public class PlaceOrderHandler : ICommandHandler<PlaceOrder>
{
    public async Task HandleAsync(PlaceOrder action, CancellationToken cancellationToken)
    {
        // ICommandHandler<T> extends IActionHandler<T> — same signature
    }
}

// Query with CQRS base class
public class GetOrderById : QueryBase<Order>
{
    public GetOrderById(Guid orderId, Guid correlationId)
        : base(correlationId) => OrderId = orderId;

    public Guid OrderId { get; }
}

public class GetOrderByIdHandler : IQueryHandler<GetOrderById, Order>
{
    public async Task<Order> HandleAsync(GetOrderById action, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(action.OrderId, cancellationToken);
    }
}
```

## Dispatching Messages

```csharp
// Inject IDispatcher via DI
private readonly IDispatcher _dispatcher;

// Context-less dispatch (context created automatically)
// Extension method from DispatcherContextExtensions (requires Excalibur.Dispatch package)
var result = await _dispatcher.DispatchAsync(new CreateOrder("cust-1", items), cancellationToken);

// Query dispatch
var result = await _dispatcher.DispatchAsync<GetOrder, Order>(
    new GetOrder(orderId), cancellationToken);

// Check result
if (result.IsSuccess)
    return Ok(result.ReturnValue);
else
    return BadRequest(result.ErrorMessage);
```

The 2-parameter `DispatchAsync(message, cancellationToken)` extension methods use ambient context (`MessageContextHolder.Current`) or create a new one automatically. These are the recommended dispatch methods for most use cases.

## Aggregate + Event Sourcing Pattern

```csharp
using Excalibur.Domain.Model;
using Excalibur.Dispatch.Abstractions;

// 1. Define domain events
public record OrderCreated(string OrderId, string CustomerId) : DomainEventBase
{
    public override string AggregateId => OrderId;
}

public record ItemAdded(string OrderId, string ItemId) : DomainEventBase
{
    public override string AggregateId => OrderId;
}

// 2. Define the aggregate
public class OrderAggregate : AggregateRoot
{
    public string CustomerId { get; private set; } = string.Empty;
    public List<string> Items { get; } = [];

    public OrderAggregate(string id) => Id = id;

    // Commands raise events
    public void Create(string customerId)
    {
        RaiseEvent(new OrderCreated(Id, customerId));
    }

    public void AddItem(string itemId)
    {
        RaiseEvent(new ItemAdded(Id, itemId));
    }

    // Pattern-matching event application (no reflection, O(1))
    protected override void ApplyEventInternal(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case OrderCreated e:
                CustomerId = e.CustomerId;
                break;
            case ItemAdded e:
                Items.Add(e.ItemId);
                break;
        }
    }
}

// 3. Use in a handler
public class PlaceOrderHandler : IActionHandler<PlaceOrder>
{
    private readonly IEventSourcedRepository<OrderAggregate> _repository;

    public PlaceOrderHandler(IEventSourcedRepository<OrderAggregate> repository)
        => _repository = repository;

    public async Task HandleAsync(PlaceOrder action, CancellationToken cancellationToken)
    {
        var order = new OrderAggregate(Guid.NewGuid().ToString());
        order.Create(action.CustomerId);

        await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);
    }
}
```

Key points:
- `RaiseEvent()` records events AND applies them via `ApplyEventInternal`
- `LoadFromHistory()` replays events to rebuild state (called by repository)
- `LoadFromSnapshot()` restores from a snapshot, then replays subsequent events
- `GetUncommittedEvents()` returns events raised since last save
- `MarkEventsAsCommitted()` clears the uncommitted list after persistence

## Common LLM Mistakes

1. **Wrong handler namespace**: Handlers are in `Excalibur.Dispatch.Abstractions.Delivery`, not `Excalibur.Dispatch.Abstractions`.
2. **Optional CancellationToken**: Framework requires `CancellationToken cancellationToken` as required parameter (no `= default`).
3. **Missing ConfigureAwait**: Library code must use `await task.ConfigureAwait(false)`.
4. **Confusing Action vs Event**: `IDispatchAction` routes to exactly 1 handler. `IDispatchEvent` supports multiple handlers (pub/sub).
5. **Explicit context creation**: Most code should use the 2-parameter `DispatchAsync(message, ct)` extensions. Only create `IMessageContext` explicitly when you need to set correlation IDs or tenant context.
6. **Wrong DomainEvent constructor**: `DomainEvent(aggregateId, version)` takes the aggregate ID and version, not event-specific data. Put event data in your record's own properties.
7. **Confusing the two DomainEvent bases**: `DomainEvent` (Dispatch.Abstractions, constructor args) vs `DomainEventBase` (Domain.Model, property overrides). Both implement `IDomainEvent`.
8. **No EntityFramework**: This framework uses **Dapper** for SQL, not EF Core. Never suggest EF migrations or DbContext.
9. **Blocking async in Dispose**: Use `IAsyncDisposable` with `DisposeAsync()`, never `task.GetAwaiter().GetResult()`.
10. **Missing aggregate factory**: `AddRepository` requires a factory function: `es.AddRepository<MyAggregate>(key => new MyAggregate(key))`.

## Deep Dive Links

- [Getting Started](../getting-started/index.md) - Full 5-minute quickstart
- [Core Concepts](../core-concepts/index.md) - Actions, handlers, context, results, DI, configuration
- [Handlers](../handlers.md) - Handler patterns deep dive
- [Pipeline & Middleware](../pipeline/index.md) - Request pipeline and middleware
- [Transports](../transports/index.md) - Multi-transport messaging
- [Event Sourcing](../event-sourcing/index.md) - Event stores, repositories, snapshots, projections
- [Domain Modeling](../domain-modeling/index.md) - Aggregates, entities, value objects
- [Data Providers](../data-providers/index.md) - SQL Server, Postgres, CosmosDB, MongoDB
- [Patterns](../patterns/index.md) - Outbox, inbox, dead letter, claim check
- [Observability](../observability/index.md) - Metrics, tracing, health checks
- [Security](../security/index.md) - Authentication, encryption, audit logging
- [Testing](../testing/index.md) - Test utilities and strategies
- [Deployment](../deployment/index.md) - ASP.NET Core, Docker, K8s, serverless
- [Migration from MediatR](../migration/from-mediatr.md) - Step-by-step migration guide
