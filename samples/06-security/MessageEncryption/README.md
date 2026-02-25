# Message Encryption Sample

This sample demonstrates how to use `Excalibur.Dispatch.Security` for encrypting sensitive message data, including field-level encryption for PII and PCI compliance.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Quick Start

```bash
dotnet run
```

No external dependencies required - uses ASP.NET Core DataProtection for local key management.

## What This Sample Demonstrates

### Field-Level Encryption

Encrypt individual sensitive fields while keeping non-sensitive data in plaintext:

```csharp
var encryptionService = host.Services.GetRequiredService<IMessageEncryptionService>();

// Encrypt sensitive fields
var encryptedEmail = await encryptionService.EncryptMessageAsync(email, ct);
var encryptedSsn = await encryptionService.EncryptMessageAsync(ssn, ct);

// Create event with encrypted fields
var customerEvent = new CustomerCreatedEvent(
    CustomerId: customerId,
    Email: MaskEmail(email),           // Masked for display
    EncryptedEmail: encryptedEmail,    // Encrypted for storage
    EncryptedPhoneNumber: encryptedPhone,
    EncryptedSocialSecurityNumber: encryptedSsn,
    CreatedAt: DateTimeOffset.UtcNow);
```

### Payment Data Encryption (PCI Compliance)

Securely handle payment card data:

```csharp
var cardData = $"{\"number\":\"{cardNumber}\",\"exp\":\"12/26\",\"cvv\":\"123\"}";
var encryptedCardData = await encryptionService.EncryptMessageAsync(cardData, ct);

var paymentEvent = new PaymentProcessedEvent(
    PaymentId: paymentId,
    MaskedCardNumber: "************1111",  // Only last 4 visible
    EncryptedCardData: encryptedCardData,  // Full data encrypted
    ...);
```

### Decryption for Authorized Operations

```csharp
// Decrypt only when absolutely necessary
var decryptedEmail = await encryptionService.DecryptMessageAsync(
    encryptedEmail,
    cancellationToken);
```

### Key Rotation

```csharp
// Manual key rotation (DataProtection auto-rotates every 90 days)
await encryptionService.RotateKeysAsync(cancellationToken);

// Old data remains decryptable after rotation
var verifyDecrypt = await encryptionService.DecryptMessageAsync(
    oldEncryptedData,
    cancellationToken);
```

## Project Structure

```
MessageEncryption/
├── Messages/
│   ├── CustomerCreatedEvent.cs    # PII encryption example
│   └── PaymentProcessedEvent.cs   # PCI compliance example
├── Handlers/
│   ├── CustomerCreatedEventHandler.cs
│   └── PaymentProcessedEventHandler.cs
├── Program.cs                      # Encryption demos
├── appsettings.json               # Configuration
└── README.md                      # This file
```

## Configuration

The sample uses ASP.NET Core DataProtection with default settings:

```json
{
  "DataProtection": {
    "ApplicationName": "MessageEncryptionSample",
    "KeyLifetimeDays": 90
  }
}
```

### Production Configuration

For production, configure persistent key storage:

```csharp
// Azure Blob Storage
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(connectionString, containerName, blobName)
    .ProtectKeysWithAzureKeyVault(keyIdentifier, credential);

// AWS
builder.Services.AddDataProtection()
    .PersistKeysToAWSSystemsManager("/MyApp/DataProtection")
    .ProtectKeysWithAwsKms("arn:aws:kms:...");

// SQL Server
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<DataProtectionDbContext>();
```

## Encryption Patterns

### When to Use Field-Level Encryption

| Data Type | Encrypt? | Example |
|-----------|----------|---------|
| PII (names, addresses) | Yes | Customer profile data |
| Financial data | Yes | Credit card numbers, bank accounts |
| Health information | Yes | Medical records (HIPAA) |
| Authentication secrets | Yes | API keys, tokens |
| Public identifiers | No | Order IDs, product SKUs |
| Timestamps | Usually no | Created dates |

### Encryption vs Transport Security

| Scenario | Solution |
|----------|----------|
| Data in transit | TLS/HTTPS (always use) |
| Data at rest | Field-level encryption |
| End-to-end encryption | Field-level + TLS |
| Compliance (PCI, HIPAA, GDPR) | Field-level encryption |

### Masking Patterns

```csharp
// Email: c********@example.com
static string MaskEmail(string email)
{
    var atIndex = email.IndexOf('@');
    return email[0] + new string('*', atIndex - 1) + email[atIndex..];
}

// Card: ************1111
static string MaskCardNumber(string cardNumber)
{
    return new string('*', cardNumber.Length - 4) + cardNumber[^4..];
}

// SSN: ***-**-6789
static string MaskSSN(string ssn)
{
    return "***-**-" + ssn[^4..];
}
```

## Key Management Best Practices

1. **Key Rotation**: Rotate keys regularly (DataProtection defaults to 90 days)
2. **Key Storage**: Store keys in a secure location (Azure Key Vault, AWS KMS)
3. **Key Backup**: Maintain encrypted backups of key material
4. **Access Control**: Limit who can access encryption keys
5. **Audit Logging**: Log all key access and rotation events

## Compliance Considerations

### PCI DSS

- Never store CVV/CVC after authorization
- Encrypt PAN (Primary Account Number) at rest
- Use strong cryptography (AES-256)
- Implement key management procedures

### GDPR

- Encrypt personal data at rest
- Implement "right to be forgotten" (delete encryption keys)
- Document encryption measures

### HIPAA

- Encrypt PHI (Protected Health Information)
- Implement access controls
- Maintain audit logs

## Related Samples

- [AzureKeyVault](../AzureKeyVault/) - Azure Key Vault integration
- [AwsSecretsManager](../AwsSecretsManager/) - AWS Secrets Manager integration
- [AuditLogging](../AuditLogging/) - Compliance audit logging

## Learn More

- [ASP.NET Core DataProtection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/)
- [Excalibur.Dispatch.Security Package](../../../src/Dispatch/Excalibur.Dispatch.Security/)
- [PCI DSS Requirements](https://www.pcisecuritystandards.org/)
- [GDPR Encryption Guidelines](https://gdpr-info.eu/)
