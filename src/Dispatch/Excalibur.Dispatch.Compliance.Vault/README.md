# Excalibur.Dispatch.Compliance.Vault

HashiCorp Vault integration for the Dispatch compliance framework. Provides IKeyManagementProvider implementation using Transit secrets engine with auto-unseal, AppRole/Kubernetes auth, and cross-datacenter replication.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Compliance.Vault
```

## Quick Start

```csharp
// Add Excalibur.Dispatch.Compliance.Vault to your service configuration
services.AddComplianceVault();
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
