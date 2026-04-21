# Excalibur.Dispatch.Azure

Experience metapackage bundling Excalibur.Dispatch with Azure Service Bus transport. Provides a single `AddDispatchAzure()` call for the common Azure messaging scenario.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Azure
```

## Quick Start

```csharp
services.AddDispatchAzure(dispatch =>
{
    dispatch.UseTransport<AzureServiceBusTransport>(options =>
    {
        options.ConnectionString = "your-connection-string";
    });
});
```

## What's Included

This metapackage bundles:

- `Excalibur.Dispatch` - Core messaging framework
- `Excalibur.Dispatch.Transport.AzureServiceBus` - Azure Service Bus transport
- Resilience middleware (retry, circuit breaker)
- Observability middleware (metrics, tracing)

## Documentation

See the [Excalibur documentation](https://github.com/TrigintaFaces/Excalibur) for full details.
