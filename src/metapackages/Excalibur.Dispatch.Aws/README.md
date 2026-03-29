# Excalibur.Dispatch.Aws

Experience metapackage bundling Excalibur.Dispatch with AWS SQS transport. Provides a single `AddDispatchAws()` call for the common AWS messaging scenario.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Aws
```

## Quick Start

```csharp
services.AddDispatchAws(dispatch =>
{
    dispatch.UseTransport<AwsSqsTransport>(options =>
    {
        options.Region = "us-east-1";
    });
});
```

## What's Included

This metapackage bundles:

- `Excalibur.Dispatch` - Core messaging framework
- `Excalibur.Dispatch.Transport.AwsSqs` - AWS SQS transport
- Resilience middleware (retry, circuit breaker)
- Observability middleware (metrics, tracing)

## Documentation

See the [Excalibur.Dispatch documentation](https://github.com/TrigintaFaces/Excalibur.Dispatch) for full details.
