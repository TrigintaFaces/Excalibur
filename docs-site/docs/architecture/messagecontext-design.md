---
sidebar_position: 2
title: MessageContext Design
description: Architectural design of the IMessageContext interface and its evolution
---

# MessageContext Design

This document explains the architectural design of `IMessageContext`, its evolution, and the rationale behind the feature-based decomposition.

## Overview

`IMessageContext` is the central context object that flows through the entire message processing pipeline. It follows the pattern of `Microsoft.AspNetCore.Http.HttpContext`: a minimal set of core properties plus a typed feature collection for extensibility.

It serves as:

1. **Metadata Container** - Core message IDs (MessageId, CorrelationId, CausationId)
2. **Middleware Communication** - Sharing data within the pipeline via Items dictionary
3. **Feature Collection** - Typed access to cross-cutting concerns (identity, routing, processing state, validation, timeout, rate limiting, transactions) via `Features` dictionary

## Design Principles

### 1. Microsoft-First: Minimal Core Interface

Following the `HttpContext` pattern, `IMessageContext` exposes only 8 properties. All cross-cutting concerns are accessed through typed feature interfaces, keeping the core interface stable as new concerns are added.

**Before (Sprint 591):** 40 direct properties on `IMessageContext`
**After (Sprint 592):** 8 core properties + 7 feature interfaces

### 2. Type Safety via Feature Interfaces

Feature interfaces provide:
- Compile-time type checking
- IntelliSense support
- No runtime casting or type errors
- Self-documenting API
- Independent testability and mockability

### 3. Separation of Core vs Features vs Items

**Core Properties (8)** - Data needed by the framework on every message:
- Identity: `MessageId`, `CorrelationId`, `CausationId`
- Payload: `Message`, `Result`
- Infrastructure: `RequestServices`, `Items`, `Features`

**Feature Interfaces** - Typed cross-cutting concerns:
- `IMessageIdentityFeature` - UserId, TenantId, SessionId, WorkflowId, ExternalId, TraceParent
- `IMessageProcessingFeature` - ProcessingAttempts, IsRetry, FirstAttemptTime, DeliveryCount
- `IMessageValidationFeature` - ValidationPassed, ValidationTimestamp
- `IMessageTimeoutFeature` - TimeoutExceeded, TimeoutElapsed
- `IMessageRateLimitFeature` - RateLimitExceeded, RateLimitRetryAfter
- `IMessageRoutingFeature` - RoutingDecision, PartitionKey, Source
- `IMessageTransactionFeature` - Transaction, TransactionId

**Items Dictionary** - Data that varies by transport or is user-defined:
- RabbitMQ headers, SQS attributes
- HTTP headers, CloudEvents attributes
- Custom user data

## Core Properties

```csharp
string? MessageId           // Unique message identifier
string? CorrelationId       // Groups related messages
string? CausationId         // Links to parent message
IDispatchMessage? Message   // The message payload
object? Result              // Handler return value
IServiceProvider RequestServices // Scoped DI container
IDictionary<string, object> Items    // Transport metadata, custom data
IDictionary<Type, object> Features   // Typed feature collection
```

## Feature Access Patterns

Three levels of feature access are available:

```csharp
using Excalibur.Dispatch.Abstractions.Features;

// 1. Generic feature access
var identity = context.GetFeature<IMessageIdentityFeature>();
context.SetFeature<IMessageIdentityFeature>(new MessageIdentityFeature { TenantId = "acme" });

// 2. Typed convenience (get or create with default implementation)
var processing = context.GetOrCreateProcessingFeature();
processing.ProcessingAttempts++;

// 3. Property-level convenience (read-only shortcuts)
var tenantId = context.GetTenantId();
var isRetry = context.GetIsRetry();
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
// Propagated: CorrelationId, IMessageIdentityFeature, IMessageRoutingFeature.Source
// Set automatically: CausationId = parent.MessageId, new MessageId
// NOT copied: Items dictionary, processing/validation/timeout features
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
3. **Cleared** when processing completes (features and items cleared)
4. **Returned** to pool for reuse

## Evolution History

| Sprint | Change |
|--------|--------|
| Initial | IMessageContext with 40 direct properties |
| 592 | Decomposed to 8 core + 7 feature interfaces (ADR-166) |

The decomposition was driven by the Microsoft-First Compliance Audit (epic `Excalibur.Dispatch-7umoi`). See ADR-166 (`management/architecture/adr-166-sprint-592-imessagecontext-decomposition.md`) for the full decision record.

## Next Steps

- [MessageContext Items Usage](./messagecontext-items-usage.md) - When to use Items vs features
- [MessageContext Best Practices](../performance/messagecontext-best-practices.md) - Performance optimization

## See Also

- [Core Concepts: Message Context](../core-concepts/message-context.md) - User-facing message context guide
- [Pipeline Overview](../pipeline/index.md) - How messages flow through the pipeline
- [Architecture Overview](./index.md) - All architecture documentation
