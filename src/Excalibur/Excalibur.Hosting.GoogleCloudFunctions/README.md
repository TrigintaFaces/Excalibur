# Excalibur.Hosting.GoogleCloudFunctions

Google Cloud Functions hosting integration for the Excalibur framework

## Installation

```bash
dotnet add package Excalibur.Hosting.GoogleCloudFunctions
```

## Quick Start

```csharp
services.AddExcaliburGoogleCloudFunctionsServerless();
```

`AddExcaliburGoogleCloudFunctionsServerless()` is a thin forwarding bootstrap over `AddGoogleCloudFunctionsServerless()`: it registers the platform host provider and cold-start optimizer, with no additional Excalibur-layer services. It does not automatically compose a complete Dispatch pipeline, saga persistence, event sourcing, or an outbox. Configure messaging and handlers explicitly with `AddDispatch(...)`, and select domain/persistence services through the [Excalibur hosting builder](../Excalibur.Hosting/README.md) (`AddExcalibur(...)`) and the relevant provider packages. See the [Dispatch GoogleCloudFunctions registration boundary](../../Dispatch/Excalibur.Dispatch.Hosting.GoogleCloudFunctions/README.md) for the forwarded services and their prerequisites.

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
