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

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
