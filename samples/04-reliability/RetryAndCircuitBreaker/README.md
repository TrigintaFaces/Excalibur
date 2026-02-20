# Retry and Circuit Breaker Sample

This sample demonstrates **resilience patterns** using Polly with the Dispatch framework.

## What This Sample Shows

1. **Retry with Exponential Backoff** - Handle transient failures with smart retries
2. **Circuit Breaker** - Protect against cascading failures
3. **Timeout Handling** - Prevent indefinite waits
4. **Bulkhead Isolation** - Protect critical resources

## Resilience Patterns Explained

### Retry Pattern

Automatically retry failed operations with configurable backoff:

```
Attempt 1: Fail -> Wait 200ms
Attempt 2: Fail -> Wait 400ms (+ jitter)
Attempt 3: Fail -> Wait 800ms (+ jitter)
Attempt 4: Success!
```

**Key features:**
- Exponential backoff prevents overwhelming services
- Jitter prevents thundering herd
- Configurable retry predicate

### Circuit Breaker Pattern

Prevent cascading failures by "breaking the circuit":

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

**Key features:**
- Fast-fail when dependency is down
- Automatic recovery testing
- Configurable thresholds

### Timeout Pattern

Prevent indefinite waits on slow operations:

```csharp
// Operation times out after 5 seconds
await ExecuteWithTimeout(operation, TimeSpan.FromSeconds(5));
```

### Bulkhead Pattern

Isolate resources to prevent exhaustion:

```
[Bulkhead: max 5 concurrent, queue 10]
   Request 1 -> Executing
   Request 2 -> Executing
   Request 3 -> Executing
   Request 4 -> Executing
   Request 5 -> Executing
   Request 6 -> Queued
   Request 7 -> Queued
   ...
   Request 16 -> Rejected (queue full)
```

## Configuration

### Retry Policy

```csharp
builder.Services.AddPollyRetryPolicy("payment-retry", options =>
{
    options.MaxRetries = 5;                                // Max attempts
    options.BaseDelay = TimeSpan.FromMilliseconds(200);    // Initial delay
    options.BackoffStrategy = BackoffStrategy.Exponential; // Backoff type
    options.UseJitter = true;                              // Add randomness
    options.JitterFactor = 0.3;                            // 30% jitter
    options.MaxDelay = TimeSpan.FromSeconds(10);           // Max delay cap
    options.ShouldRetry = ex => ex is TransientException;  // Retry predicate
});
```

### Circuit Breaker

```csharp
builder.Services.AddPollyCircuitBreaker("inventory-circuit", options =>
{
    options.FailureThreshold = 3;                    // Open after 3 failures
    options.SuccessThreshold = 2;                    // Close after 2 successes
    options.OpenDuration = TimeSpan.FromSeconds(10); // Cooldown period
    options.OperationTimeout = TimeSpan.FromSeconds(5);
});
```

### Timeout Manager

```csharp
builder.Services.ConfigureTimeoutManager(options =>
{
    options.DefaultTimeout = TimeSpan.FromSeconds(5);
    options.OperationTimeouts["payment"] = TimeSpan.FromSeconds(10);
    options.OperationTimeouts["notification"] = TimeSpan.FromSeconds(2);
});
```

### Bulkhead

```csharp
builder.Services.AddBulkhead("notification-bulkhead", options =>
{
    options.MaxConcurrency = 5;
    options.MaxQueueLength = 10;
    options.OperationTimeout = TimeSpan.FromSeconds(5);
});
```

## Running the Sample

```bash
cd samples/04-reliability/RetryAndCircuitBreaker
dotnet run
```

## Expected Output

```
Starting Retry and Circuit Breaker Sample...

=== Demo 1: Retry with Exponential Backoff ===

Retry pattern handles transient failures:
  - Automatic retry with configurable attempts
  - Exponential backoff prevents overwhelming services
  - Jitter prevents thundering herd

Sending payment (will fail 2 times, then succeed)...
[PaymentService] Payment PAY-001 failed (attempt 1/3)
[PaymentService] Payment PAY-001 failed (attempt 2/3)
[PaymentService] Payment PAY-001 succeeded on attempt 3
Payment completed successfully!

=== Demo 2: Circuit Breaker Pattern ===

Checking inventory (healthy service)...
[InventoryService] SKU WIDGET-001: 42 available

Simulating inventory service failure...
Request 1 failed: InventoryServiceException
Request 2 failed: InventoryServiceException
Request 3 failed: BrokenCircuitException  <- Circuit opened!
Request 4 failed: BrokenCircuitException
Request 5 failed: BrokenCircuitException
```

## Configuration Options

### Retry Options

| Option | Default | Description |
|--------|---------|-------------|
| `MaxRetries` | 3 | Maximum retry attempts |
| `BaseDelay` | 1 second | Initial delay between retries |
| `BackoffStrategy` | Exponential | Linear, Exponential, or Constant |
| `UseJitter` | true | Add randomness to prevent thundering herd |
| `JitterStrategy` | Equal | Full, Equal, or Decorrelated jitter |
| `JitterFactor` | 0.2 | Jitter magnitude (0.0-1.0) |
| `MaxDelay` | 1 minute | Maximum delay cap |
| `OperationTimeout` | null | Overall timeout for all retries |

### Circuit Breaker Options

| Option | Default | Description |
|--------|---------|-------------|
| `FailureThreshold` | 5 | Consecutive failures to open circuit |
| `SuccessThreshold` | 3 | Successes needed to close from half-open |
| `OpenDuration` | 30 seconds | Time circuit stays open |
| `OperationTimeout` | 5 seconds | Timeout for each operation |
| `MaxHalfOpenTests` | 3 | Max concurrent tests in half-open |

### Bulkhead Options

| Option | Default | Description |
|--------|---------|-------------|
| `MaxConcurrency` | 10 | Max concurrent operations |
| `MaxQueueLength` | 50 | Max queued operations |
| `OperationTimeout` | 30 seconds | Timeout for operations |

## Best Practices

1. **Combine patterns**: Use retry + circuit breaker + timeout together
2. **Tune thresholds**: Adjust based on SLAs and failure patterns
3. **Use jitter**: Always enable for distributed systems
4. **Monitor metrics**: Track retry counts, circuit state, timeouts
5. **Fail fast**: Don't retry non-transient errors

## Pattern Combinations

### Recommended: Retry inside Circuit Breaker

```
Request -> CircuitBreaker[
              Retry[
                 Timeout[
                    Operation
                 ]
              ]
           ]
```

This ensures:
1. Circuit opens after repeated failures
2. Retries happen within each circuit attempt
3. Each operation has a timeout

## Project Structure

```
RetryAndCircuitBreaker/
RetryAndCircuitBreaker.csproj  # Project file
Program.cs                      # Main sample with demos
appsettings.json                # Configuration
README.md                       # This file
Messages/
   ExternalServiceEvents.cs     # Command and event classes
Handlers/
    ExternalServiceHandlers.cs  # Handlers using external services
Services/
    FlakyExternalServices.cs    # Simulated unreliable services
```

## Related Samples

- [Outbox Pattern](../OutboxPattern/) - Reliable message delivery
- [Saga Orchestration](../SagaOrchestration/) - Multi-step workflows
