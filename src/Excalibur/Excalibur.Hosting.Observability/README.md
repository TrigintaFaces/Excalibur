# Excalibur.Hosting.Observability

OpenTelemetry observability integration for Excalibur hosting applications.

## Features

- Metrics configuration with Prometheus exporter
- Distributed tracing with ASP.NET Core instrumentation
- Console exporters for development

## Usage

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureExcaliburMetrics();
builder.ConfigureExcaliburTracing();
```

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
