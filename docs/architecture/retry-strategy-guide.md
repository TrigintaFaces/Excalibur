# Retry Strategy Guide

## Overview

The Excalibur framework uses multiple retry strategies across subsystems. This divergence
is **intentional** -- each subsystem has different reliability semantics and SDK-specific
retry behavior.

## Retry Strategies by Subsystem

| Subsystem | Mechanism | Location | When to Use |
|-----------|-----------|----------|-------------|
| **Dispatch Core** | `IRetryPolicy` + `DefaultRetryPolicy` + `NoOpRetryPolicy` | `src/Dispatch/Excalibur.Dispatch/Resilience/` | Framework-level message handler retries. Use for custom handler-level retry logic. |
| **Polly Integration** | `PollyRetryPolicyAdapter` implements `IRetryPolicy` | `src/Dispatch/Excalibur.Dispatch.Resilience.Polly/` | When consumers want Polly v8 resilience policies for handler retries. Wraps `ResiliencePipeline` as an `IRetryPolicy`. |
| **Outbox** | Custom loop with configurable delay | `src/Excalibur/Excalibur.Outbox/` | Background service retry for failed outbox message delivery. Retry interval and max attempts configured via `OutboxProcessingOptions`. |
| **AWS SQS** | AWS SDK retry via `AmazonSQSConfig.MaxErrorRetry` | `src/Dispatch/Excalibur.Dispatch.Transport.AwsSqs/` | SDK-level transient fault handling. Attempt count set by `UseMaxRetryAttempts` on the transport builder. |
| **Google PubSub** | `IRetryPolicyManager` + `RetryPolicyManager` | `src/Dispatch/Excalibur.Dispatch.Transport.GooglePubSub/PubSub/DeadLetter/` | PubSub-specific retry for dead-letter processing. Uses PubSub acknowledgment deadlines. |
| **Persistence Providers** | SDK retry (Cosmos DB, DynamoDB, Firestore, etc.) | Various `src/Excalibur/Excalibur.Data.*` | Database-level transient fault handling. Relies on SDK built-in retry (e.g., Cosmos DB `CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests`). |
| **Leader Election** | Per-provider retry (Kubernetes, Consul) | Various `src/Excalibur/Excalibur.LeaderElection.*` | Leader election renewal retry. Uses provider-specific lease renewal semantics. |

## Architecture Decision

### Why Not a Single Retry Abstraction?

Forcing all subsystems onto `IRetryPolicy` would be incorrect for several reasons:

1. **Transport retries are SDK-specific**: SQS uses visibility timeouts, PubSub uses ack deadlines,
   Kafka uses consumer group rebalancing. These are fundamentally different from handler-level retry.

2. **Persistence retries are SDK-managed**: Cosmos DB, DynamoDB, and Firestore SDKs have built-in
   retry logic optimized for their rate limiting and throttling patterns. Wrapping these in
   `IRetryPolicy` would add overhead with no benefit.

3. **Different retry semantics**: Handler retry re-executes application code. Transport retry
   re-delivers messages. Persistence retry re-attempts database operations. These are different
   concerns at different layers.

### When to Use `IRetryPolicy`

Use `IRetryPolicy` (or `PollyRetryPolicyAdapter`) for:
- Handler-level retry in the Dispatch pipeline
- Custom application-level retry logic
- Scenarios where consumers want to configure retry behavior via `ResiliencePipeline`

### When to Use Subsystem-Specific Retry

Use subsystem-specific retry for:
- Transport-level retry (SQS, PubSub, RabbitMQ, Kafka)
- Persistence-level retry (Cosmos DB, SQL Server, DynamoDB)
- Infrastructure-level retry (leader election, health checks)

## Consolidation Opportunities

These are tracked for future sprints:

1. **PubSub `IRetryPolicyManager`** could wrap `IRetryPolicy` for the retry decision logic,
   while still managing PubSub ack deadlines externally.

This change would reduce code duplication in backoff calculation without forcing architectural
changes on the transport-specific retry semantics.

## Transport Resilience Comparison (Sprint 681)

### Current State

| Transport | Retry | Circuit Breaker | Timeout | Health Check |
|-----------|-------|----------------|---------|-------------|
| **RabbitMQ** | Custom `RetryPolicy` class | No | Connection timeout | Yes (S680) |
| **Kafka** | Confluent SDK retry | No | Producer timeout | Yes (S680) |
| **Azure ServiceBus** | SDK retry (`AmqpRetryOptions`) | No | SDK timeout | No |
| **AWS SQS** | AWS SDK retry (`MaxErrorRetry`) | No | SDK request timeout | No |
| **Google PubSub** | `RetryPolicyManager` | No | gRPC deadline | No |

### Analysis

**No transport currently uses Polly `ResiliencePipeline`.** Each relies on either its SDK's built-in retry (AWS SQS, Kafka, Azure ServiceBus) or a transport-specific retry class (RabbitMQ, Google PubSub). Circuit breaking is available at the dispatch layer via `CircuitBreakerMiddleware` and `Excalibur.Dispatch.Resilience.Polly`, not per-transport.

### Ideal Target State

Each transport uses `ResiliencePipeline` from `Microsoft.Extensions.Resilience` for retry/circuit-breaker/timeout, configured via transport-specific options. This gives consumers consistent resilience behavior across transports while respecting SDK-specific semantics.

### Migration Roadmap

| Sprint | Transport | Effort | Notes |
|--------|-----------|--------|-------|
| Next | **RabbitMQ** | Medium | Custom `RetryPolicy` → Polly `ResiliencePipeline`. Easiest first step since it already has a retry class. |
| Next+1 | **Kafka** | Low | Confluent SDK handles retry internally. Add optional Polly wrapper for connection-level resilience only. |
| Next+2 | **Azure ServiceBus** | Low | Azure SDK handles retry internally (`ServiceBusRetryOptions`). Add optional Polly wrapper for application-level resilience. |
| Next+3 | **Google PubSub** | Medium | `RetryPolicyManager` → Polly. gRPC deadline handling needs to be preserved. |

### Design Constraints

1. **SDK retry is non-negotiable**: Do not replace SDK-level retry (Confluent, Azure, gRPC). Add Polly on top for application-level resilience only.
2. **Circuit breaker placement**: Should be at the transport sender/receiver level, not at the SDK level. Use `TelemetryTransportSender` decorator chain.
3. **Options consistency**: All transports should expose resilience via `ResilienceOptions` sub-property with the same shape (MaxRetryAttempts, BaseDelay, MaxDelay, UseCircuitBreaker, etc.).
