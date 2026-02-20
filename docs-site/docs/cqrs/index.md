---
sidebar_position: 5
title: CQRS
description: Command Query Responsibility Segregation with Excalibur
---

# CQRS (Command Query Responsibility Segregation)

CQRS separates read and write operations into distinct models. Combined with event sourcing, this enables optimized query models and scalable architectures.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.EventSourcing
  ```
- Familiarity with [actions and handlers](../core-concepts/actions-and-handlers.md) and [event sourcing](../event-sourcing/index.md)

## What is CQRS?

```
Traditional Approach:
┌─────────────────────────────────────────────────┐
│              Single Model                       │
│  ┌─────────────────────────────────────────┐   │
│  │  Read + Write through same structures    │   │
│  │  Same database, same tables              │   │
│  └─────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘

CQRS Approach:
┌─────────────────────┐    ┌─────────────────────┐
│   Write Model       │    │   Read Model        │
│   (Commands)        │    │   (Queries)         │
│ ┌─────────────────┐ │    │ ┌─────────────────┐ │
│ │ Domain Aggregates│ │    │ │ Denormalized    │ │
│ │ Business Logic   │ │    │ │ Views/DTOs      │ │
│ │ Event Store      │ │───▶│ │ Optimized DB    │ │
│ └─────────────────┘ │    │ └─────────────────┘ │
└─────────────────────┘    └─────────────────────┘
```

## Why Use CQRS?

| Challenge | CQRS Solution |
|-----------|---------------|
| Complex queries slow down writes | Separate optimized read database |
| Different read/write scaling needs | Scale independently |
| Complex reporting requirements | Purpose-built read models |
| Multiple UI views of same data | Multiple projections |

## CQRS with Excalibur

### Command Side (Write)

Commands modify state through aggregates and emit events:

```csharp
// Command definition
public record PlaceOrderCommand(
    Guid OrderId,
    string CustomerId,
    List<OrderLineDto> Lines) : IDispatchAction;

// Command handler
public class PlaceOrderHandler : IActionHandler<PlaceOrderCommand>
{
    private readonly IEventSourcedRepository<Order, Guid> _repository;

    public async Task HandleAsync(PlaceOrderCommand command, CancellationToken ct)
    {
        // Load or create aggregate
        var order = new Order(command.OrderId, command.CustomerId);

        // Apply business logic
        foreach (var line in command.Lines)
        {
            order.AddLine(line.ProductId, line.Quantity, line.UnitPrice);
        }

        order.Submit();

        // Persist events
        await _repository.SaveAsync(order, ct);
    }
}
```

### Query Side (Read)

Queries read from optimized projections:

```csharp
// Query definition
public record GetOrderSummaryQuery(Guid OrderId) : IDispatchAction<OrderSummaryDto>;

// Query handler reads from projection
public class GetOrderSummaryHandler : IActionHandler<GetOrderSummaryQuery, OrderSummaryDto>
{
    private readonly IDbConnection _readDb;

    public async Task<OrderSummaryDto> HandleAsync(
        GetOrderSummaryQuery query,
        CancellationToken ct)
    {
        // Read from denormalized view (not event store)
        return await _readDb.QuerySingleOrDefaultAsync<OrderSummaryDto>(@"
            SELECT Id, CustomerName, Status, Total, LineCount, CreatedAt
            FROM OrderSummaries
            WHERE Id = @OrderId",
            new { query.OrderId });
    }
}
```

:::tip Direct Dapper vs IDataRequest
The examples above use direct Dapper for simplicity. For complex or reusable queries, consider the `IDataRequest` pattern which provides encapsulation, testability, and correlation tracking:

```csharp
// Using IDataRequest for complex queries
public class GetOrderSummaryRequest : IDataRequest<IDbConnection, OrderSummaryDto?>
{
    public Guid OrderId { get; }

    public GetOrderSummaryRequest(Guid orderId) => OrderId = orderId;

    public CommandDefinition Command => new(@"
        SELECT Id, CustomerName, Status, Total, LineCount, CreatedAt
        FROM OrderSummaries WHERE Id = @OrderId",
        new { OrderId });

    public Func<IDbConnection, Task<OrderSummaryDto?>> ResolveAsync =>
        conn => conn.QuerySingleOrDefaultAsync<OrderSummaryDto>(Command);
}
```

See [IDb Interface](../data-access/idb-interface.md#idatarequest-pattern) for more details.
:::

### Event Synchronization

Events flow from write side to read side using projection handlers:

```csharp
// Projection handler updates read model when events occur
public class OrderSummaryProjectionHandler :
    IEventHandler<OrderCreated>,
    IEventHandler<OrderLineAdded>,
    IEventHandler<OrderSubmitted>
{
    private readonly IDbConnection _readDb;

    public OrderSummaryProjectionHandler(IDbConnection readDb)
    {
        _readDb = readDb;
    }

    public async Task HandleAsync(OrderCreated e, CancellationToken ct)
    {
        await _readDb.ExecuteAsync(@"
            INSERT INTO OrderSummaries
            (Id, CustomerId, Status, Total, LineCount, CreatedAt)
            VALUES (@OrderId, @CustomerId, 'Draft', 0, 0, @OccurredAt)",
            e);
    }

    public async Task HandleAsync(OrderLineAdded e, CancellationToken ct)
    {
        await _readDb.ExecuteAsync(@"
            UPDATE OrderSummaries
            SET LineCount = LineCount + 1,
                Total = Total + (@Quantity * @UnitPrice)
            WHERE Id = @OrderId",
            e);
    }

    public async Task HandleAsync(OrderSubmitted e, CancellationToken ct)
    {
        await _readDb.ExecuteAsync(@"
            UPDATE OrderSummaries
            SET Status = 'Submitted'
            WHERE Id = @OrderId",
            e);
    }
}
```

## Architecture Patterns

### Separate Databases

Use different databases optimized for each side:

```
Write Side:                    Read Side:
┌──────────────────┐          ┌──────────────────┐
│   Event Store    │          │   SQL Server     │
│   (Append-only)  │──Events─▶│   (Read Model)   │
└──────────────────┘          └──────────────────┘
                              ┌──────────────────┐
                     ─Events─▶│   Elasticsearch  │
                              │   (Search)       │
                              └──────────────────┘
```

Configuration:

```csharp
// Write side: Event sourcing with SQL Server
builder.Services.AddSqlServerEventSourcing(writeConnectionString);

// Read side: Separate read database
builder.Services.AddScoped<IDbConnection>(_ =>
    new SqlConnection(readConnectionString));

// Projection worker to sync read models
builder.Services.AddHostedService<ProjectionWorker>();
```

### Eventual Consistency

Read models are eventually consistent with the write model:

```csharp
public class OrderService
{
    public async Task<OrderSummaryDto?> GetOrderAsync(
        Guid orderId,
        bool requireConsistency,
        CancellationToken ct)
    {
        if (requireConsistency)
        {
            // Wait for projection to catch up
            await _projectionSyncService.WaitForPositionAsync(
                orderId,
                timeout: TimeSpan.FromSeconds(5),
                ct);
        }

        return await _readDb.QuerySingleOrDefaultAsync<OrderSummaryDto>(@"
            SELECT * FROM OrderSummaries WHERE Id = @OrderId",
            new { OrderId = orderId });
    }
}
```

## Read Model Design

### Denormalized Views

Store pre-computed data to avoid joins:

```csharp
// Denormalized read model
public class OrderListItem
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; }
    public string CustomerName { get; set; }     // Denormalized from Customer
    public string CustomerEmail { get; set; }    // Denormalized from Customer
    public string Status { get; set; }
    public decimal Total { get; set; }
    public int LineCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? TrackingNumber { get; set; }  // Denormalized from Shipping
}
```

### Multiple Read Models

Create specialized views for different needs:

```csharp
// For order listing (minimal data)
public class OrderListProjection { }

// For order detail page (full data)
public class OrderDetailProjection { }

// For customer dashboard (customer-centric)
public class CustomerOrdersProjection { }

// For admin reporting (analytics)
public class SalesReportProjection { }

// For search functionality
public class OrderSearchProjection { }
```

### Handling Cross-Aggregate Data

When read models need data from multiple aggregates:

```csharp
public class OrderWithCustomerProjectionHandler :
    IEventHandler<OrderCreated>,
    IEventHandler<CustomerNameChanged>
{
    private readonly IDbConnection _db;
    private readonly ICustomerReadModel _customerReadModel;

    public OrderWithCustomerProjectionHandler(
        IDbConnection db,
        ICustomerReadModel customerReadModel)
    {
        _db = db;
        _customerReadModel = customerReadModel;
    }

    public async Task HandleAsync(OrderCreated e, CancellationToken ct)
    {
        // Fetch customer name from customer read model
        var customerName = await _customerReadModel.GetNameAsync(e.CustomerId, ct);

        await _db.ExecuteAsync(@"
            INSERT INTO OrdersWithCustomers
            (OrderId, CustomerId, CustomerName, Status, CreatedAt)
            VALUES (@OrderId, @CustomerId, @CustomerName, 'Draft', @OccurredAt)",
            new { e.OrderId, e.CustomerId, CustomerName = customerName, e.OccurredAt });
    }

    public async Task HandleAsync(CustomerNameChanged e, CancellationToken ct)
    {
        // Update all orders for this customer
        await _db.ExecuteAsync(@"
            UPDATE OrdersWithCustomers
            SET CustomerName = @NewName
            WHERE CustomerId = @CustomerId",
            new { e.CustomerId, e.NewName });
    }
}
```

## Query Patterns

### Simple Queries

```csharp
public record GetOrderByIdQuery(Guid OrderId) : IDispatchAction<OrderDto?>;

public class GetOrderByIdHandler : IActionHandler<GetOrderByIdQuery, OrderDto?>
{
    public async Task<OrderDto?> HandleAsync(GetOrderByIdQuery query, CancellationToken ct)
    {
        return await _db.QuerySingleOrDefaultAsync<OrderDto>(
            "SELECT * FROM Orders WHERE Id = @OrderId",
            query);
    }
}
```

### Paginated Queries

```csharp
public record GetOrdersQuery(
    int Page,
    int PageSize,
    string? Status,
    string? SortBy) : IDispatchAction<PagedResult<OrderDto>>;

public class GetOrdersHandler : IActionHandler<GetOrdersQuery, PagedResult<OrderDto>>
{
    public async Task<PagedResult<OrderDto>> HandleAsync(
        GetOrdersQuery query,
        CancellationToken ct)
    {
        var offset = (query.Page - 1) * query.PageSize;

        var sql = @"
            SELECT * FROM OrderSummaries
            WHERE (@Status IS NULL OR Status = @Status)
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*) FROM OrderSummaries
            WHERE (@Status IS NULL OR Status = @Status);";

        using var multi = await _db.QueryMultipleAsync(sql,
            new { query.Status, Offset = offset, query.PageSize });

        var items = await multi.ReadAsync<OrderDto>();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<OrderDto>(items.ToList(), total, query.Page, query.PageSize);
    }
}
```

## Application Request Types

The `Excalibur.Application` package provides rich base types for commands, queries, jobs, and notifications with built-in support for correlation, multi-tenancy, auditing, transactions, and validation.

### Activity System

All request types implement `IActivity`, which provides observability and access-control metadata:

```csharp
public interface IActivity
{
    ActivityType ActivityType { get; }      // Command, Query, Job, or Notification
    string ActivityName { get; }            // Unique identifier (e.g., "MyApp.Orders:PlaceOrderCommand")
    string ActivityDisplayName { get; }     // Human-readable name for ACL UIs
    string ActivityDescription { get; }     // Detailed description
}

public enum ActivityType
{
    Unknown = 0,
    Command = 1,
    Query = 2,
    Notification = 3,
    Job = 4
}
```

**Activity Naming Conventions:**

By default, `ActivityDisplayName` and `ActivityDescription` are generated automatically from the type name and namespace:
- The type suffix (`Command`, `Query`, `Job`, `Notification`) is stripped
- PascalCase is split into spaces: `PlaceOrderCommand` in namespace `MyApp.Orders` becomes `"MyApp.Orders: Place Order"`
- Both properties default to the same namespace-qualified value, ensuring uniqueness across microservices

Override options (in priority order):
1. **`[Activity]` attribute** (recommended) - `[Activity("Submit Order")]` or `[Activity("Submit Order", "Submits a new order")]`
2. **Virtual property override** - `public override string ActivityDisplayName => "Custom";`
3. **Convention default** - No code needed

:::tip
The `[Activity]` attribute enforces static, type-level metadata because attribute arguments must be compile-time constants. This prevents accidentally embedding instance-specific data (like order IDs) into ACL metadata.
:::

### Commands

Commands represent write operations. Use `ICommand` and `CommandBase` for rich functionality:

```csharp
using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Commands;

// Convention-based: no overrides needed
// ActivityDisplayName => "MyApp.Orders: Place Order"
// ActivityDescription => "MyApp.Orders: Place Order"
public class PlaceOrderCommand : CommandBase
{
    public Guid OrderId { get; init; }
    public string CustomerId { get; init; }
    public List<OrderLineDto> Lines { get; init; }
}

// Using [Activity] attribute for custom metadata
[Activity("Create Product", "Creates a new product in the catalog")]
public class CreateProductCommand : CommandBase<ProductDto>
{
    public string Name { get; init; }
    public decimal Price { get; init; }
}

// Handler
public class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand>
{
    public async Task HandleAsync(PlaceOrderCommand command, CancellationToken ct)
    {
        // Access correlation ID for tracing
        var correlationId = command.CorrelationId;

        // Access tenant ID for multi-tenant scenarios
        var tenantId = command.TenantId;

        // Business logic...
    }
}
```

**CommandBase Features:**
- `Id` / `MessageId` - Unique identifier for the command
- `CorrelationId` - For distributed tracing across services
- `TenantId` - Multi-tenant isolation
- `Headers` - Custom metadata dictionary
- `TransactionBehavior` - Transaction scope (default: `Required`)
- `TransactionIsolation` - Isolation level (default: `ReadCommitted`)
- `TransactionTimeout` - Timeout (default: 1 minute)

### Queries

Queries represent read operations. Use `IQuery<TResult>` and `QueryBase<TResult>`:

```csharp
using Excalibur.Application.Requests.Queries;

// Convention-based: no overrides needed
// ActivityDisplayName => "MyApp.Orders: Get Order Summary"
// ActivityDescription => "MyApp.Orders: Get Order Summary"
public class GetOrderSummaryQuery : QueryBase<OrderSummaryDto>
{
    public Guid OrderId { get; init; }
}

// Handler
public class GetOrderSummaryHandler : IQueryHandler<GetOrderSummaryQuery, OrderSummaryDto>
{
    private readonly IDbConnection _db;

    public async Task<OrderSummaryDto> HandleAsync(
        GetOrderSummaryQuery query,
        CancellationToken ct)
    {
        return await _db.QuerySingleOrDefaultAsync<OrderSummaryDto>(@"
            SELECT * FROM OrderSummaries WHERE Id = @OrderId",
            new { query.OrderId });
    }
}
```

**QueryBase Features:**
- Same correlation and multi-tenancy support as commands
- `TransactionTimeout` defaults to 2 minutes (longer for complex reads)

### Jobs

Jobs represent background operations. Use `IJob` and `JobBase`:

```csharp
using Excalibur.Application.Requests.Jobs;

// Convention-based: no overrides needed
// ActivityDisplayName => "MyApp.Orders: Process Order Batch"
// ActivityDescription => "MyApp.Orders: Process Order Batch"
public class ProcessOrderBatchJob : JobBase
{
    public DateOnly BatchDate { get; init; }
}

// Handler inherits from JobHandlerBase
public class ProcessOrderBatchHandler : JobHandlerBase<ProcessOrderBatchJob>
{
    public override async Task<JobResult> HandleAsync(
        ProcessOrderBatchJob job,
        CancellationToken ct)
    {
        var orders = await GetPendingOrdersAsync(job.BatchDate, ct);

        if (orders.Count == 0)
            return JobResult.NoWorkPerformed;

        await ProcessOrdersAsync(orders, ct);
        return JobResult.OperationSucceeded;
    }
}
```

**JobResult Values:**
- `JobResult.NoWorkPerformed` - Job ran but found nothing to do
- `JobResult.OperationSucceeded` - Job completed successfully

### Notifications

Notifications represent events that can be published across services. Use `INotification` and `NotificationBase`:

```csharp
using Excalibur.Application.Requests.Notifications;

// Convention-based: no overrides needed
// ActivityDisplayName => "MyApp.Shipping: Order Shipped"
// ActivityDescription => "MyApp.Shipping: Order Shipped"
public class OrderShippedNotification : NotificationBase
{
    public Guid OrderId { get; init; }
    public string TrackingNumber { get; init; }

    public OrderShippedNotification(Guid correlationId, string? tenantId = null)
        : base(correlationId, tenantId)
    {
    }
}
```

**NotificationBase Features:**
- Implements `IIntegrationEvent` for cross-service communication
- Full transaction support like commands

### Marker Interfaces

Add cross-cutting capabilities by implementing marker interfaces:

```csharp
// Correlation tracking for distributed tracing
public interface IAmCorrelatable
{
    Guid CorrelationId { get; }
}

// Multi-tenant isolation
public interface IAmMultiTenant
{
    string? TenantId { get; }
}

// Audit logging marker
public interface IAmAuditable;
```

All base classes (`CommandBase`, `QueryBase`, `JobBase`, `NotificationBase`) already implement `IAmCorrelatable` and `IAmMultiTenant`.

### Request Validation

Use FluentValidation with built-in validators that automatically apply based on interfaces:

```csharp
using Excalibur.Application.Requests.Validation;

public class PlaceOrderCommandValidator : RequestValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        // Base class automatically includes:
        // - ActivityValidator (validates ActivityName, DisplayName, Description, Type)
        // - CorrelationValidator (validates CorrelationId)
        // - MultiTenantValidator (validates TenantId if IAmMultiTenant)

        // Add your custom rules
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Lines).NotEmpty()
            .WithMessage("Order must have at least one line item");
    }
}
```

**Built-in Validators:**
- `ActivityValidator<T>` - Validates `IActivity` properties
- `CorrelationValidator<T>` - Validates `IAmCorrelatable.CorrelationId`
- `MultiTenantValidator<T>` - Validates `IAmMultiTenant.TenantId`
- `RequestValidator<T>` - Base class that auto-includes validators based on interfaces
- `RulesFor<TRequest, TPart>` - Create validators for specific interface parts

### Transaction Configuration

Configure transaction behavior per request:

```csharp
public class ImportOrdersCommand : CommandBase
{
    public ImportOrdersCommand()
    {
        // Override transaction defaults for long-running operations
        TransactionBehavior = TransactionScopeOption.Required;
        TransactionIsolation = IsolationLevel.ReadCommitted;
        TransactionTimeout = TimeSpan.FromMinutes(10);
    }

    // ...
}
```

| Request Type | Default Timeout | Default Isolation |
|--------------|-----------------|-------------------|
| Command | 1 minute | ReadCommitted |
| Query | 2 minutes | ReadCommitted |
| Notification | 2 minutes | ReadCommitted |
| Job | (varies) | ReadCommitted |

## Best Practices

### 1. Keep Commands and Queries Separate

```csharp
// Commands in one namespace
namespace MyApp.Commands
{
    public record PlaceOrderCommand(...) : IDispatchAction;
    public record CancelOrderCommand(...) : IDispatchAction;
}

// Queries in another
namespace MyApp.Queries
{
    public record GetOrderQuery(...) : IDispatchAction<OrderDto>;
    public record GetOrdersQuery(...) : IDispatchAction<PagedResult<OrderDto>>;
}
```

### 2. Design Read Models for UI

```csharp
// Match what the UI needs exactly
public class OrderDashboardView
{
    public int PendingOrders { get; set; }
    public int ShippedToday { get; set; }
    public decimal TodayRevenue { get; set; }
    public List<RecentOrderDto> RecentOrders { get; set; }
}
```

### 3. Handle Eventual Consistency in UI

```typescript
// After command, poll or use optimistic UI
async function placeOrder(order: CreateOrderDto) {
  await api.post('/orders', order);

  // Option 1: Optimistic UI update
  addToLocalState(order);

  // Option 2: Poll until consistent
  await pollUntilReady(order.id);

  // Option 3: Real-time notification
  await waitForNotification('order-created', order.id);
}
```

## When to Use CQRS

| Use CQRS When | Avoid CQRS When |
|---------------|-----------------|
| Complex domain with many queries | Simple CRUD applications |
| Different read/write scaling needs | Low traffic systems |
| Multiple views of same data | Single straightforward UI |
| Event sourcing is used | Traditional database is sufficient |
| High query performance needed | Simple queries work fine |

## Next Steps

- **[Event Sourcing](../event-sourcing/index.md)** - Store state as events
- **[Projections](../event-sourcing/projections.md)** - Build read models
- **[Domain Modeling](../domain-modeling/index.md)** - Design aggregates
- **[Validation Middleware](../middleware/validation.md)** - Integrate FluentValidation with the pipeline

## See Also

- [Actions and Handlers](../core-concepts/actions-and-handlers.md) - Command and query handler patterns
- [Patterns Overview](../patterns/index.md) - Architectural patterns including outbox and saga
- [Middleware Overview](../middleware/index.md) - Pipeline middleware for cross-cutting concerns
