# Excalibur.Dispatch.Observability

Observability and telemetry for the Dispatch messaging framework.

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
