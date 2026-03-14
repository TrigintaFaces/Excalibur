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
