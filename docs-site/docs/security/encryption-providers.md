---
sidebar_position: 4
title: Encryption Providers
description: Key management providers for AWS KMS, Azure Key Vault, and HashiCorp Vault.
---

# Encryption Providers

Dispatch encryption uses `IEncryptionProvider` as its core abstraction. Key management providers handle key storage, rotation, and envelope encryption via cloud-native or self-hosted vaults.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Security
  ```
- Access to a key management provider (AWS KMS, Azure Key Vault, or HashiCorp Vault)
- Familiarity with [security concepts](./index.md) and [Dispatch pipeline](../pipeline/index.md)

## Core Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

// Register via the Dispatch builder (recommended)
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.AddSecurity(configuration);
});

// Or standalone encryption registration
services.AddEncryption(builder =>
{
    // Configure encryption provider, key policies, etc.
});

// Development-only encryption (insecure, for local dev)
services.AddDevEncryption();
```

---

## AWS KMS

Envelope encryption with AWS Key Management Service.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Compliance.Aws
```

### Setup

```csharp
// Basic registration
services.AddAwsKmsKeyManagement(options =>
{
    options.KeyId = "arn:aws:kms:us-east-1:123456789:key/your-key-id";
});

// With custom client factory
services.AddAwsKmsKeyManagement(
    sp => new AmazonKeyManagementServiceClient(RegionEndpoint.USEast1),
    options =>
    {
        options.KeyId = "alias/my-key";
    });

// LocalStack for development
services.AddAwsKmsKeyManagementLocalStack(
    localStackEndpoint: "http://localhost:4566",
    options =>
    {
        options.KeyId = "alias/dev-key";
    });

// Multi-region for high availability
services.AddAwsKmsKeyManagementMultiRegion(
    primaryRegion: RegionEndpoint.USEast1,
    replicaRegions: new[] { RegionEndpoint.USWest2, RegionEndpoint.EUWest1 },
    options =>
    {
        options.KeyId = "mrk-key-id";
    });
```

---

## Azure Key Vault

Envelope encryption with Azure Key Vault.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Compliance.Azure
```

### Setup

```csharp
// With options callback
services.AddAzureKeyVaultKeyManagement(options =>
{
    options.VaultUri = "https://my-vault.vault.azure.net/";
    options.KeyName = "dispatch-encryption-key";
});

// With pre-built options
var kvOptions = new AzureKeyVaultOptions
{
    VaultUri = "https://my-vault.vault.azure.net/",
    KeyName = "dispatch-encryption-key"
};
services.AddAzureKeyVaultKeyManagement(kvOptions);

// From configuration section
services.AddAzureKeyVaultKeyManagement(
    configuration.GetSection("AzureKeyVault"));
```

### Additional Azure Security

```csharp
// Via the Dispatch builder (recommended)
services.AddDispatch(dispatch =>
{
    dispatch.AddSecurity(configuration);
});

// Or standalone Azure security setup
services.AddAzureKeyVaultCredentialStore(configuration);
services.AddAzureServiceBusSecurityValidation();
services.AddDispatchSecurityAzure(configuration);
```

---

## HashiCorp Vault

Envelope encryption with HashiCorp Vault Transit secrets engine.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Compliance.Vault
```

### Setup

```csharp
// With options callback
services.AddVaultKeyManagement(options =>
{
    options.Address = "https://vault.example.com:8200";
    options.Token = "s.your-vault-token";
    options.TransitMountPath = "transit";
    options.KeyNamePrefix = "dispatch-";  // Keys named: dispatch-{keyId}
});

// With pre-built options
var vaultOptions = new VaultOptions
{
    Address = "https://vault.example.com:8200",
    Token = "s.your-vault-token"
};
services.AddVaultKeyManagement(vaultOptions);

// From configuration section
services.AddVaultKeyManagement(
    configuration.GetSection("Vault"));
```

---

## Provider Comparison

| Feature | AWS KMS | Azure Key Vault | HashiCorp Vault |
|---------|---------|-----------------|-----------------|
| Multi-region | `AddAwsKmsKeyManagementMultiRegion` | Via Azure replication | Via Vault replication |
| Local development | `AddAwsKmsKeyManagementLocalStack` | N/A | Dev mode |
| Configuration binding | Action callback | Action, options, config section | Action, options, config section |
| Custom client | Factory overload | Via Azure Identity | Token-based |

## See Also

- [Encryption Architecture](./encryption-architecture.md) — Core encryption design and field-level encryption
- [Compliance](../compliance/index.md) — FedRAMP, HIPAA, SOC2 checklists
- [Data Providers](../data-providers/index.md) — Database-level encryption at rest
