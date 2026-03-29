# Excalibur.Dispatch.Kafka

Experience metapackage bundling Excalibur.Dispatch with Kafka transport. Provides a single `AddDispatchKafka()` call for the common Kafka streaming scenario.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Kafka
```

## Quick Start

```csharp
services.AddDispatchKafka(dispatch =>
{
    dispatch.UseTransport<KafkaTransport>(options =>
    {
        options.BootstrapServers = "localhost:9092";
    });
});
```

## What's Included

This metapackage bundles:

- `Excalibur.Dispatch` - Core messaging framework
- `Excalibur.Dispatch.Transport.Kafka` - Kafka transport
- Resilience middleware (retry, circuit breaker)
- Observability middleware (metrics, tracing)

## Documentation

See the [Excalibur documentation](https://github.com/TrigintaFaces/Excalibur) for full details.
