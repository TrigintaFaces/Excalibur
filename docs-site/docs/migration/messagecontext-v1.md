---
sidebar_position: 5
title: MessageContext Guide
description: Using IMessageContext direct properties for type-safe, high-performance message context access
---

# MessageContext Guide

This guide covers the `IMessageContext` interface and how to use its strongly-typed properties for cross-cutting concerns, validation tracking, retry handling, and more.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Familiarity with [message context](../core-concepts/message-context.md) and [middleware](../middleware/)

## Interface Overview

The `IMessageContext` interface provides 25+ direct properties organized by category, offering type safety and performance over dictionary lookups.

### Identity Properties

```csharp
// Message identification
string? MessageId { get; set; }        // Unique message instance ID
string? ExternalId { get; set; }       // External system correlation
string? MessageType { get; set; }      // CLR type name for routing

// Correlation and causation
string? CorrelationId { get; set; }    // Business transaction ID
string? CausationId { get; set; }      // Parent message ID (causality chain)
```

### Multi-Tenancy & User Context

```csharp
string? TenantId { get; set; }         // Tenant isolation
string? UserId { get; set; }           // Authenticated user
```

### Distributed Tracing

```csharp
string? TraceParent { get; set; }      // W3C Trace Context
string? SessionId { get; set; }        // Message grouping
string? WorkflowId { get; set; }       // Saga orchestration
string? PartitionKey { get; set; }     // Partition routing
string? Source { get; set; }           // Origin service
```

### Processing State (Hot-Path)

```csharp
// Retry tracking
int ProcessingAttempts { get; set; }   // Attempt counter
DateTimeOffset? FirstAttemptTime { get; set; }
bool IsRetry { get; set; }

// Validation
bool ValidationPassed { get; set; }
DateTimeOffset? ValidationTimestamp { get; set; }

// Transactions
object? Transaction { get; set; }
string? TransactionId { get; set; }

// Timeout handling
bool TimeoutExceeded { get; set; }
TimeSpan? TimeoutElapsed { get; set; }

// Rate limiting
bool RateLimitExceeded { get; set; }
TimeSpan? RateLimitRetryAfter { get; set; }
```

### Message & Routing

```csharp
IDispatchMessage? Message { get; set; }
object? Result { get; set; }
IRoutingResult RoutingResult { get; set; }
IServiceProvider RequestServices { get; set; }
```

### Timestamps

```csharp
DateTimeOffset ReceivedTimestampUtc { get; set; }
DateTimeOffset? SentTimestampUtc { get; set; }
```

---

## Usage Patterns

### Cross-Cutting Concerns

```csharp
// Type-safe, performant property access
var correlationId = context.CorrelationId;
var tenantId = context.TenantId;
context.UserId = userId;
```

### Validation Tracking

```csharp
context.ValidationPassed = true;
context.ValidationTimestamp = DateTimeOffset.UtcNow;

if (context.ValidationPassed)
{
    // Skip validation
}
```

### Retry Handling

```csharp
context.ProcessingAttempts++;
context.IsRetry = context.ProcessingAttempts > 1;
context.FirstAttemptTime ??= DateTimeOffset.UtcNow;
```

### Transaction Management

```csharp
context.Transaction = transaction;
context.TransactionId = transaction.TransactionId;

var tx = context.Transaction as IDbTransaction;
```

### Rate Limiting

```csharp
context.RateLimitExceeded = true;
context.RateLimitRetryAfter = TimeSpan.FromSeconds(30);
```

---

## When to Use Items Dictionary

The `Items` dictionary is appropriate for:

1. **Transport-specific metadata**
   ```csharp
   // RabbitMQ headers
   context.SetItem("x-death", deathHeaders);

   // SQS attributes
   context.SetItem("ApproximateReceiveCount", receiveCount);
   ```

2. **CloudEvents extension attributes**
   ```csharp
   context.SetItem("ce_customextension", extensionValue);
   ```

3. **Custom HTTP headers**
   ```csharp
   context.SetItem("X-Custom-Header", headerValue);
   ```

---

## Middleware Example

```csharp
public class ValidationMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        // Check if already validated (direct property)
        if (context.ValidationPassed)
        {
            return await nextDelegate(message, context, cancellationToken);
        }

        // Perform validation
        var isValid = await ValidateAsync(message);

        context.ValidationPassed = isValid;
        context.ValidationTimestamp = DateTimeOffset.UtcNow;

        if (!isValid)
        {
            // Use SetItem for validation errors (unpredictable structure)
            context.SetItem("ValidationErrors", _errors);
            return MessageResult.Failure("Validation failed");
        }

        return await nextDelegate(message, context, cancellationToken);
    }
}
```

---

## Handler Example

```csharp
public class OrderHandler : IDispatchHandler<PlaceOrderCommand>
{
    public async Task<IMessageResult> HandleAsync(
        PlaceOrderCommand command,
        IMessageContext context,
        CancellationToken ct)
    {
        var userId = context.UserId;
        var tenantId = context.TenantId;
        var correlationId = context.CorrelationId;

        // ... handle command
    }
}
```

---

## Testing

### Using TestMessageContext

`TestMessageContext` is a shared test double in `Tests.Shared`:

```csharp
using Tests.Shared.TestDoubles;

[Fact]
public async Task Handler_ShouldUseUserIdFromContext()
{
    // Arrange
    var context = new TestMessageContext
    {
        MessageId = Guid.NewGuid().ToString(),
        UserId = "user-123",
        TenantId = "tenant-456",
        CorrelationId = Guid.NewGuid().ToString()
    };

    var contextAccessor = new TestMessageContextAccessor(context);
    var handler = new OrderHandler(contextAccessor);
    var command = new CreateOrderCommand("cust-123");

    // Act
    var result = await handler.HandleAsync(command, CancellationToken.None);

    // Assert
    result.IsSuccess.ShouldBeTrue();
}
```

### Testing Child Context Propagation

```csharp
[Fact]
public void CreateChildContext_ShouldPropagateCrossCuttingConcerns()
{
    // Arrange
    var parent = new TestMessageContext
    {
        MessageId = "parent-123",
        CorrelationId = "correlation-456",
        TenantId = "tenant-789",
        UserId = "user-abc"
    };

    // Act
    var child = parent.CreateChildContext();

    // Assert
    child.CorrelationId.ShouldBe(parent.CorrelationId);
    child.TenantId.ShouldBe(parent.TenantId);
    child.UserId.ShouldBe(parent.UserId);
    child.CausationId.ShouldBe(parent.MessageId); // Causality chain
    child.MessageId.ShouldNotBe(parent.MessageId); // New ID
}
```

---

## Related Documentation

- [Testing Guide](../advanced/testing.md) - Test double usage
- [Middleware Guide](../middleware/) - Pipeline patterns

## See Also

- [Message Context](../core-concepts/message-context.md) - Full reference for IMessageContext properties and usage patterns
- [Version Upgrades](./version-upgrades.md) - Breaking changes and upgrade steps between Dispatch versions
- [Migration Guides Overview](./index.md) - Index of all migration guides including MediatR, MassTransit, and NServiceBus
