# Excalibur.AuditLogging.Splunk

Splunk HTTP Event Collector (HEC) exporter for audit logging in the Excalibur framework. Supports real-time and batch export modes with retry and backoff.

## Installation

```bash
dotnet add package Excalibur.AuditLogging.Splunk
```

## Quick Start

```csharp
services.AddAuditLogging();
services.AddSplunkAuditExporter(splunk => splunk
    .HecEndpoint(new Uri("https://splunk.example.com:8088"))
    .HecToken(configuration["Splunk:HecToken"]!));
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
