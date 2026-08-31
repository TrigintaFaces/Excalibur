# Excalibur.Compliance.Azure

Azure Key Vault integration for the Dispatch compliance framework. Provides IKeyManagementProvider implementation with HSM support, key rotation, caching, and multi-region DR capabilities.

## Installation

```bash
dotnet add package Excalibur.Compliance.Azure
```

## Quick Start

```csharp
services.AddAzureKeyVaultKeyManagement(azure => azure
    .VaultUri(new Uri("https://my-vault.vault.azure.net/")));
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
