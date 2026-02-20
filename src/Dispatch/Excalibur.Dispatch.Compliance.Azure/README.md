# Excalibur.Dispatch.Compliance.Azure

Azure Key Vault integration for the Dispatch compliance framework. Provides IKeyManagementProvider implementation with HSM support, key rotation, caching, and multi-region DR capabilities.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Compliance.Azure
```

## Quick Start

```csharp
// Add Excalibur.Dispatch.Compliance.Azure to your service configuration
services.AddComplianceAzure();
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
