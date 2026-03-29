# Excalibur.Dispatch.Observability

Observability and telemetry for the Excalibur framework.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.RabbitMQ` | Starter | Transport + Resilience + Observability |
| `Excalibur.Dispatch.Kafka` | Starter | Transport + Resilience + Observability |
| `Excalibur.Dispatch.Azure` | Starter | Transport + Resilience + Observability |
| `Excalibur.Dispatch.Aws` | Starter | Transport + Resilience + Observability |

> **Tip:** This package is automatically included when you install any transport starter metapackage.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Observability
```

## Features

- OpenTelemetry integration
- Distributed tracing
- Metrics collection
- Structured logging
- Application Insights support

## Configuration

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddObservability(obs => obs.Enabled = true);
});

// Or register directly
services.AddDispatchObservability();
```

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
