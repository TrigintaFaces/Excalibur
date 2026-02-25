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
    dispatch.AddDispatchResilience();
});
```

## Registration Options

### Pipeline Integration

```csharp
using Microsoft.Extensions.DependencyInjection;

// Basic — adds all resilience services
dispatch.AddDispatchResilience();

// With options
dispatch.AddDispatchResilience(options =>
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
    options.SuccessThreshold = 3;
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

```csharp
using Excalibur.Dispatch.Resilience;

// Access diagnostic information
if (circuitBreaker is IServiceProvider provider)
{
    var diagnostics = provider.GetService(typeof(ICircuitBreakerDiagnostics))
        as ICircuitBreakerDiagnostics;

    if (diagnostics is not null)
    {
        Console.WriteLine($"Consecutive failures: {diagnostics.ConsecutiveFailures}");
        Console.WriteLine($"Last opened: {diagnostics.LastOpenedAt}");
    }

    var events = provider.GetService(typeof(ICircuitBreakerEvents))
        as ICircuitBreakerEvents;

    if (events is not null)
    {
        events.StateChanged += (sender, args) =>
            Console.WriteLine($"Circuit state changed: {args}");
    }
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
    options.SuccessThreshold = 3;
    options.OpenDuration = TimeSpan.FromSeconds(60);
    options.OperationTimeout = TimeSpan.FromSeconds(5);
});
```

### Distributed Circuit Breaker

For multi-instance deployments, `IDistributedCircuitBreaker` shares state across instances (5 members):

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

**Configuration:**

```json
{
  "Resilience": {
    "DistributedCircuitBreaker": {
      "Enabled": true,
      "SyncInterval": "00:00:05"
    }
  }
}
```

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

// Access via GetService() on the registry instance
if (registry is IServiceProvider provider)
{
    var diagnostics = provider.GetService(typeof(ITransportCircuitBreakerDiagnostics))
        as ITransportCircuitBreakerDiagnostics;

    if (diagnostics is not null)
    {
        var count = diagnostics.Count;
        var states = diagnostics.GetAllStates();
        var names = diagnostics.GetTransportNames();

        diagnostics.ResetAll();
        diagnostics.Remove("OldTransport");
    }
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

Limit concurrent executions to prevent resource exhaustion:

```csharp
// BulkheadManager limits concurrent calls
// Configured via Polly bulkhead policies
```

`BulkheadManager` manages named bulkhead isolations to prevent one slow operation from consuming all available threads.

### Graceful Degradation

Graceful degradation returns reduced or cached responses when dependencies are unavailable. Configuration uses a collection-based `Levels` pattern (following Polly v8 `RetryStrategyOptions` as reference):

```csharp
using Excalibur.Dispatch.Resilience.Polly;

public class GracefulDegradationOptions
{
    public bool EnableAutoAdjustment { get; set; } = true;
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MinimumLevelDuration { get; set; } = TimeSpan.FromMinutes(1);
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
| `Levels` | `List<DegradationLevelConfig>` | 5 defaults | Ordered degradation level configurations |

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
      "MinimumLevelDuration": "00:01:00"
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
| `IBackoffCalculator` | Built-in | `PollyBackoffCalculatorAdapter` |
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
      "HealthCheckInterval": "00:00:30"
    },
    "DistributedCircuitBreaker": {
      "Enabled": false,
      "SyncInterval": "00:00:05"
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
