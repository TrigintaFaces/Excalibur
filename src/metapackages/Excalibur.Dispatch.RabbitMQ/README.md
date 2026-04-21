# Excalibur.Dispatch.RabbitMQ

Experience metapackage bundling Excalibur.Dispatch with RabbitMQ transport. Provides a single `AddDispatchRabbitMQ()` call for the common RabbitMQ messaging scenario.

## Installation

```bash
dotnet add package Excalibur.Dispatch.RabbitMQ
```

## Quick Start

```csharp
services.AddDispatchRabbitMQ(dispatch =>
{
    dispatch.UseTransport<RabbitMQTransport>(options =>
    {
        options.HostName = "localhost";
    });
});
```

## What's Included

This metapackage bundles:

- `Excalibur.Dispatch` - Core messaging framework
- `Excalibur.Dispatch.Transport.RabbitMQ` - RabbitMQ transport
- Resilience middleware (retry, circuit breaker)
- Observability middleware (metrics, tracing)

## Documentation

See the [Excalibur documentation](https://github.com/TrigintaFaces/Excalibur) for full details.
