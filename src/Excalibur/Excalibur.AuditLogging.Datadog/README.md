# Excalibur.AuditLogging.Datadog

Datadog audit log exporter for the Dispatch compliance framework. Supports the Datadog Logs API for custom log ingestion.

## Installation

```bash
dotnet add package Excalibur.AuditLogging.Datadog
```

## Quick Start

```csharp
services.AddAuditLogging();
services.AddDatadogAuditExporter(datadog => datadog
    .ApiKey(configuration["Datadog:ApiKey"]!)
    .Site("datadoghq.com"));
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur repository. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
