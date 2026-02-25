---
sidebar_position: 2
title: MessageContext Design
description: Architectural design of the IMessageContext interface and its evolution
---

# MessageContext Design

This document explains the architectural design of `IMessageContext`, its evolution, and the rationale behind direct properties vs the Items dictionary.

## Overview

`IMessageContext` is the central context object that flows through the entire message processing pipeline. It serves as:

1. **Metadata Container** - Message IDs, timestamps, routing information
2. **Cross-Cutting Concerns** - Correlation, causation, tenancy
3. **Middleware Communication** - Sharing data within the pipeline via Items
4. **Processing State Tracker** - Validation, authorization, retry tracking

## Design Principles

### 1. Performance-First for Hot Paths

Message contexts are created for every message processed. At high throughput (100K+ messages/second), every nanosecond matters.

**Direct properties** provide ~1-3ns access time for frequently-accessed data.
**Dictionary access** requires ~30-50ns (includes hashing, lookup, and boxing overhead).

This 10-20x difference is significant at scale.

### 2. Type Safety Over Magic Strings

Direct properties provide:
- Compile-time type checking
- IntelliSense support
- No runtime casting or type errors
- Self-documenting API

### 3. Separation of Core vs Extensibility

**Core Properties** - Data needed by the framework on every message:
- Identity: `MessageId`, `CorrelationId`, `CausationId`
- Tenancy: `TenantId`, `UserId`
- Routing: `SessionId`, `PartitionKey`
- Processing: `ProcessingAttempts`, `ValidationPassed`

**Items Dictionary** - Data that varies by transport or is user-defined:
- RabbitMQ headers
- HTTP headers
- CloudEvents attributes
- Custom user data

## Property Categories

### Identity & Tracing

```csharp
string? MessageId       // Unique message identifier
string? CorrelationId   // Groups related messages
string? CausationId     // Links to parent message
string? TraceParent     // W3C trace context
string? ExternalId      // External system ID
```

These properties enable distributed tracing and debugging across services.

### Tenancy & Security

```csharp
string? TenantId  // Multi-tenant isolation
string? UserId    // Audit trail
string? Source    // Origin service
```

Essential for multi-tenant applications and security auditing.

### Routing & Ordering

```csharp
string? SessionId     // FIFO ordering
string? PartitionKey  // Partitioned processing
string? WorkflowId    // Saga orchestration
```

Used by transports that support ordering guarantees.

### Message Content

```csharp
IDispatchMessage? Message     // The message object
string? MessageType           // CLR type name
string? ContentType           // Serialization format
object? Result                // Handler return value
```

### Timestamps

```csharp
DateTimeOffset ReceivedTimestampUtc  // When received
DateTimeOffset? SentTimestampUtc     // When sent
```

### Hot-Path Properties

These properties are accessed on nearly every message and were promoted from Items dictionary for performance:

```csharp
// Retry Tracking
int ProcessingAttempts           // Attempt count
DateTimeOffset? FirstAttemptTime // First attempt timestamp
bool IsRetry                     // Retry flag

// Validation
bool ValidationPassed            // Validation result
DateTimeOffset? ValidationTimestamp // When validated

// Transactions
object? Transaction              // Active transaction
string? TransactionId            // Transaction ID

// Timeout
bool TimeoutExceeded             // Timeout flag
TimeSpan? TimeoutElapsed         // Elapsed before timeout

// Rate Limiting
bool RateLimitExceeded           // Rate limit flag
TimeSpan? RateLimitRetryAfter    // Retry-after duration
```

## Items Dictionary

The Items dictionary is for data that:

1. **Varies by transport** - RabbitMQ has delivery tags; SQS has receipt handles
2. **Has unpredictable keys** - HTTP headers, CloudEvents attributes
3. **Is accessed infrequently** - Setup once, read once

### Transport-Specific Examples

```csharp
// RabbitMQ
context.Items["rabbitmq.exchange"] = exchange;
context.Items["rabbitmq.deliveryTag"] = deliveryTag;

// HTTP Headers
context.Items["Authorization"] = bearerToken;
context.Items["X-Custom-Header"] = value;

// CloudEvents
context.Items["ce.type"] = "com.example.event";
context.Items["ce.source"] = "/my-service";
```

See [MessageContext Items Usage](./messagecontext-items-usage.md) for complete documentation.

## Context Propagation

When dispatching child messages, cross-cutting concerns propagate automatically:

```csharp
var childContext = context.CreateChildContext();
// Propagated: CorrelationId, TenantId, UserId, SessionId,
//             WorkflowId, TraceParent, Source
// Set automatically: CausationId = parent.MessageId
// NOT copied: Items dictionary, hot-path properties
```

This enables distributed tracing without manual plumbing.

## Thread Safety

`MessageContext` is designed to be thread-safe:

- Property setters use standard CLR mechanisms
- Items dictionary can be accessed from middleware on different threads
- The context is scoped to a single message processing operation

## Lifetime & Pooling

Message contexts are pooled for performance:

1. **Acquired** from pool when message enters pipeline
2. **Used** throughout message processing
3. **Cleared** when processing completes
4. **Returned** to pool for reuse

Hot-path properties reset to default values on clear. Items dictionary is cleared but not reallocated.

## Next Steps

- [MessageContext Items Usage](./messagecontext-items-usage.md) - When to use Items vs properties
- [MessageContext Best Practices](../performance/messagecontext-best-practices.md) - Performance optimization
- [Migration Guide](../migration/messagecontext-v1.md) - Migrating from Items to direct properties

## See Also

- [Core Concepts: Message Context](../core-concepts/message-context.md) - User-facing message context guide
- [Pipeline Overview](../pipeline/index.md) - How messages flow through the pipeline
- [Architecture Overview](./index.md) - All architecture documentation
