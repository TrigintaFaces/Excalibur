---
sidebar_position: 1
title: MessageContext Items Usage
description: Guidelines for when to use the Items dictionary vs direct properties
---

# MessageContext Items Dictionary Usage

This document explains when to use the `IMessageContext.Items` dictionary versus direct properties, and catalogs legitimate Items dictionary usage across the Dispatch framework.

## Overview

`IMessageContext` provides two mechanisms for storing contextual data:

1. **Direct Properties** - Strongly-typed properties with ~1-3ns access time
2. **Items Dictionary** - Flexible key-value storage with ~30-50ns access time (includes boxing overhead)

### When to Use Direct Properties

Use direct properties for data that:

- Is accessed on **every message dispatch** (hot path)
- Has a **known, fixed schema** across all transports
- Is **core to the messaging framework** (correlation, validation, retry tracking)

Examples of direct properties:
- `CorrelationId`, `CausationId`, `TenantId`
- `ProcessingAttempts`, `IsRetry`, `FirstAttemptTime`
- `ValidationPassed`, `ValidationTimestamp`
- `TimeoutExceeded`, `RateLimitExceeded`

### When to Use Items Dictionary

Use the Items dictionary for data that:

- Is **transport-specific** (only relevant to RabbitMQ, SQS, etc.)
- Has **unpredictable keys** (custom headers, user-defined attributes)
- Is accessed **infrequently** (once during message setup, not on every dispatch)
- Would **bloat the interface** with transport-specific concerns

## Transport-Specific Items Usage

The following sections document legitimate Items dictionary usage by transport.

### RabbitMQ Transport

```csharp
// Set by RabbitMqChannelConsumer when receiving messages
context.Items["rabbitmq.exchange"] = ea.Exchange;      // Exchange name
context.Items["rabbitmq.routingKey"] = ea.RoutingKey;  // Message routing key
context.Items["rabbitmq.deliveryTag"] = ea.DeliveryTag; // Delivery tag for ack/nack
context.Items["rabbitmq.redelivered"] = ea.Redelivered; // Redelivery flag

// Custom headers from RabbitMQ messages (unpredictable keys)
foreach (var header in basicProperties.Headers)
{
    context.Items[header.Key] = header.Value;
}
```

**Rationale**: RabbitMQ headers are user-defined and unpredictable. The delivery tag and redelivered flag are transport-specific concepts not shared with other transports.

### Google Pub/Sub Transport

```csharp
// Set by GooglePubSubChannelReceiver when receiving messages
foreach (var attr in pubsubMessage.Attributes)
{
    context.Items[attr.Key] = attr.Value;
}
```

**Rationale**: Pub/Sub attributes are user-defined key-value pairs with unpredictable keys.

### ASP.NET Core Integration

```csharp
// Set by HttpContextExtensions when bridging HTTP requests
foreach (var header in httpContext.Request.Headers)
{
    context.Items[header.Key] = header.Value.ToString();
}
```

**Rationale**: HTTP headers are user-defined with unpredictable keys.

### CloudEvents Integration

```csharp
// Set by CloudEventMiddleware for CloudEvents attributes
context.Items["cloudevent.outgoing"] = outgoingCloudEvent;
context.Items[$"ce.{attr.Name}"] = cloudEvent[attr.Name];
```

**Rationale**: CloudEvents extension attributes are defined by the CloudEvents spec and vary by event type.

### Service Mesh Integration

```csharp
// Set by ServiceMeshMiddleware for Envoy/Istio integration
context.Items["X-Envoy-Available"] = isAvailable.ToString();
context.Items["X-Service-Mesh"] = "true";
context.Items["X-Service-Mesh-Version"] = "1.0";
context.Items["X-Service-Name"] = serviceName;
context.Items["X-Instance-MessageId"] = instanceId;
context.Items["X-Mesh-Timestamp"] = timestamp;
context.Items["X-Service-Endpoints"] = endpointCount;
context.Items["X-Service-Healthy"] = healthyCount;
context.Items["X-Processing-Time-Ms"] = elapsedMs;
context.Items["X-Processing-Success"] = success;
```

**Rationale**: Service mesh headers follow Envoy/Istio conventions and are only relevant when service mesh is enabled.

### Routing and Scheduling

```csharp
// Set by RoutingMiddleware on strongly-typed context property
context.RoutingDecision = decision; // RoutingDecision (Transport, Endpoints, metadata)

// Legacy transport metadata (set by transport adapters)
context.Items["queue"] = meta.QueueName;
context.Items["topic"] = meta.TopicName;
context.Items["routing-key"] = meta.RoutingKey;

// Set by EnhancedScheduledMessageService
context.Items["ScheduleTimeZone"] = item.TimeZoneId;

// Set by TransportAdapterRouter
context.Items["TransportAdapterId"] = adapterId;
context.Items["RoutedTimestamp"] = DateTimeOffset.UtcNow;
```

**Rationale**: Routing decision is now strongly typed (`IMessageContext.RoutingDecision`) while transport-specific legacy metadata can still vary by adapter.

### Distributed Tracing Context

```csharp
// Set by DispatchContextInitializer for W3C Baggage propagation
context.Items[$"baggage.{key}"] = value;
```

**Rationale**: W3C Baggage entries are user-defined with unpredictable keys.

## Internal Framework Usage

Some Items usage is internal to the framework and may be migrated to direct properties in future versions:

### Caching Middleware

```csharp
// Used internally by CachingMiddleware
context.Items["Dispatch:Result"] = returnValue;
context.Items["Dispatch:OriginalResult"] = messageResult;
```

### Upcasting

```csharp
// Used by UpcastingMessageBusDecorator for event versioning
context.Items["Dispatch:OriginalMessageType"] = evt.GetType();
context.Items["Dispatch:UpcastedMessageType"] = upcasted.GetType();
context.Items["Dispatch:OriginalVersion"] = versioned.Version;
context.Items["Dispatch:UpcastedVersion"] = latestVersion;
```

### Poison Message Handling

```csharp
// Some Items still used by PoisonMessageMiddleware
// Note: ProcessingAttempts, FirstAttemptTime, IsRetry are now direct properties
context.Items["CurrentAttemptTime"] = DateTimeOffset.UtcNow;
context.Items["ProcessingHistory"] = history;
context.Items["IsReplay"] = true;
context.Items["OriginalDeadLetterId"] = deadLetterMessage.Id;
```

### Validation

```csharp
// Used by InputValidationMiddleware and profile validation
context.Items["PayloadSizeWarning"] = warningMessage;
// Note: ValidationPassed and ValidationTimestamp are now direct properties
```

## Best Practices

### DO

- Use Items for transport-specific metadata that varies by transport
- Use Items for user-defined headers/attributes with unpredictable keys
- Prefix internal framework keys with `Dispatch:` to avoid collisions
- Prefix transport-specific keys with the transport name (e.g., `rabbitmq.`)

### DON'T

- Use Items for data accessed on every dispatch (use direct properties)
- Use Items for cross-cutting concerns like correlation or tenancy
- Store large objects in Items (impacts memory and pooling)
- Rely on Items for data that should propagate to child contexts

## Migration from Items to Direct Properties

If you previously used Items for hot-path data, migrate to the new direct properties:

```csharp
// Before (Items dictionary - 30-50ns)
context.Items["ValidationPassed"] = true;
var passed = (bool)context.Items["ValidationPassed"];

// After (Direct property - 1-3ns)
context.ValidationPassed = true;
var passed = context.ValidationPassed;
```

### Available Direct Properties

| Property | Type | Purpose |
|----------|------|---------|
| `ProcessingAttempts` | `int` | Retry tracking |
| `FirstAttemptTime` | `DateTimeOffset?` | First processing timestamp |
| `IsRetry` | `bool` | Retry flag |
| `ValidationPassed` | `bool` | Validation result |
| `ValidationTimestamp` | `DateTimeOffset?` | Validation completion time |
| `Transaction` | `object?` | Active transaction |
| `TransactionId` | `string?` | Transaction identifier |
| `TimeoutExceeded` | `bool` | Timeout flag |
| `TimeoutElapsed` | `TimeSpan?` | Elapsed time before timeout |
| `RateLimitExceeded` | `bool` | Rate limit flag |
| `RateLimitRetryAfter` | `TimeSpan?` | Retry-after duration |

## Performance Comparison

| Access Method | Typical Latency | Notes |
|---------------|-----------------|-------|
| Direct property | 1-3ns | Inline property access |
| Items dictionary | 30-50ns | Dictionary lookup + boxing |
| GetItem&lt;T&gt; | 40-60ns | Dictionary + type check |

For data accessed on every message (hot path), the 10-20x performance difference is significant at scale.

## Next Steps

- [MessageContext Design](./messagecontext-design.md) - Architecture and design rationale
- [Performance Best Practices](../performance/messagecontext-best-practices.md) - Optimization guidance
- [Middleware Pipeline](../pipeline/) - Pipeline architecture

## See Also

- [Core Concepts: Message Context](../core-concepts/message-context.md) - User-facing message context guide
- [Auto-Freeze](../performance/auto-freeze.md) - Automatic message context freezing for performance
- [Architecture Overview](./index.md) - All architecture documentation
