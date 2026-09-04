---
sidebar_position: 1
title: Handlers
description: Action handlers for commands and queries, plus event handlers for pub-sub
---

# Handlers

Dispatch provides two types of handlers: **action handlers** for request/response patterns and **event handlers** for pub-sub notifications.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [getting started](./getting-started/index.md) and [dependency injection](./core-concepts/dependency-injection.md)

## Action Handlers

Action handlers process actions (commands and queries) dispatched through the pipeline.

### Commands (No Return Value)

Use `IActionHandler<TAction>` for commands that don't return data:

```csharp
using Excalibur.Dispatch.Delivery;

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
using Excalibur.Dispatch.Delivery;

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
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

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

        // TResponse inferred from IDispatchAction<Order> - no type args needed
        var result = await _dispatcher.DispatchAsync(action, ct);

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

        // Publish the follow-on event. When this runs inside a dispatch
        // (e.g. invoked from a handler), DispatchAsync propagates
        // correlation/tenant and sets CausationId to the parent message,
        // preserving the causal chain. Handlers must remain idempotent.
        var @event = new OrderCompletedEvent(orderId, DateTime.UtcNow);
        await _dispatcher.DispatchAsync(@event, ct);
    }
}
```

## Context Propagation

When dispatching messages from within a handler, call `DispatchAsync` — it automatically maintains proper message lineage for distributed tracing and debugging by dispatching a child message.

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

        return result.IsSuccess
            ? Ok()
            : Problem(result.ErrorMessage, statusCode: result.ProblemDetails?.Status);
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

        // Nested dispatch: DispatchAsync auto-childs for proper context chaining
        await _dispatcher.DispatchAsync(
            new ValidateInventoryAction(order.Id, action.Items), ct);
    }
}
```

:::caution Do not map a failed result to 400

A failed `IMessageResult` means your handler **ran** and reported a failure — not, by default, that
the caller sent a bad request. Hard-coding `BadRequest(...)` reports a server-side fault to the
caller as their own mistake, so they retry a request that can never succeed, or abandon one that
would have.

`result.ProblemDetails.Status` carries the status the framework determined for that failure. With
the pipeline's exception mapping configured (`UseExceptionMapping()`), a validation failure arrives
as **400** and an authorization failure as **403**; a handler that threw with nothing mapping it
arrives as **500**. When no status was determined, `ProblemDetails` is `null` and `Problem(...)`
falls back to 500 — the safe direction, and never the caller's fault by accident.
:::

### What Gets Propagated

When called from within a handler, `DispatchAsync` automatically creates a child context that:

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

### How `DispatchAsync` Behaves by Context

There is a single `DispatchAsync` method whose behavior depends on whether an ambient context exists:

| Call Site | Behavior |
|-----------|----------|
| Top level (no ambient context) | Creates a fresh root context |
| Within a handler that takes a context (ambient context exists) | Dispatches a child message — fresh `MessageId`, `CausationId` set to the parent's `MessageId`, propagating correlation/tenant/identity |
| Within a handler that does **not** take a context | Creates a fresh root context — see below |

:::caution A nested dispatch only childs if your handler asked for the context

Childing needs a parent to child *from*, and the dispatcher only makes one available to
handlers that declared they want it. A handler declares that by taking `IMessageContext`
as a settable property, or by injecting `IMessageContextAccessor`.

A handler that declares neither runs on a faster path that establishes no ambient context
at all — so a nested `DispatchAsync(message, ct)` from inside it starts a **new root**, and
the causal chain stops there. Nothing throws; you simply get a root where you expected a
child.

If a handler dispatches follow-up messages and you want the causal chain, declare the
context:

```csharp
// Chain preserved: this handler asked for the context, so nested dispatches child from it.
public sealed class PlaceOrderHandler(IMessageContextAccessor context, IDispatcher dispatcher)
    : IActionHandler<PlaceOrder>
{
    public async Task HandleAsync(PlaceOrder action, CancellationToken cancellationToken)
    {
        await dispatcher.DispatchAsync(new ReserveStock(action.Sku), cancellationToken);
    }
}
```

Or pass the parent explicitly, which works from any handler:

```csharp
await dispatcher.DispatchAsync(childAction, parentContext, cancellationToken);
```

This is the trade that keeps the common path fast: a handler that never touches the
context does not pay for one being published.
:::

To deliberately reuse the parent context instead of childing, pass it explicitly to the `DispatchAsync(message, context, ct)` overload.

```csharp
// From a controller or service (top-level) - creates a fresh root context
await _dispatcher.DispatchAsync(action, cancellationToken);

// From within a handler (nested) - automatically establishes the causal chain
await _dispatcher.DispatchAsync(childAction, cancellationToken);
```

### Causal Chain Example

When `DispatchAsync` is called from within a handler, the message chain becomes traceable:

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
using Excalibur.Dispatch.Delivery;
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
using Excalibur.Dispatch.Delivery;

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
using Excalibur.Dispatch.Delivery;
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
using Excalibur.Dispatch.Delivery;

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
    private readonly IOrderRepository _repository;
    private readonly IValidator _validator;
    private readonly ILogger<ComplexHandler> _logger;

    public ComplexHandler(
        IOrderRepository repository,
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
