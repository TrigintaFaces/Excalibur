# Reliability Samples

Reliability patterns for distributed systems: guaranteed delivery, resilience, and saga orchestration.

## Choosing a Reliability Pattern

| Pattern | Best For | Complexity | Infrastructure |
|---------|----------|------------|----------------|
| **[Outbox Pattern](OutboxPattern/)** | Guaranteed message delivery | Medium | Database |
| **[Retry/Circuit Breaker](RetryAndCircuitBreaker/)** | Transient failure handling | Low | None |
| **[Saga Orchestration](SagaOrchestration/)** | Distributed transactions | High | Database |

## Samples Overview

| Sample | What It Demonstrates | Local Dev Ready |
|--------|---------------------|-----------------|
| [OutboxPattern](OutboxPattern/) | Transactional outbox, inbox deduplication, at-least-once delivery | Yes - in-memory |
| [RetryAndCircuitBreaker](RetryAndCircuitBreaker/) | Retry policies, circuit breaker, timeout, bulkhead | Yes - no dependencies |
| [SagaOrchestration](SagaOrchestration/) | Multi-step workflows, compensation, saga state | Yes - in-memory |

## Quick Start

### Retry & Circuit Breaker (Simplest)

```bash
cd samples/04-reliability/RetryAndCircuitBreaker
dotnet run
```

No external dependencies - demonstrates resilience patterns.

### Outbox Pattern (Guaranteed Delivery)

```bash
cd samples/04-reliability/OutboxPattern
dotnet run
```

Demonstrates atomic commit with message staging.

### Saga Orchestration (Distributed Transactions)

```bash
cd samples/04-reliability/SagaOrchestration
dotnet run
```

Demonstrates multi-step workflows with compensation.

## Reliability Patterns Comparison

### Pattern Selection Guide

| Scenario | Recommended Pattern |
|----------|---------------------|
| External API calls failing intermittently | **Retry with backoff** |
| Dependency is down | **Circuit breaker** |
| Need guaranteed event delivery | **Outbox pattern** |
| Multi-service transactions | **Saga orchestration** |
| Prevent duplicate processing | **Inbox deduplication** |
| Resource exhaustion | **Bulkhead isolation** |

### Delivery Guarantees

| Pattern | Guarantee | Deduplication |
|---------|-----------|---------------|
| Outbox | At-least-once | Requires inbox |
| Direct publish | At-most-once | N/A |
| Saga | Eventual consistency | Built-in |

## Key Concepts

### Outbox Pattern

```
1. BEGIN TRANSACTION
2. Save business data
3. Save event to outbox table
4. COMMIT TRANSACTION

5. Background processor:
   - Read pending messages
   - Publish to message broker
   - Mark as processed (or retry)
```

### Retry with Exponential Backoff

```
Attempt 1: Fail -> Wait 200ms
Attempt 2: Fail -> Wait 400ms (+ jitter)
Attempt 3: Fail -> Wait 800ms (+ jitter)
Attempt 4: Success!
```

### Circuit Breaker States

```
CLOSED (normal)
   |
   v (failures >= threshold)
OPEN (fail fast)
   |
   v (after cooldown)
HALF-OPEN (test)
   |
   +-> CLOSED (on success)
   +-> OPEN (on failure)
```

### Saga with Compensation

```
Order Placement Saga:
  1. Reserve inventory → (compensate: release inventory)
  2. Process payment  → (compensate: refund payment)
  3. Ship order       → (compensate: cancel shipment)
```

## Configuration Examples

### Outbox Configuration

```csharp
builder.Services.AddExcaliburOutbox(options =>
{
    options.BatchSize = 100;
    options.PollingInterval = TimeSpan.FromSeconds(5);
    options.MaxRetryCount = 3;
    options.MessageRetentionPeriod = TimeSpan.FromDays(7);
});
```

### Retry Policy

```csharp
builder.Services.AddPollyRetryPolicy("payment-retry", options =>
{
    options.MaxRetries = 5;
    options.BaseDelay = TimeSpan.FromMilliseconds(200);
    options.BackoffStrategy = BackoffStrategy.Exponential;
    options.UseJitter = true;
});
```

### Circuit Breaker

```csharp
builder.Services.AddPollyCircuitBreaker("inventory-circuit", options =>
{
    options.FailureThreshold = 3;
    options.SuccessThreshold = 2;
    options.OpenDuration = TimeSpan.FromSeconds(10);
});
```

### Bulkhead

```csharp
builder.Services.AddBulkhead("notification-bulkhead", options =>
{
    options.MaxConcurrency = 5;
    options.MaxQueueLength = 10;
});
```

## Best Practices

### DO

- Use outbox for critical business events
- Combine retry + circuit breaker + timeout
- Design idempotent handlers (safe for retries)
- Use jitter to prevent thundering herd
- Monitor circuit breaker state and outbox depth

### DON'T

- Retry non-transient errors (validation failures, 404s)
- Use outbox for read queries
- Skip compensation logic in sagas
- Set very short circuit breaker cooldowns
- Ignore retry counts in metrics

## Pattern Combinations

### Recommended: Layered Resilience

```
Request -> CircuitBreaker[
              Retry[
                 Timeout[
                    Operation
                 ]
              ]
           ]
```

### Recommended: Outbox + Inbox

```
Producer:
  1. Save to database + outbox (atomic)
  2. Outbox processor publishes

Consumer:
  1. Check inbox for duplicate
  2. Process message
  3. Record in inbox
```

## Prerequisites

| Sample | Requirements |
|--------|-------------|
| OutboxPattern | .NET 9.0 SDK |
| RetryAndCircuitBreaker | .NET 9.0 SDK |
| SagaOrchestration | .NET 9.0 SDK |

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Outbox` | Transactional outbox abstractions |
| `Excalibur.Outbox.SqlServer` | SQL Server outbox/inbox |
| `Excalibur.Dispatch.Resilience.Polly` | Polly integration (retry, circuit breaker) |
| `Excalibur.Saga` | Saga orchestration |
| `Excalibur.Saga.SqlServer` | SQL Server saga persistence |

## Related Samples

- [RabbitMQ](../02-messaging-transports/RabbitMQ/) - Transport integration
- [Kafka](../02-messaging-transports/Kafka/) - High-throughput transport

## Learn More

- [Transactional Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [Circuit Breaker Pattern](https://martinfowler.com/bliki/CircuitBreaker.html)
- [Saga Pattern](https://microservices.io/patterns/data/saga.html)
- [Polly Documentation](https://github.com/App-vNext/Polly)

---

*Category: Reliability | Sprint 432*
