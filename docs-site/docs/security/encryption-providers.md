---
sidebar_position: 4
title: Encryption Providers
description: Key management providers for AWS KMS, Azure Key Vault, and HashiCorp Vault.
---

# Encryption Providers

Dispatch encryption uses `IEncryptionProvider` as its core abstraction. Key management providers handle key storage, rotation, and envelope encryption via cloud-native or self-hosted vaults.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Security
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
    dispatch.UseSecurity(configuration);
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
dotnet add package Excalibur.Compliance.Aws
```

### Setup

```csharp
// Registration. The key is selected by ALIAS PREFIX plus purpose, not by a key id.
services.AddAwsKmsKeyManagement(aws => aws
    .Region("us-east-1")
    .KeyAliasPrefix("master-encryption-")
    .Environment("prod"));

// LocalStack for development -- point ServiceUrl at the emulator.
services.AddAwsKmsKeyManagement(aws => aws
    .Region("us-east-1")
    .ServiceUrl("http://localhost:4566")
    .KeyAliasPrefix("dev-"));

// Multi-region keys. The fluent builder covers the five connection settings above;
// key-policy and cache settings live on AwsKmsOptions and are set through
// Configure<AwsKmsOptions> or by binding a configuration section.
services.Configure<AwsKmsOptions>(options =>
{
    options.KeyPolicy.CreateMultiRegionKeys = true;
    options.KeyPolicy.ReplicaRegions = [RegionEndpoint.USWest2, RegionEndpoint.EUWest1];
});
```

---

## Azure Key Vault

Envelope encryption with Azure Key Vault.

### Installation

```bash
dotnet add package Excalibur.Compliance.Azure
```

### Setup

```csharp
// Fluent builder
services.AddAzureKeyVaultKeyManagement(azure =>
{
    azure.VaultUri(new Uri("https://my-vault.vault.azure.net/"))
         .KeyNamePrefix("dispatch-encryption-");
});

// From a configuration section
services.AddAzureKeyVaultKeyManagement(azure =>
{
    azure.BindConfiguration("AzureKeyVault");
});
```

### Additional Azure Security

```csharp
// Via the Dispatch builder (recommended)
services.AddDispatch(dispatch =>
{
    dispatch.UseSecurity(configuration);
});

// Or standalone Azure security setup — the Key Vault credential store is
// wired through the Azure security builder, and is registered only when a
// VaultUri is supplied.
services.AddDispatchSecurityAzure(azure =>
{
    azure.VaultUri("https://my-vault.vault.azure.net/");
});
```

---

## HashiCorp Vault

Envelope encryption with HashiCorp Vault Transit secrets engine.

### Installation

```bash
dotnet add package Excalibur.Compliance.Vault
```

### Setup

```csharp
// Core connection settings via the fluent builder
services.AddVaultKeyManagement(vault =>
    vault.VaultUri(new Uri("https://vault.example.com:8200"))
         .TransitMountPath("transit")
         .KeyNamePrefix("dispatch-"));   // Keys named: dispatch-{keyId}

// Authentication (and other grouped sub-options) via Configure<VaultOptions>
services.Configure<VaultOptions>(options =>
{
    options.Auth.AuthMethod = VaultAuthMethod.Token;
    options.Auth.Token = "s.your-vault-token";
});

// Or bind the whole VaultOptions from an appsettings "Vault" section
services.AddVaultKeyManagement(vault => vault.BindConfiguration("Vault"));
```

---

## Provider Comparison

| Feature | AWS KMS | Azure Key Vault | HashiCorp Vault |
|---------|---------|-----------------|-----------------|
| Multi-region | `AwsKmsOptions.KeyPolicy.CreateMultiRegionKeys` + `ReplicaRegions` | Via Azure replication | Via Vault replication |
| Local development | `ServiceUrl("http://localhost:4566")` (LocalStack) | N/A | Dev mode |
| Configuration binding | Action callback | Action, options, config section | Action, options, config section |
| Custom client | Factory overload | Via Azure Identity | Token-based |

## See Also

- [Encryption Architecture](./encryption-architecture.md) — Core encryption design and field-level encryption
- [Compliance](../compliance/index.md) — FedRAMP, HIPAA, SOC2 checklists
- [Data Providers](../data-providers/index.md) — Database-level encryption at rest
