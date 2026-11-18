# Excalibur.Hosting.AwsLambda

AWS Lambda hosting integration for the Excalibur framework

## Installation

```bash
dotnet add package Excalibur.Hosting.AwsLambda
```

## Quick Start

```csharp
services.AddExcaliburAwsLambdaServerless();
```

`AddExcaliburAwsLambdaServerless()` is a thin forwarding bootstrap over `AddAwsLambdaServerless()`: it registers the platform host provider and cold-start optimizer and the Lambda serializer fallback, with no additional Excalibur-layer services. It does not automatically compose a complete Dispatch pipeline, saga persistence, event sourcing, or an outbox. Configure messaging and handlers explicitly with `AddDispatch(...)`, and select domain/persistence services through the [Excalibur hosting builder](../Excalibur.Hosting/README.md) (`AddExcalibur(...)`) and the relevant provider packages. See the [Dispatch AwsLambda registration boundary](../../Dispatch/Excalibur.Dispatch.Hosting.AwsLambda/README.md) for the forwarded services and their prerequisites.

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.

