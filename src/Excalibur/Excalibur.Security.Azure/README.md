# Excalibur.Dispatch.Security.Azure

Azure security integrations for Dispatch messaging including Azure Key Vault credential management and Azure Service Bus validation.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Security.Azure
```

## Features

- Azure Key Vault integration for credential management
- Azure Service Bus message validation
- Managed identity support
- Secure credential caching
- Integration with Dispatch messaging security pipeline

## Configuration

```csharp
services.AddDispatch(options =>
{
    options.UseSecurity(security =>
    {
        security.UseAzureKeyVault(azure =>
        {
            azure.VaultUri = new Uri("https://my-vault.vault.azure.net/");
            azure.UseManagedIdentity = true;
        });
    });
});
```

## Requirements

- Azure Identity SDK
- Azure Key Vault Secrets SDK
- Azure credentials configured (Managed Identity, Service Principal, or Azure CLI)

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
