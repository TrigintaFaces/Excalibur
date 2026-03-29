---
sidebar_position: 3
title: Message Signing
description: Ensure message integrity and authenticity with HMAC, ECDSA, Ed25519, and RSA signing algorithms.
---

# Message Signing

Excalibur.Dispatch provides message signing to ensure messages haven't been tampered with during transmission. The signing infrastructure supports both symmetric (HMAC) and asymmetric (ECDSA, Ed25519, RSA) algorithms.

**Package:** `Excalibur.Dispatch.Security`

## Architecture

The signing system follows a composite pattern (similar to ASP.NET Core `CompositeFileProvider`):

```
IMessageSigningService
├── HmacMessageSigningService      (default, symmetric)
└── CompositeMessageSigningService  (multi-algorithm)
    ├── HmacSignatureAlgorithmProvider    (HMAC-SHA256/512)
    ├── EcdsaSignatureAlgorithmProvider   (ECDSA P-256)
    └── Ed25519SignatureAlgorithmProvider (Ed25519)
```

- **`AddMessageSigning()`** — Registers `HmacMessageSigningService` for HMAC-only scenarios
- **`AddAsymmetricSigning()`** — Registers `CompositeMessageSigningService` with all algorithm providers for non-repudiation scenarios

Both methods register `MessageSigningMiddleware` in the Dispatch pipeline automatically.

## Supported Algorithms

| Algorithm | Enum Value | Type | Use Case |
|-----------|------------|------|----------|
| HMAC-SHA256 | `SigningAlgorithm.HMACSHA256` | Symmetric | Internal service-to-service (default) |
| HMAC-SHA512 | `SigningAlgorithm.HMACSHA512` | Symmetric | Higher security symmetric |
| ECDSA P-256 | `SigningAlgorithm.ECDSASHA256` | Asymmetric | Non-repudiation, cross-boundary |
| Ed25519 | `SigningAlgorithm.Ed25519` | Asymmetric | High-performance asymmetric |
| RSA-SHA256 | `SigningAlgorithm.RSASHA256` | Asymmetric | Legacy interoperability |
| RSA-PSS-SHA256 | `SigningAlgorithm.RSAPSSSHA256` | Asymmetric | Modern RSA with PSS padding |

## Setup

### HMAC Signing (Symmetric)

For service-to-service signing where all parties share a secret key:

```csharp
builder.Services.AddMessageSigning(opt =>
{
    opt.Enabled = true;
    opt.DefaultAlgorithm = SigningAlgorithm.HMACSHA256;
    opt.DefaultKeyId = "service-signing-key";
    opt.MaxSignatureAgeMinutes = 5;
});
```

### Asymmetric Signing (ECDSA / Ed25519)

For non-repudiation scenarios where the signer and verifier use different keys:

```csharp
builder.Services.AddAsymmetricSigning(opt =>
{
    opt.Enabled = true;
    opt.DefaultAlgorithm = SigningAlgorithm.ECDSASHA256;
    opt.DefaultKeyId = "ecdsa-signing-key";
    opt.MaxSignatureAgeMinutes = 5;
    opt.IncludeTimestampByDefault = true;
    opt.KeyRotationIntervalDays = 30;
});
```

`AddAsymmetricSigning()` registers all algorithm providers (HMAC, ECDSA, Ed25519) via `CompositeMessageSigningService`, so you can use any algorithm at runtime.

### Per-Tenant Algorithms

Override the signing algorithm for specific tenants:

```csharp
builder.Services.AddAsymmetricSigning(opt =>
{
    opt.DefaultAlgorithm = SigningAlgorithm.HMACSHA256;
    opt.TenantAlgorithms["tenant-financial"] = SigningAlgorithm.ECDSASHA256;
    opt.TenantAlgorithms["tenant-healthcare"] = SigningAlgorithm.Ed25519;
});
```

### Full Security Registration

Use `UseSecurity()` on the dispatch builder to register signing alongside encryption, rate limiting, and authentication:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseSecurity(builder.Configuration);
});
```

Security options are configured via `IConfiguration` (e.g., `appsettings.json`):

```json
{
  "Security": {
    "Signing": {
      "EnableSigning": true,
      "SigningAlgorithm": "ECDSASHA256"
    },
    "Encryption": {
      "EnableEncryption": true
    },
    "Authentication": {
      "EnableAuthentication": true
    }
  }
}
```

## Key Provider

Signing requires an `IKeyProvider` to supply key material. Cloud-specific packages provide implementations:

- **`Excalibur.Dispatch.Security.Azure`** — Azure Key Vault
- **`Excalibur.Dispatch.Security.Aws`** — AWS KMS

For local development, register a custom `IKeyProvider`:

```csharp
builder.Services.AddSingleton<IKeyProvider, MyLocalKeyProvider>();
```

### Asymmetric Key Resolution

For asymmetric algorithms (ECDSA, Ed25519, RSA), the `CompositeMessageSigningService` automatically appends `:pub` to the key ID when resolving keys for verification. Store your keys using this convention:

| Operation | Key ID resolved |
|-----------|----------------|
| Signing | `signing:{tenantId}:{keyId}` |
| Verification | `signing:{tenantId}:{keyId}:pub` |

## SigningOptions Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enable/disable signing |
| `DefaultAlgorithm` | `SigningAlgorithm` | `HMACSHA256` | Default algorithm |
| `DefaultKeyId` | `string?` | `null` | Default key identifier |
| `MaxSignatureAgeMinutes` | `int` | `5` | Replay protection window |
| `IncludeTimestampByDefault` | `bool` | `true` | Embed timestamp in signed data |
| `KeyRotationIntervalDays` | `int` | `30` | Key rotation interval |
| `TenantAlgorithms` | `Dictionary<string, SigningAlgorithm>` | empty | Per-tenant algorithm overrides |

## Pipeline Integration

`MessageSigningMiddleware` runs at the `Validation` stage of the Dispatch pipeline. It:

1. **Outbound messages** — Signs message content using the configured algorithm and key
2. **Inbound messages** — Verifies the signature and rejects tampered messages

The middleware is registered automatically by both `AddMessageSigning()` and `AddAsymmetricSigning()`.

## See Also

- [Encryption Architecture](./encryption-architecture.md) — Message encryption for confidentiality
- [Encryption Providers](./encryption-providers.md) — Cloud-specific encryption providers
- [Security Overview](./index.md) — Security infrastructure overview
