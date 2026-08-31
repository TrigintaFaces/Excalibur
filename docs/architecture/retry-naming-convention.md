# Retry Naming Convention

## Standard

All retry count properties across the Excalibur.Dispatch framework MUST use the name `MaxRetryAttempts`.

### Why `MaxRetryAttempts`?

- **Unambiguous**: "MaxRetries" is ambiguous — does `MaxRetries = 3` mean 3 retries (4 total attempts) or 3 total? `MaxRetryAttempts` is self-documenting: it is the maximum number of retry attempts, excluding the initial attempt.
- **Consistent with Microsoft patterns**: `Azure.Core.RetryOptions.MaxRetries` exists but `Microsoft.Extensions.Resilience` uses descriptive naming. Our framework uses the more descriptive form.
- **Most widely used**: The majority of our codebase already uses `MaxRetryAttempts`.

### Semantic distinction: `MaxRetryAttempts` vs `MaxAttempts`

| Property | Meaning | Example: value = 3 |
|----------|---------|---------------------|
| `MaxRetryAttempts` | Number of retry attempts after the first attempt fails | 1 initial + 3 retries = 4 total |
| `MaxAttempts` | Total number of attempts including the first | 3 total attempts |

Both names are valid but serve different purposes:

- Use `MaxRetryAttempts` for retry policies, resilience options, and transport retry configuration.
- Use `MaxAttempts` only in delivery/processing contexts where the semantic is "total attempts" (e.g., `OutboxOptions.MaxAttempts`, `InboxOptions.MaxAttempts`).

## Current State Audit (Sprint 681)

### Correct (`MaxRetryAttempts`) — 30+ usages

Already aligned. No changes needed.

### Needs Rename (`MaxRetries` → `MaxRetryAttempts`) — 23 usages

| File | Package | Property |
|------|---------|----------|
| `RabbitMQTransportOptions.cs` | Transport.RabbitMQ | `MaxRetries` |
| `ClaimCheckOperationOptions.cs` | Dispatch.Patterns | `MaxRetries` |
| `RabbitMqRetryOptions.cs` | Transport.RabbitMQ | `MaxRetries` |
| `SecretRotationPolicy.cs` | Data.ElasticSearch | `MaxRetries` |
| `CredentialRotationOptions.cs` | Data.ElasticSearch | `MaxRetries` |
| `KafkaRetryOptions.cs` | Transport.Kafka | `MaxRetries` |
| `KubernetesLeaderElectionOptions.cs` | LeaderElection.Kubernetes | `MaxRetries` |
| `SagaCompensationAttribute.cs` | Saga | `MaxRetries` |
| `OutboxBackgroundService.cs` (options) | Outbox | `MaxRetries` |
| `OrderingKeyOptions.cs` | Transport.GooglePubSub | `MaxRetries` |
| `AzureRetryOptions.cs` | Transport.AzureServiceBus | `MaxRetries` |
| `AzureLogicAppsSchedulerOptions.cs` | Transport.AzureServiceBus | `MaxRetries` |
| `AwsProviderOptions.cs` | Transport.AwsSqs | `MaxRetries` |
| `EventBridgeSchedulerOptions.cs` | Transport.AwsSqs | `MaxRetries` |
| `DlqOptions.cs` | Transport.AwsSqs | `MaxRetries` |
| `RetryPolicyOptions.cs` | Transport.AwsSqs | `MaxRetries` |
| `RetryOptions.cs` | Resilience.Polly | `MaxRetries` |
| `OutboxMiddlewareRetryOptions.cs` | Dispatch | `MaxRetries` |
| `OutboxConfigurationOptions.cs` | Dispatch | `MaxRetries` |
| `InboxOptions.cs` (Configuration) | Dispatch | `MaxRetries` |
| `ConsumerOptions.cs` | Dispatch | `MaxRetries` |

### Acceptable Exceptions (`MaxAttempts`)

These use `MaxAttempts` intentionally because their semantic is "total attempts" (not retries):

| File | Package | Property | Rationale |
|------|---------|----------|-----------|
| `OutboxDeliveryOptions.cs` | Dispatch | `MaxAttempts` | Total delivery attempts |
| `InboxOptions.cs` (Delivery) | Dispatch | `MaxAttempts` | Total processing attempts |
| `DeadLetterOptions.cs` | Dispatch | `MaxAttempts` | Total DLQ attempts |
| `BackoffOptions.cs` | Transport.GooglePubSub | `MaxAttempts` | Total backoff attempts |
| `RetryPolicyOptions.cs` | Data.ElasticSearch | `MaxAttempts` | Total ES retry attempts |
| `DataProcessingOptions.cs` | Data.DataProcessing | `MaxAttempts` | Total processing attempts |

## Migration Plan

Since this is a pre-release framework with no consumers, all renames are safe binary-breaking changes.

### Priority Order

1. **Sprint 681**: Document convention (this file), rename transport Options properties
2. **Next sprint**: Rename remaining packages (Dispatch core, Patterns, Resilience.Polly, Saga, LeaderElection, ElasticSearch)
3. **Follow-up**: Update `PublicAPI.Shipped.txt` baseline files for all affected packages
