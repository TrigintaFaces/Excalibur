---
sidebar_position: 5
title: MessageContext Guide
description: Using IMessageContext with typed feature interfaces for cross-cutting concerns
---

# MessageContext Guide

This guide covers the `IMessageContext` interface and how to use its typed feature interfaces for cross-cutting concerns, validation tracking, retry handling, and more.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Familiarity with [message context](../core-concepts/message-context.md) and [middleware](../middleware/)

## Interface Overview

The `IMessageContext` interface provides 8 core properties plus a `Features` dictionary for typed access to cross-cutting concerns. This design follows `Microsoft.AspNetCore.Http.HttpContext`.

### Core Properties

```csharp
// Message identification
string? MessageId { get; set; }        // Unique message instance ID
string? CorrelationId { get; set; }    // Business transaction ID
string? CausationId { get; set; }      // Parent message ID (causality chain)

// Payload and result
IDispatchMessage? Message { get; set; }
object? Result { get; set; }

// Infrastructure
IServiceProvider RequestServices { get; set; }
IDictionary<string, object> Items { get; }    // Transport metadata, custom data
IDictionary<Type, object> Features { get; }   // Typed feature collection
```

### Feature Interfaces

Cross-cutting concerns are accessed via typed feature interfaces:

```csharp
using Excalibur.Dispatch.Abstractions.Features;

// Identity & multi-tenancy (IMessageIdentityFeature)
var tenantId = context.GetTenantId();
var userId = context.GetUserId();
var traceParent = context.GetTraceParent();
var sessionId = context.GetSessionId();
var workflowId = context.GetWorkflowId();
var partitionKey = context.GetPartitionKey();

// Processing state (IMessageProcessingFeature)
var attempts = context.GetProcessingAttempts();
var isRetry = context.GetIsRetry();
var deliveryCount = context.GetDeliveryCount();

// Validation (IMessageValidationFeature)
var passed = context.GetValidationPassed();

// Timeout (IMessageTimeoutFeature)
var exceeded = context.GetTimeoutExceeded();

// Rate limiting (IMessageRateLimitFeature)
var limited = context.GetRateLimitExceeded();

// Routing (IMessageRoutingFeature)
var decision = context.GetRoutingDecision();
var source = context.GetSource();

// Transactions (IMessageTransactionFeature)
var txId = context.GetTransactionId();
```

---

## Usage Patterns

### Cross-Cutting Concerns

```csharp
// Core properties (direct on interface)
var correlationId = context.CorrelationId;

// Identity via feature extensions
var tenantId = context.GetTenantId();
var userId = context.GetUserId();

// Write via feature instance
var identity = context.GetOrCreateIdentityFeature();
identity.UserId = userId;
```

### Validation Tracking

```csharp
var validation = context.GetOrCreateValidationFeature();
validation.ValidationPassed = true;
validation.ValidationTimestamp = DateTimeOffset.UtcNow;

if (context.GetValidationPassed())
{
    // Skip re-validation
}
```

### Retry Handling

```csharp
var processing = context.GetOrCreateProcessingFeature();
processing.ProcessingAttempts++;
processing.IsRetry = processing.ProcessingAttempts > 1;
processing.FirstAttemptTime ??= DateTimeOffset.UtcNow;
```

### Transaction Management

```csharp
var txFeature = context.GetOrCreateTransactionFeature();
txFeature.Transaction = transaction;
txFeature.TransactionId = transaction.TransactionId;

var tx = context.GetTransaction() as IDbTransaction;
```

### Rate Limiting

```csharp
var rateLimit = context.GetOrCreateRateLimitFeature();
rateLimit.RateLimitExceeded = true;
rateLimit.RateLimitRetryAfter = TimeSpan.FromSeconds(30);
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
        // Check if already validated (feature extension)
        if (context.GetValidationPassed())
        {
            return await nextDelegate(message, context, cancellationToken);
        }

        // Perform validation
        var isValid = await ValidateAsync(message);

        var validation = context.GetOrCreateValidationFeature();
        validation.ValidationPassed = isValid;
        validation.ValidationTimestamp = DateTimeOffset.UtcNow;

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
        // Core property
        var correlationId = context.CorrelationId;

        // Feature extensions
        var userId = context.GetUserId();
        var tenantId = context.GetTenantId();

        // ... handle command
    }
}
```

---

## Testing

### Using Feature Extensions in Tests

```csharp
using Excalibur.Dispatch.Abstractions.Features;

[Fact]
public async Task Handler_ShouldUseUserIdFromContext()
{
    // Arrange - create context with features
    var context = new MessageContext
    {
        MessageId = Guid.NewGuid().ToString(),
        CorrelationId = Guid.NewGuid().ToString()
    };

    // Set identity via feature
    var identity = context.GetOrCreateIdentityFeature();
    identity.UserId = "user-123";
    identity.TenantId = "tenant-456";

    var handler = new OrderHandler();
    var command = new CreateOrderCommand("cust-123");

    // Act
    var result = await handler.HandleAsync(command, context, CancellationToken.None);

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
    var parent = new MessageContext
    {
        MessageId = "parent-123",
        CorrelationId = "correlation-456"
    };
    var identity = parent.GetOrCreateIdentityFeature();
    identity.TenantId = "tenant-789";
    identity.UserId = "user-abc";

    // Act
    var child = parent.CreateChildContext();

    // Assert
    child.CorrelationId.ShouldBe(parent.CorrelationId);
    child.GetTenantId().ShouldBe("tenant-789");
    child.GetUserId().ShouldBe("user-abc");
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
- [MessageContext Design](../architecture/messagecontext-design.md) - Architectural decisions behind the feature-based design
- [Version Upgrades](./version-upgrades.md) - Breaking changes and upgrade steps between Dispatch versions
- [Migration Guides Overview](./index.md) - Index of all migration guides including MediatR, MassTransit, and NServiceBus
