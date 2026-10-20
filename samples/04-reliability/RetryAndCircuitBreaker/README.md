# Retry and Circuit Breaker

This Dispatch-only sample executes the framework's Polly policies around simulated downstream dependencies. Each handler receives a named policy through `[FromKeyedServices]`; every message gets a fresh dispatch context.

| Dependency | Framework policy | Observable behavior |
| --- | --- | --- |
| Payment | `AddPollyRetryPolicy("payment-retry", ...)` | Two transient failures, three attempts, one completed payment |
| Inventory | `AddPollyCircuitBreaker("inventory-circuit", ...)` | Failures open the breaker; refused calls never reach inventory; explicit reset restores service |
| Notifications | `AddBulkhead("notification-bulkhead", ...)` | The operation observes Polly's timeout token; canceled work releases capacity for the next notification |

Run the demonstration:

```shell
dotnet run --project samples/04-reliability/RetryAndCircuitBreaker
```

Run the bounded verification scenario:

```shell
dotnet run --project samples/04-reliability/RetryAndCircuitBreaker -- --verify
```

Verification exits unsuccessfully if any assertion fails. It also proves persistent payment failure stops after five retries (six total attempts) with no completed payment. A successful run prints `RESILIENCE_SAMPLE_VERIFIED`. Verification uses shorter deterministic retry delays and a 100 ms notification timeout; the normal demonstration uses jitter and a five-second timeout. No external infrastructure is required.

## Dependency ownership

The named payment policy owns payment retries. Dispatcher retries are left disabled, so a single dispatched command does not multiply the dependency budget. In a real payment integration, use a stable idempotency key and confirm the provider's duplicate handling before retrying an ambiguous outcome after a request might have committed.

The inventory breaker uses `MinimumThroughput` and `FailureRatio`, which are Polly's rolling-window controls. The normal demonstration uses four observations and a 75% failure ratio; verification starts with two failures and a 100% ratio. Recovery here is an explicit `ResetAsync` operation; this scenario does not certify timed half-open recovery.

The notification handler uses the token-taking `IBulkheadPolicy.ExecuteAsync` overload and passes its token to the service. A cooperative timeout waits for that operation to finish cancellation before releasing its permit. The legacy tokenless overload cannot supply a strategy token to the operation.

```csharp
public ProcessPaymentHandler(
    FlakyPaymentService paymentService,
    [FromKeyedServices("payment-retry")] IRetryPolicy retry,
    ILogger<ProcessPaymentHandler> logger)
{
    // Store these dependencies; HandleAsync executes only the downstream call through retry.
}

var transactionId = await retry.ExecuteAsync(
    token => paymentService.ProcessPaymentAsync(paymentId, amount, token),
    cancellationToken);
```

`appsettings.json` configures logging. Named resilience options are configured explicitly in `Program.cs`, where their executing handlers are visible. The simulated services expose attempt, completion, and cancellation counters; these are teaching doubles, while retry, breaker, timeout, and concurrency behavior comes from the framework.

For delivery persistence, continue with [OutboxPattern](../OutboxPattern/). For multi-step workflows, see [SagaOrchestration](../SagaOrchestration/).