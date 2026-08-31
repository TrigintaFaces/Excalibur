# Excalibur.Compliance.Vault

HashiCorp Vault integration for the Dispatch compliance framework. Provides IKeyManagementProvider implementation using Transit secrets engine with auto-unseal, AppRole/Kubernetes auth, and cross-datacenter replication.

## Installation

```bash
dotnet add package Excalibur.Compliance.Vault
```

## Quick Start

```csharp
services.AddVaultKeyManagement(vault => vault
    .VaultUri(new Uri("https://vault.example.com:8200"))
    .TransitMountPath("transit"));
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
