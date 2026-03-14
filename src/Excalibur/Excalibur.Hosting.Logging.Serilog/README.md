# Excalibur.Hosting.Logging.Serilog

Serilog logging integration for Excalibur hosting applications.

## Features

- Structured logging with Serilog
- OpenTelemetry log export
- Console, Debug, and File sinks included

## Usage

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureExcaliburLogging();
```
