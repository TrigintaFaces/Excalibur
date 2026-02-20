# Excalibur.Dispatch.AuditLogging.Splunk

Splunk HTTP Event Collector (HEC) exporter for audit logging in the Dispatch messaging framework. Supports real-time and batch export modes with retry and backoff.

## Installation

```bash
dotnet add package Excalibur.Dispatch.AuditLogging.Splunk
```

## Quick Start

```csharp
// Add Excalibur.Dispatch.AuditLogging.Splunk to your service configuration
services.AddAuditLoggingSplunk();
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
