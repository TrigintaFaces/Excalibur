---
sidebar_position: 3
title: Polly Resilience
description: Full Excalibur.Dispatch.Resilience.Polly API — circuit breakers, retry, timeout, bulkhead, and graceful degradation.
---

# Polly Resilience

`Excalibur.Dispatch.Resilience.Polly` provides comprehensive resilience patterns built on [Polly](https://github.com/App-vNext/Polly). It integrates with the Dispatch pipeline to add retry, circuit breaker, timeout, bulkhead, and graceful degradation policies.

For provider-level operational resilience (transient error handling per database), see [Operational Resilience](./resilience.md).

## Before You Start

- Install `Excalibur.Dispatch.Resilience.Polly` (see [Installation](#installation))
- Register the Dispatch pipeline with `AddDispatch()` (see [Getting Started](../getting-started/index.md))
- Understand the three circuit breaker states: **Closed** (normal), **Open** (rejecting), **Half-Open** (testing recovery)

## Installation

```bash
dotnet add package Excalibur.Dispatch.Resilience.Polly
```

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Add Polly resilience to the dispatch pipeline
    dispatch.UseResilience();
});
```

## Registration Options

### Pipeline Integration

```csharp
using Microsoft.Extensions.DependencyInjection;

// Basic — adds all resilience services
dispatch.UseResilience();

// With options
dispatch.UseResilience(options =>
{
    options.Enabled = true;
    options.EnableCircuitBreaker = true;
    options.DefaultRetryCount = 3;
    options.DefaultTimeoutSeconds = 30;
});

// Replace default implementations with Polly adapters
dispatch.AddPollyResilienceAdapters(options =>
{
    // Configure retry
    options.RetryOptions = new RetryOptions
    {
        MaxRetries = 3,
    };
});
```

### Standalone Service Collection Registration

These methods are also available for registering Polly services outside the builder:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Add all Polly resilience services (standalone)
services.AddPollyResilience(configuration);

// Add named circuit breaker
services.AddPollyCircuitBreaker("orders-cb", options =>
{
    options.FailureThreshold = 5;
    options.OpenDuration = TimeSpan.FromSeconds(60);
    options.OperationTimeout = TimeSpan.FromSeconds(5);
});

// Add named retry policy
services.AddPollyRetryPolicy("transient-retry", options =>
{
    options.MaxRetries = 3;
    options.BaseDelay = TimeSpan.FromMilliseconds(200);
    options.BackoffStrategy = BackoffStrategy.Exponential;
    options.UseJitter = true;
});
```

`BackoffStrategy` supports `Fixed`, `Linear`, `Exponential`, `ExponentialWithJitter`, `Fibonacci`,
`FullJitter`, and `DecorrelatedJitter`. Use **`BackoffStrategy.FullJitter`** for AWS-style full jitter — the
delay is sampled uniformly from `[0, min(maxDelay, baseDelay * multiplier^(attempt-1))]`, maximally
decorrelating concurrent clients to avoid a thundering herd on retry:

```csharp
options.BackoffStrategy = BackoffStrategy.FullJitter;
```

Use **`BackoffStrategy.DecorrelatedJitter`** for AWS-style *decorrelated* jitter — each delay is sampled from
`[baseDelay, previousDelay * 3]` and capped at `maxDelay`, threading the previous actual delay forward for
smoother, less-correlated growth than full jitter. Because it depends on prior delay state it is **stateful
and in-process only** (the in-memory retry middleware / Polly path); durable retry paths (outbox/inbox
schedulable stores) intentionally use an attempt-derived strategy instead, so it is not persisted across
restarts:

```csharp
options.BackoffStrategy = BackoffStrategy.DecorrelatedJitter;
```

## Circuit Breaker

### CircuitState

A single canonical `CircuitState` enum is defined in `Excalibur.Dispatch.Resilience`:

```csharp
namespace Excalibur.Dispatch.Resilience;

public enum CircuitState
{
    Closed = 0,   // Normal operation, requests flow through
    Open = 1,     // Failure threshold exceeded, requests rejected
    HalfOpen = 2  // Testing recovery, limited requests allowed
}
```

### ICircuitBreakerPolicy

The local circuit breaker policy interface (5 members):

```csharp
using Excalibur.Dispatch.Resilience;

public interface ICircuitBreakerPolicy
{
    CircuitState State { get; }

    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken);

    void RecordSuccess();
    void RecordFailure(Exception? exception = null);
    void Reset();
}
```

**Usage:**

```csharp
using Excalibur.Dispatch.Resilience;

var result = await circuitBreaker.ExecuteAsync(
    async ct => await httpClient.GetStringAsync("/api/orders", ct),
    cancellationToken);
```

### Diagnostics and Events via GetService()

Diagnostic properties and state-change events are accessed through sub-interfaces using the `GetService()` pattern, keeping the core interface minimal:

Test the policy for the optional interfaces directly. A policy that measures nothing does not
implement `ICircuitBreakerDiagnostics`, so a failed test means no breaker is installed — which is a
different thing from a breaker that is installed and closed:

```csharp
using Excalibur.Dispatch.Resilience;

if (circuitBreaker is ICircuitBreakerDiagnostics diagnostics)
{
    Console.WriteLine($"Consecutive failures: {diagnostics.ConsecutiveFailures}");
    Console.WriteLine($"Last opened: {diagnostics.LastOpenedAt}");
}
else
{
    Console.WriteLine("No circuit breaker is installed; nothing is being measured.");
}

if (circuitBreaker is ICircuitBreakerEvents events)
{
    events.StateChanged += (sender, args) =>
        Console.WriteLine($"Circuit state changed: {args}");
}
```

**ICircuitBreakerDiagnostics:**

| Member | Type | Description |
|--------|------|-------------|
| `ConsecutiveFailures` | `int` | Failures since the last success |
| `LastOpenedAt` | `DateTimeOffset?` | When the circuit was last opened |

**ICircuitBreakerEvents:**

| Member | Type | Description |
|--------|------|-------------|
| `StateChanged` | `EventHandler<CircuitStateChangedEventArgs>?` | Raised on circuit state transitions |

### Named Circuit Breakers

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddPollyCircuitBreaker("payment-service", options =>
{
    options.FailureThreshold = 5;
    options.FailureRatio = 0.5;
    options.SamplingDuration = TimeSpan.FromSeconds(30);
    options.OpenDuration = TimeSpan.FromSeconds(60);
    options.OperationTimeout = TimeSpan.FromSeconds(5);
});
```

This breaker opens on a **failure proportion measured over a rolling window**, not on a run of
consecutive failures:

| Setting | Default | Meaning on this breaker |
| --- | --- | --- |
| `FailureThreshold` | `5` | Minimum calls that must be observed within `SamplingDuration` before `FailureRatio` is evaluated at all. Must be at least 2 — a proportion needs two calls to exist, and a lower value is rejected at construction. |
| `FailureRatio` | `0.5` | Proportion of observed calls that must have failed for the circuit to open. |
| `SamplingDuration` | 30s | The rolling window the proportion is measured over. Minimum 500 ms. |

The practical consequence is worth stating plainly: a run of failures that is diluted by enough
successes inside the same window keeps the proportion below `FailureRatio`, and the circuit stays
closed. If you want the circuit to open on a run of consecutive failures regardless of surrounding
traffic, use the count-based circuit breaker policy rather than this one — it opens on
`FailureThreshold` consecutive failures and accepts a threshold of 1.

The named circuit breaker admits **one** trial call while half-open and closes if it succeeds. That
is not configurable, and it is a different type from `DistributedCircuitBreakerOptions` below, which
*does* expose `SuccessThresholdToClose` because it implements consecutive-success closing itself.

```csharp
```

### Distributed Circuit Breaker

For multi-instance deployments, `IDistributedCircuitBreaker` shares circuit state across instances
(5 members):

```csharp
using Excalibur.Dispatch.Resilience.Polly;

public interface IDistributedCircuitBreaker
{
    Task<CircuitState> GetStateAsync(CancellationToken cancellationToken);

    Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken);

    Task RecordSuccessAsync(CancellationToken cancellationToken);
    Task RecordFailureAsync(CancellationToken cancellationToken, Exception? exception = null);
    Task ResetAsync(CancellationToken cancellationToken);
}
```

**Registration.** Register a cross-instance cache *before* the breaker, and resolve the breaker by
name:

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));

builder.Services.AddDistributedCircuitBreaker("payments", options =>
{
    options.ConsecutiveFailureThreshold = 5;
    options.BreakDuration = TimeSpan.FromSeconds(30);
});

// ...
var breaker = serviceProvider.GetRequiredKeyedService<IDistributedCircuitBreaker>("payments");
```

`AddDistributedCircuitBreaker` does not register a cache of its own. If no `IDistributedCache` is
registered, or the one registered is the in-process `AddDistributedMemoryCache()`, the host **fails at
startup** and the message names the remedy. That is deliberate: an in-process cache is not shared
between instances, so every replica would trip its own circuit and never observe another's — a
per-instance breaker behind a name that promises coordination, with nothing at runtime to reveal it.
For a deliberately per-instance breaker, use `AddPollyCircuitBreaker` instead.

:::info What is shared, and what is not
**Shared:** the circuit state. When any instance trips, it writes `Open` to the store and every other
instance short-circuits on its next call, so a failing dependency is shed fleet-wide rather than by each
replica in turn. Recovery is shared the same way — the first instance past `BreakDuration` moves the
shared state to half-open, and the successes that close it are visible to all.

**Not shared:** the counting that decides *when* to trip. Each instance evaluates `FailureRatio`,
`MinimumThroughput`, `SamplingDuration` and `ConsecutiveFailureThreshold` over its own traffic, so those
thresholds are **per-instance, not fleet-wide totals**. Size them against the traffic one replica sees,
not the traffic all of them see together.

This is a property of the store, not an omission. `IDistributedCache` exposes only Get, Set and Remove —
no atomic increment and no compare-and-swap — so a shared counter could only be maintained by a
read-modify-write that loses updates between instances. Under that scheme the breaker under-counts and
opens late or never, which is the failure it exists to prevent. A per-instance count is exact and its
meaning is stated; a shared count over this abstraction would be neither.
:::

**Configuration:**

```json
{
  "Resilience": {
    "DistributedCircuitBreaker": {
      "Enabled": true,
      "SyncInterval": "00:00:05",
      "FailureRatio": 0.5,
      "MinimumThroughput": 10,
      "SamplingDuration": "00:00:30",
      "BreakDuration": "00:00:30",
      "ConsecutiveFailureThreshold": 5,
      "SuccessThresholdToClose": 3
    }
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FailureRatio` | `double` | `0.5` | Failure ratio (0.0–1.0) within the rolling `SamplingDuration` window that trips the circuit to **Open** |
| `MinimumThroughput` | `int` | `10` | Minimum number of attempts that must accrue **within the `SamplingDuration` window** before the failure ratio is evaluated. Below this throughput the circuit never opens on ratio (avoids tripping on a handful of early failures) |
| `SamplingDuration` | `TimeSpan` | 30s | Rolling window over which the failure ratio is measured |
| `BreakDuration` | `TimeSpan` | 30s | How long the circuit stays **Open** before the next call is allowed through as a probe (transition to **Half-Open**) |
| `ConsecutiveFailureThreshold` | `int` | `5` | Consecutive failures that trip the circuit to **Open**, independent of the windowed ratio |
| `SuccessThresholdToClose` | `int` | `3` | Consecutive successes required while **Half-Open** to recover to **Closed** |

:::info Failure ratio is windowed, not lifetime-cumulative
The circuit opens when, **within the rolling `SamplingDuration` window**, at least `MinimumThroughput`
attempts have accumulated **and** the failure ratio meets or exceeds `FailureRatio` (mirroring Polly v8's
`CircuitBreakerStrategyOptions`). Lifetime-cumulative counts do not trip the ratio path — only attempts
inside the current window count. The `ConsecutiveFailureThreshold` path is independent and trips on a run
of consecutive failures regardless of throughput.
:::

:::info Half-Open → Closed recovery
After `BreakDuration` elapses the breaker admits a probe call (**Half-Open**). Each
`RecordSuccessAsync` increments a consecutive-success counter and resets it to zero on any failure;
once `SuccessThresholdToClose` **consecutive** successes are recorded while Half-Open, the circuit
transitions back to **Closed** automatically. (A single in-flight failure during Half-Open resets the
counter and re-opens the circuit.) Recovery is keyed off the breaker's own `ConsecutiveSuccesses`
metric, so a long-running service recovers correctly rather than getting stuck Half-Open.
:::

### Transport Circuit Breaker Registry

`ITransportCircuitBreakerRegistry` manages per-transport circuit breakers so that failures in one transport do not affect others (3 members):

```csharp
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

public interface ITransportCircuitBreakerRegistry
{
    ICircuitBreakerPolicy GetOrCreate(string transportName);
    ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options);
    ICircuitBreakerPolicy? TryGet(string transportName);
}
```

:::caution The registry holds at most 1024 distinct circuits

Once 1024 circuits exist, a key that is not already present shares one overflow circuit instead of
allocating another, so the map cannot grow without bound. Protection is preserved but coarser: one
failing key can open the circuit for every other key that landed in the overflow.

This matters when the key is derived from the message. `CircuitBreakerOptions.CircuitKeySelector` is
a `Func<IDispatchMessage, string>` you supply, so a selector returning a tenant id, a route or any
other high-cardinality value can exceed the cap. **Return a bounded set of keys** — bucket by route
family or tenant tier rather than by identity. Transport names, the intended use of this registry,
are naturally far below the cap.

Diagnostics reflect the sharing: `GetAllStates()` reports the shared circuit under the reserved key
`__overflow__`, and `TryGet(...)` returns `null` for a key that ended up in it.

:::

**Usage:**

```csharp
using Excalibur.Dispatch.Resilience;

// Get or create a circuit breaker for a specific transport
var breaker = registry.GetOrCreate("RabbitMQ");
var state = breaker.State; // Closed, Open, or HalfOpen

// With custom options
var customBreaker = registry.GetOrCreate("AzureServiceBus", new CircuitBreakerOptions
{
    FailureThreshold = 3,
    OpenDuration = TimeSpan.FromSeconds(30)
});
```

**Diagnostics via GetService():**

Administrative operations are on a separate `ITransportCircuitBreakerDiagnostics` interface:

```csharp
using Excalibur.Dispatch.Resilience;

// A registry is not obliged to expose diagnostics, so test the instance for the facet.
// The registries shipped in the box implement it.
if (registry is ITransportCircuitBreakerDiagnostics diagnostics)
{
    var count = diagnostics.Count;
    var states = diagnostics.GetAllStates();
    var names = diagnostics.GetTransportNames();

    diagnostics.ResetAll();
    diagnostics.Remove("OldTransport");
}
```

| Member | Return Type | Description |
|--------|-------------|-------------|
| `Count` | `int` | Number of registered circuit breakers |
| `Remove(string)` | `bool` | Remove a transport's circuit breaker |
| `ResetAll()` | `void` | Reset all circuit breakers to Closed |
| `GetAllStates()` | `IReadOnlyDictionary<string, CircuitState>` | States of all registered breakers |
| `GetTransportNames()` | `IEnumerable<string>` | Names of all registered transports |

## Policy Types

### Retry

Automatic retry with configurable backoff for transient failures:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddPollyRetryPolicy("my-retry", options =>
{
    options.MaxRetries = 3;
    options.BaseDelay = TimeSpan.FromMilliseconds(200);
    options.BackoffStrategy = BackoffStrategy.Exponential;  // 200ms, 400ms, 800ms
    options.UseJitter = true;                               // Decorrelated jitter
});
```

The Polly adapter replaces the default `IRetryPolicy` with `PollyRetryPolicyAdapter`, providing decorrelated jitter and advanced retry strategies.

### Timeout

Prevent operations from blocking indefinitely:

```json
{
  "Resilience": {
    "Timeouts": {
      "DefaultTimeout": "00:00:30",
      "OperationTimeouts": {
        "database-query": "00:00:10",
        "external-api": "00:01:00"
      }
    }
  }
}
```

`ITimeoutManager` and `TimeoutManager` manage named timeout policies per operation type.

### Bulkhead

Limit concurrent executions to prevent resource exhaustion. A bulkhead admits up to `MaxConcurrency`
operations to run simultaneously; additional callers wait in a bounded queue of up to `MaxQueueLength`
waiters, and callers beyond that are rejected immediately with a `BulkheadRejectedException`.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddBulkhead("external-api", options =>
{
    options.MaxConcurrency = 10;  // concurrent executions allowed (default 10)
    options.MaxQueueLength = 50;  // additional callers allowed to wait (default 50)
});
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxConcurrency` | `int` | `10` | Maximum operations executing concurrently (must be ≥ 1) |
| `MaxQueueLength` | `int` | `50` | Maximum callers allowed to wait for a slot (must be ≥ 0) |
| `AllowQueueing` | `bool` | `true` | Whether callers may wait at all. Set `false` to reject immediately once `MaxConcurrency` is reached, ignoring `MaxQueueLength` |

:::info `MaxQueueLength` is a hard admission bound, and the queue is FIFO
Admission is handled by `System.Threading.RateLimiting.ConcurrencyLimiter`, so the bound holds under
contention rather than depending on a check-then-act gate: a caller arriving when the queue is full is
rejected immediately with `BulkheadRejectedException`, and the waiter count surfaced through
`BulkheadMetrics.QueueLength` and `HasCapacity` reflects the limiter's own accounting.

Waiters are admitted oldest-first, so the queue is FIFO. A caller already waiting is never evicted to
make room for one that arrives later — the newcomer is rejected instead.
:::

The bulkhead manager (resolved via DI as `IBulkheadManager`) manages named bulkhead isolations to prevent one slow operation from consuming all available threads.

### Graceful Degradation

Graceful degradation returns reduced or cached responses when dependencies are unavailable. Configuration uses a collection-based `Levels` pattern (following Polly v8 `RetryStrategyOptions` as reference):

```csharp
using Excalibur.Dispatch.Resilience.Polly;

public sealed class GracefulDegradationOptions
{
    public bool EnableAutoAdjustment { get; set; } = true;
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MinimumLevelDuration { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan ErrorRateWindow { get; set; } = TimeSpan.FromMinutes(1);
    public int ErrorRateWindowBuckets { get; set; } = 6;
    public List<DegradationLevelConfig> Levels { get; set; } = DefaultLevels();
}

public record DegradationLevelConfig(
    string Name,
    int PriorityThreshold,
    double ErrorRateThreshold,
    double CpuThreshold,
    double MemoryThreshold);
```

**Core properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableAutoAdjustment` | `bool` | `true` | Allow automatic level changes based on health |
| `HealthCheckInterval` | `TimeSpan` | 30s | Cadence between health evaluation cycles |
| `MinimumLevelDuration` | `TimeSpan` | 1m | Minimum time before a level can be reevaluated |
| `ErrorRateWindow` | `TimeSpan` | 1m | Sliding window over which the auto-degradation error rate is measured |
| `ErrorRateWindowBuckets` | `int` | `6` | Number of buckets the `ErrorRateWindow` is divided into (rollover granularity) |
| `Levels` | `List<DegradationLevelConfig>` | 5 defaults | Ordered degradation level configurations |

:::info Error rate is windowed, not lifetime-cumulative
Auto-degradation evaluates the error rate over a **sliding `ErrorRateWindow`** (a Polly v8-style
rolling-health window divided into `ErrorRateWindowBuckets` buckets), not a lifetime-cumulative
ratio. Previously the error rate was computed from process-lifetime totals, so in a long-running
service the ever-growing denominator meant a recent burst of failures could no longer move the ratio
and error-rate auto-degradation effectively stopped firing after warm-up. With the rolling window, a
recent burst of failures triggers degradation regardless of process uptime, while old failures age
out as the window advances. CPU and memory signals are unchanged.

Tuning: a larger `ErrorRateWindow` smooths the signal (slower to react, slower to recover); more
`ErrorRateWindowBuckets` ages out old samples more granularly. It is **recommended** (but not
required) that `ErrorRateWindow >= HealthCheckInterval` so each health check sees a fully-covered
window — a shorter window is valid but leaves an inter-check blind spot.
:::

:::note Startup validation (`ValidateOnStart`)
When you bind `GracefulDegradationOptions` from configuration or via `ConfigureGracefulDegradation`,
an `IValidateOptions<GracefulDegradationOptions>` runs at host startup and fails fast if
`HealthCheckInterval <= TimeSpan.Zero`, `ErrorRateWindow <= TimeSpan.Zero`, or
`ErrorRateWindowBuckets < 1`.
:::

**Default levels:**

| Name | Priority Threshold | Error Rate | CPU | Memory |
|------|-------------------|------------|-----|--------|
| Minor | 10 | 1% | 60% | 60% |
| Moderate | 30 | 5% | 70% | 70% |
| Major | 50 | 10% | 80% | 80% |
| Severe | 70 | 25% | 90% | 90% |
| Emergency | 100 | 50% | 95% | 95% |

**Configuration via appsettings.json:**

```json
{
  "Resilience": {
    "GracefulDegradation": {
      "EnableAutoAdjustment": true,
      "HealthCheckInterval": "00:00:30",
      "MinimumLevelDuration": "00:01:00",
      "ErrorRateWindow": "00:01:00",
      "ErrorRateWindowBuckets": 6
    }
  }
}
```

`IGracefulDegradationService` provides fallback behavior when primary operations fail, returning cached or default responses.

## Polly Adapter Replacements

`AddPollyResilienceAdapters()` replaces default implementations with Polly-based adapters:

| Interface | Default | Polly Adapter |
|-----------|---------|---------------|
| `IRetryPolicy` | Built-in | `PollyRetryPolicyAdapter` |
| `ICircuitBreakerPolicy` | Built-in | `PollyCircuitBreakerPolicyAdapter` |
| `IBackoffCalculator` | Built-in | Polly adapter (internal, resolved via DI) |
| `ITransportCircuitBreakerRegistry` | Built-in | `PollyTransportCircuitBreakerRegistry` |

## Configuration

```json
{
  "Resilience": {
    "Timeouts": {
      "DefaultTimeout": "00:00:30"
    },
    "GracefulDegradation": {
      "EnableAutoAdjustment": true,
      "HealthCheckInterval": "00:00:30",
      "ErrorRateWindow": "00:01:00",
      "ErrorRateWindowBuckets": 6
    },
    "DistributedCircuitBreaker": {
      "Enabled": false,
      "SyncInterval": "00:00:05",
      "FailureRatio": 0.5,
      "MinimumThroughput": 10,
      "SamplingDuration": "00:00:30",
      "BreakDuration": "00:00:30",
      "ConsecutiveFailureThreshold": 5,
      "SuccessThresholdToClose": 3
    }
  }
}
```

## Policy Composition

Combine multiple policies for defense-in-depth:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Add Polly with all adapters
    dispatch.AddPollyResilienceAdapters(options =>
    {
        options.RetryOptions = new RetryOptions
        {
            MaxRetries = 3,
        };
    });
});

// Named policies for specific operations
services.AddPollyCircuitBreaker("external-api", options =>
{
    options.FailureThreshold = 3;
    options.OpenDuration = TimeSpan.FromSeconds(30);
});

services.AddPollyRetryPolicy("idempotent-ops", options =>
{
    options.MaxRetries = 5;
    options.BackoffStrategy = BackoffStrategy.Exponential;
    options.UseJitter = true;
});
```

## See Also

- [Operational Resilience](./resilience.md) -- Provider-level transient error handling
- [Observability](../observability/index.md) -- Monitor resilience metrics
- [Middleware Pipeline](../middleware/index.md) -- How resilience integrates with the pipeline
- [Health Checks](../observability/health-checks.md) -- Monitor circuit breaker and service health
