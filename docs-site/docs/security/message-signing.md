---
sidebar_position: 3
title: Message Signing
description: Ensure message integrity and authenticity with HMAC, ECDSA, and RSA signing algorithms.
---

# Message Signing

Excalibur.Dispatch provides message signing to ensure messages haven't been tampered with during transmission. The signing infrastructure supports both symmetric (HMAC) and asymmetric (ECDSA, RSA) algorithms.

**Package:** `Excalibur.Security`

## Architecture

The signing system follows a composite pattern (similar to ASP.NET Core `CompositeFileProvider`):

```
IMessageSigningService
├── HmacMessageSigningService      (default, symmetric)
└── CompositeMessageSigningService  (multi-algorithm)
    ├── HmacSignatureAlgorithmProvider    (HMAC-SHA256/512)
    ├── EcdsaSignatureAlgorithmProvider   (ECDSA with SHA-256, P-256 or stronger)
    └── RsaSignatureAlgorithmProvider     (RSA PKCS#1 / PSS, SHA-256)
```

- **`AddMessageSigning()`** — Registers `HmacMessageSigningService` for HMAC-only scenarios
- **`AddAsymmetricSigning()`** — Registers `CompositeMessageSigningService` with all algorithm providers for non-repudiation scenarios

Both methods register `MessageSigningMiddleware` in the Dispatch pipeline automatically.

## Supported Algorithms

| Algorithm | Enum Value | Type | Use Case |
|-----------|------------|------|----------|
| HMAC-SHA256 | `SigningAlgorithm.HMACSHA256` | Symmetric | Internal service-to-service (default) |
| HMAC-SHA512 | `SigningAlgorithm.HMACSHA512` | Symmetric | Higher security symmetric |
| ECDSA with SHA-256 | `SigningAlgorithm.ECDSASHA256` | Asymmetric | Non-repudiation, cross-boundary. The curve comes from the key you supply; P-256 is the enforced minimum and anything weaker is rejected at sign and verify |
| RSA-SHA256 | `SigningAlgorithm.RSASHA256` | Asymmetric | Legacy interoperability (RSASSA-PKCS1-v1_5) |
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

### Asymmetric Signing (ECDSA / RSA)

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

`AddAsymmetricSigning()` registers all algorithm providers (HMAC, ECDSA, RSA) via `CompositeMessageSigningService`, so you can use any supported algorithm at runtime.

### Per-Tenant Algorithms

Override the signing algorithm for specific tenants:

```csharp
builder.Services.AddAsymmetricSigning(opt =>
{
    opt.DefaultAlgorithm = SigningAlgorithm.HMACSHA256;
    opt.TenantAlgorithms["tenant-financial"] = SigningAlgorithm.ECDSASHA256;
    opt.TenantAlgorithms["tenant-healthcare"] = SigningAlgorithm.RSAPSSSHA256;
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

:::caution Every component is off unless you turn it on, and the key is always `Enabled`
On this path each component defaults to **off**, so every line below is a deliberate non-default. The
key is `Enabled` — not `EnableSigning`, `EnableEncryption` or `EnableAuthentication`. This overload
reads `Security:<Component>:Enabled`, and a section that sets one of the longer names instead leaves
that component uncomposed with no error. Turning signing on also obliges you to register an
`IKeyProvider`; see [Key Provider](#key-provider) below.
:::

```json
{
  "Security": {
    "Signing": {
      "Enabled": true,
      "SigningAlgorithm": "ECDSASHA256"
    },
    "Encryption": {
      "Enabled": true
    },
    "Authentication": {
      "Enabled": true
    }
  }
}
```

## Key Provider

Signing is **opt-in**, as every security component is: `Signing.EnableSigning` defaults to `false`, and
so do the encryption, rate limiting and authentication flags. Signing then asks more of you than the
others do. It needs an `IKeyProvider` supplying key material shared by every process that signs or
verifies, and which key that is can only be a deployment decision.

**The framework registers no key provider and never mints signing keys.** A key nobody chose produces
signatures no other instance can verify — and because `RequireValidSignature` defaults to `true`, every
such message is rejected by the receiving instance as an authorization failure. Enabling signing without
registering an `IKeyProvider` therefore fails at host startup, naming the missing provider, rather than
starting a host that cannot verify its own traffic.

So enabling signing means two steps, together: turn it on, **and** register a durable provider. Which
switch turns it on depends on how you compose security, and the three are easy to confuse:

| How you compose | The switch that turns signing on | Default |
|---|---|---|
| `UseSecurity(configuration)` / `AddDispatchSecurityMiddleware(IConfiguration)` | the `Security:Signing:Enabled` configuration key | off |
| `AddDispatchSecurityMiddleware(Action<SecurityOptions>)` | `options.Signing.EnableSigning` | off |
| `AddMessageSigning(...)` / `AddAsymmetricSigning(...)` called directly | `SigningOptions.Enabled` | on — calling the method *is* the opt-in |

The first two compose signing only when their switch is on. The third is already an explicit request for
signing, so its own `Enabled` flag defaults to `true` and is there to let you disable it again without
removing the call. All three still require an `IKeyProvider`.

Cloud-specific packages provide Secrets-backed implementations that fail closed (a resolution error never
yields a null/empty key) and cache resolved key material for a bounded TTL (`CacheTtlSeconds`, default
300s; disable with `EnableCache = false`):

- **`Excalibur.Security.Azure`** — Azure Key Vault (`AddAzureKeyVaultKeyProvider`)
- **`Excalibur.Security.Aws`** — AWS Secrets Manager (`AddAwsSecretsManagerKeyProvider`)

```csharp
// Azure Key Vault
builder.Services.AddAzureKeyVaultKeyProvider(o =>
{
    o.VaultUri = builder.Configuration["Signing:VaultUri"];
    o.SecretNamePrefix = "dispatch-signing-";   // optional
    o.CacheTtlSeconds = 300;                     // bounded cache (default)
});

// AWS Secrets Manager
builder.Services.AddAwsSecretsManagerKeyProvider(o =>
{
    o.Region = "us-east-1";
    o.SecretNamePrefix = "dispatch-signing-";   // optional
});
```

Both registrations validate their options at startup (`ValidateOnStart`) and register their `IKeyProvider`
with `AddSingleton`, so an explicit selection wins whichever order the calls are made in. There is no
fallback provider for it to win against — the framework registers none. To supply your own instead,
register it after the cloud call, or on its own with no cloud call at all:

```csharp
builder.Services.AddSingleton<IKeyProvider, MyLocalKeyProvider>();
```

### Asymmetric Key Resolution

For asymmetric algorithms (ECDSA, RSA), the `CompositeMessageSigningService` automatically appends `:pub` to the key ID when resolving keys for verification. Store your keys using this convention:

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
