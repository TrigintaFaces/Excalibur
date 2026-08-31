# Excalibur.AuditLogging.Sentinel

Azure Sentinel audit log exporter for the Dispatch compliance framework. Supports Azure Monitor Data Collector API for custom log ingestion.

## Installation

```bash
dotnet add package Excalibur.AuditLogging.Sentinel
```

## Quick Start

```csharp
services.AddAuditLogging();
services.AddSentinelAuditExporter(sentinel => sentinel
    .WorkspaceId(configuration["Sentinel:WorkspaceId"]!)
    .SharedKey(configuration["Sentinel:SharedKey"]!));
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur repository. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
