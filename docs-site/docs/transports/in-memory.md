---
sidebar_position: 2
title: In-Memory Transport
description: Built-in in-memory transport for testing and development
---

# In-Memory Transport
The in-memory transport is included with Dispatch and is ideal for testing, development, and single-process scenarios.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [choosing a transport](./choosing-a-transport.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Quick Start
```csharp
builder.Services.AddDispatch(typeof(Program).Assembly);
builder.Services.AddInMemoryTransport("inmemory");
```

## Configuration
```csharp
builder.Services.AddDispatch(typeof(Program).Assembly);
builder.Services.AddInMemoryTransport("inmemory", options =>
{
    // Channel capacity for bounded message queue (default: 1000)
    // Producers will wait when capacity is reached
    options.ChannelCapacity = 2000;
});
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `ChannelCapacity` | 1000 | Maximum messages in the bounded channel. When reached, producers wait for space. |

## Testing Scenarios

The in-memory transport provides properties and methods specifically designed for testing:

```csharp
var registry = serviceProvider.GetRequiredService<TransportRegistry>();
var adapter = (InMemoryTransportAdapter)registry.GetTransportAdapter("inmemory")!;

// Inspect sent messages (keyed by message ID)
IReadOnlyDictionary<string, IDispatchMessage> messages = adapter.SentMessages;

// Clear messages between tests
adapter.ClearSentMessages();

// Check transport state
bool isRunning = adapter.IsRunning;
```

Add `using Excalibur.Dispatch.Transport;` to access `TransportRegistry` and `InMemoryTransportAdapter`.

## Health Checks

The in-memory transport implements `ITransportHealthChecker` for integration with ASP.NET Core health checks:

```csharp
services.AddHealthChecks()
    .AddTransportHealthChecks();
```

Health status is determined by:
- **Healthy**: Transport is running with acceptable failure rate
- **Degraded**: Running but failure rate exceeds 10%
- **Unhealthy**: Transport is not running

## Limitations
- No persistence — messages are lost on restart
- Single-process only — no network communication
- Not suitable for production distributed scenarios

## Next Steps
- [Kafka Transport](kafka.md) — For production high-throughput scenarios
- [RabbitMQ Transport](rabbitmq.md) — For production messaging with routing

## See Also

- [Transports Overview](index.md) - All available transport providers
- [Transport Test Doubles](../testing/transport-test-doubles.md) - InMemory test doubles for transport testing
- [Choosing a Transport](choosing-a-transport.md) - Comparison guide for transport selection
