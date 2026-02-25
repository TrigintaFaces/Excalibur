---
sidebar_position: 1
title: Handlers
description: Action handlers for commands and queries, plus event handlers for pub-sub
---

# Handlers

Dispatch provides two types of handlers: **action handlers** for request/response patterns and **event handlers** for pub-sub notifications.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Abstractions
  ```
- Familiarity with [getting started](./getting-started/index.md) and [dependency injection](./core-concepts/dependency-injection.md)

## Action Handlers

Action handlers process actions (commands and queries) dispatched through the pipeline.

### Commands (No Return Value)

Use `IActionHandler<TAction>` for commands that don't return data:

```csharp
using Excalibur.Dispatch.Abstractions.Delivery;

public record CreateOrderAction(string CustomerId, List<string> Items) : IDispatchAction;

public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IOrderRepository _repository;

    public CreateOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(
        CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = action.CustomerId,
            Items = action.Items,
            Status = OrderStatus.Created
        };

        await _repository.SaveAsync(order, cancellationToken);
    }
}
```

### Queries (With Return Value)

Use `IActionHandler<TAction, TResult>` for queries that return data:

```csharp
using Excalibur.Dispatch.Abstractions.Delivery;

public record GetOrderAction(Guid OrderId) : IDispatchAction<Order>;

public class GetOrderHandler : IActionHandler<GetOrderAction, Order>
{
    private readonly IOrderRepository _repository;

    public GetOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Order> HandleAsync(
        GetOrderAction action,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(action.OrderId, cancellationToken);
    }
}
```

## Event Handlers

Event handlers subscribe to domain events for pub-sub messaging. Multiple handlers can process the same event.

```csharp
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

public record OrderCreatedEvent(Guid OrderId, string CustomerId, DateTime CreatedAt)
    : IDispatchEvent;

public class SendOrderConfirmationHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IEmailService _emailService;

    public SendOrderConfirmationHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        await _emailService.SendOrderConfirmationAsync(
            @event.OrderId,
            @event.CustomerId,
            cancellationToken);
    }
}

public class UpdateInventoryHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IInventoryService _inventoryService;

    public UpdateInventoryHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        await _inventoryService.ReserveItemsAsync(
            @event.OrderId,
            cancellationToken);
    }
}
```

## Handler Registration

Register handlers during service configuration:

```csharp
// Auto-discover all handlers in an assembly (recommended)
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Or register from multiple assemblies
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(OrderHandler).Assembly);
    dispatch.AddHandlersFromAssembly(typeof(InventoryHandler).Assembly);
});
```

## Dispatching Messages

### Dispatching Actions

Dispatch manages message context automatically - no explicit context needed:

```csharp
public class OrderService
{
    private readonly IDispatcher _dispatcher;

    public OrderService(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task<Order> GetOrderAsync(Guid orderId, CancellationToken ct)
    {
        var action = new GetOrderAction(orderId);

        // Context-less dispatch - Dispatch creates context automatically
        var result = await _dispatcher.DispatchAsync<GetOrderAction, Order>(action, ct);

        if (result.IsSuccess)
            return result.ReturnValue;

        throw new OrderNotFoundException(orderId);
    }

    public async Task CreateOrderAsync(string customerId, List<string> items, CancellationToken ct)
    {
        var action = new CreateOrderAction(customerId, items);

        // Simple dispatch without explicit context
        var result = await _dispatcher.DispatchAsync(action, ct);

        if (!result.IsSuccess)
            throw new OrderCreationException(result.ErrorMessage);
    }
}
```

### Publishing Events

```csharp
public class OrderService
{
    private readonly IDispatcher _dispatcher;

    public async Task CompleteOrderAsync(Guid orderId, CancellationToken ct)
    {
        // ... order completion logic ...

        // Dispatch event to all handlers (context managed automatically)
        var @event = new OrderCompletedEvent(orderId, DateTime.UtcNow);
        await _dispatcher.DispatchAsync(@event, ct);
    }
}
```

## Context Propagation

When dispatching messages from within a handler, use `DispatchChildAsync` to maintain proper message lineage for distributed tracing and debugging.

### Top-Level vs Nested Dispatch

```csharp
public class OrderController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request, CancellationToken ct)
    {
        // Top-level dispatch: context created automatically
        var result = await _dispatcher.DispatchAsync(
            new CreateOrderAction(request.CustomerId, request.Items), ct);

        return result.IsSuccess ? Ok() : BadRequest(result.ErrorMessage);
    }
}

public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IDispatcher _dispatcher;
    private readonly IOrderRepository _repository;

    public CreateOrderHandler(IDispatcher dispatcher, IOrderRepository repository)
    {
        _dispatcher = dispatcher;
        _repository = repository;
    }

    public async Task HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        var order = new Order { Id = Guid.NewGuid(), CustomerId = action.CustomerId };
        await _repository.SaveAsync(order, ct);

        // Nested dispatch: use DispatchChildAsync for proper context chaining
        await _dispatcher.DispatchChildAsync(
            new ValidateInventoryAction(order.Id, action.Items), ct);
    }
}
```

### What Gets Propagated

`DispatchChildAsync` creates a child context that:

| Property | Behavior |
|----------|----------|
| `CorrelationId` | Copied from parent (maintains distributed trace) |
| `TenantId` | Copied from parent (multi-tenant isolation) |
| `UserId` | Copied from parent (audit trail) |
| `SessionId` | Copied from parent (message grouping) |
| `WorkflowId` | Copied from parent (saga orchestration) |
| `TraceParent` | Copied from parent (OpenTelemetry integration) |
| `Source` | Copied from parent (origin tracking) |
| `CausationId` | Set to parent's `MessageId` (causal chain) |
| `MessageId` | New unique ID generated |

### When to Use Each Method

| Method | Use When |
|--------|----------|
| `DispatchAsync` | Top-level dispatch (controllers, background services, external triggers) |
| `DispatchChildAsync` | Nested dispatch from within a handler |

```csharp
// From a controller or service (top-level)
await _dispatcher.DispatchAsync(action, cancellationToken);

// From within a handler (nested) - establishes causal chain
await _dispatcher.DispatchChildAsync(childAction, cancellationToken);
```

### Causal Chain Example

When `DispatchChildAsync` is used, the message chain becomes traceable:

```
CreateOrderAction (MessageId: "msg-001")
    └── ValidateInventoryAction (MessageId: "msg-002", CausationId: "msg-001")
            └── ReserveStockAction (MessageId: "msg-003", CausationId: "msg-002")
```

All three messages share the same `CorrelationId`, making it easy to trace the entire business transaction in logs and monitoring tools.

## Streaming Handlers

Dispatch provides specialized handlers for processing large documents and data streams efficiently using `IAsyncEnumerable<T>`. These handlers enable memory-efficient processing without loading entire datasets into memory.

### Document-to-Stream Handler

Use `IStreamingDocumentHandler<TDocument, TOutput>` when a single document produces multiple outputs:

```csharp
using Excalibur.Dispatch.Abstractions.Delivery;
using System.Runtime.CompilerServices;

public record CsvDocument(string Content) : IDispatchDocument;

public class CsvRowHandler : IStreamingDocumentHandler<CsvDocument, DataRow>
{
    public async IAsyncEnumerable<DataRow> HandleAsync(
        CsvDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var line in document.Content.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return ParseRow(line);
        }
    }

    private DataRow ParseRow(string line) => new DataRow(line.Split(','));
}
```

**Use cases:**
- CSV/JSON parsing into records
- Document splitting into pages
- Entity extraction from text
- Report row generation

### Stream Consumer Handler

Use `IStreamConsumerHandler<TDocument>` to consume an incoming stream of documents:

```csharp
using Excalibur.Dispatch.Abstractions.Delivery;

public class BatchImportHandler : IStreamConsumerHandler<DataRow>
{
    private readonly IDatabase _database;

    public BatchImportHandler(IDatabase database) => _database = database;

    public async Task HandleAsync(
        IAsyncEnumerable<DataRow> documents,
        CancellationToken cancellationToken)
    {
        var batch = new List<DataRow>();
        await foreach (var row in documents.WithCancellation(cancellationToken))
        {
            batch.Add(row);
            if (batch.Count >= 1000)
            {
                await _database.BulkInsertAsync(batch, cancellationToken);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            await _database.BulkInsertAsync(batch, cancellationToken);
        }
    }
}
```

**Use cases:**
- Batch imports with buffering
- ETL sinks writing to storage
- Message queue consumers
- Aggregation pipelines

### Stream Transform Handler

Use `IStreamTransformHandler<TInput, TOutput>` for stream-to-stream transformations:

```csharp
using Excalibur.Dispatch.Abstractions.Delivery;
using System.Runtime.CompilerServices;

public class EnrichmentHandler : IStreamTransformHandler<CustomerRecord, EnrichedCustomer>
{
    private readonly IExternalService _service;

    public EnrichmentHandler(IExternalService service) => _service = service;

    public async IAsyncEnumerable<EnrichedCustomer> HandleAsync(
        IAsyncEnumerable<CustomerRecord> input,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var record in input.WithCancellation(cancellationToken))
        {
            var enriched = await _service.EnrichAsync(record, cancellationToken);
            yield return enriched;
        }
    }
}
```

**Use cases:**
- Data enrichment from external sources
- Format conversion
- Filtering and aggregation
- Batching and flattening

### Progress Document Handler

Use `IProgressDocumentHandler<TDocument>` for long-running operations with progress reporting:

```csharp
using Excalibur.Dispatch.Abstractions.Delivery;

public class PdfExportHandler : IProgressDocumentHandler<ExportDocument>
{
    public async Task HandleAsync(
        ExportDocument document,
        IProgress<DocumentProgress> progress,
        CancellationToken cancellationToken)
    {
        var pages = document.GetPages();
        var total = pages.Count;

        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessPageAsync(pages[i], cancellationToken);

            progress.Report(DocumentProgress.FromItems(
                itemsProcessed: i + 1,
                totalItems: total,
                currentPhase: $"Processing page {i + 1} of {total}"));
        }

        progress.Report(DocumentProgress.Completed(total, "Export complete"));
    }
}
```

**Use cases:**
- Large file processing
- Multi-step transformations
- Report generation
- Data migrations

### Streaming Handler Summary

| Interface | Input | Output | Purpose |
|-----------|-------|--------|---------|
| `IStreamingDocumentHandler<TDoc, TOut>` | Single document | `IAsyncEnumerable<TOut>` | Document to stream |
| `IStreamConsumerHandler<TDoc>` | `IAsyncEnumerable<TDoc>` | `Task` | Consume stream |
| `IStreamTransformHandler<TIn, TOut>` | `IAsyncEnumerable<TIn>` | `IAsyncEnumerable<TOut>` | Transform stream |
| `IProgressDocumentHandler<TDoc>` | Document + `IProgress<T>` | `Task` | Progress reporting |

## Handler Interfaces Summary

Dispatch provides two tiers of handler interfaces:

### Recommended Handlers (Application Code)

These handlers return your business types directly. The framework automatically wraps results in `IMessageResult`:

| Interface | Purpose | Return | Framework Wraps To |
|-----------|---------|--------|-------------------|
| `IActionHandler<TAction>` | Commands without return value | `Task` | `IMessageResult` |
| `IActionHandler<TAction, TResult>` | Queries with return value | `Task<TResult>` | `IMessageResult<TResult>` |
| `IEventHandler<TEvent>` | Pub-sub event subscribers | `Task` | `IMessageResult` |
| `IDocumentHandler<TDocument>` | Document processing | `Task` | `IMessageResult` |

### Advanced Handler (Infrastructure/Power Users)

This handler gives you direct control over `IMessageResult`:

| Interface | Purpose | Return |
|-----------|---------|--------|
| `IDispatchHandler<TMessage>` | Full control over result | `Task<IMessageResult>` |

Use `IDispatchHandler` when you need to:
- Return `MessageResult.SuccessFromCache()` with `CacheHit = true`
- Set `ValidationResult` or `AuthorizationResult` on success
- Return failure without throwing an exception
- Implement batch processing (`IBatchableHandler`)

### Streaming Handlers

| Interface | Input | Output | Purpose |
|-----------|-------|--------|---------|
| `IStreamingDocumentHandler<TDoc, TOut>` | Single document | `IAsyncEnumerable<TOut>` | Document to stream |
| `IStreamConsumerHandler<TDoc>` | `IAsyncEnumerable<TDoc>` | `Task` | Consume stream |
| `IStreamTransformHandler<TIn, TOut>` | `IAsyncEnumerable<TIn>` | `IAsyncEnumerable<TOut>` | Transform stream |
| `IProgressDocumentHandler<TDoc>` | Document + `IProgress<T>` | `Task` | Progress reporting |

## Message Interfaces Summary

| Interface | Purpose |
|-----------|---------|
| `IDispatchAction` | Marker for commands (no return) |
| `IDispatchAction<TResult>` | Marker for queries (with return) |
| `IDispatchEvent` | Events for pub-sub dispatch |
| `IDomainEvent` | Domain events with event sourcing metadata (extends `IDispatchEvent`) |

## Best Practices

### Keep Handlers Focused

Each handler should do one thing well:

```csharp
// Good: Single responsibility
public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    public async Task HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        // Only creates the order
    }
}

// Good: Separate handler for side effects
public class SendOrderConfirmationHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Only sends confirmation
    }
}
```

### Use Constructor Injection

Handlers support full dependency injection:

```csharp
public class ComplexHandler : IActionHandler<ComplexAction>
{
    private readonly IRepository _repository;
    private readonly IValidator _validator;
    private readonly ILogger<ComplexHandler> _logger;

    public ComplexHandler(
        IRepository repository,
        IValidator validator,
        ILogger<ComplexHandler> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async Task HandleAsync(ComplexAction action, CancellationToken ct)
    {
        _logger.LogInformation("Processing {ActionType}", action.GetType().Name);
        // ...
    }
}
```

### Handle Cancellation

Always respect the cancellation token:

```csharp
public async Task HandleAsync(LongRunningAction action, CancellationToken ct)
{
    foreach (var item in action.Items)
    {
        ct.ThrowIfCancellationRequested();
        await ProcessItemAsync(item, ct);
    }
}
```

## See Also

- [Pipeline](./pipeline/index.md) - Add middleware for cross-cutting concerns
- [Middleware](./middleware/index.md) - Built-in and custom middleware components
- [Event Sourcing](./event-sourcing/index.md) - Build event-sourced aggregates with handlers
