# Security Samples

Security and compliance patterns for enterprise applications using **Excalibur.Dispatch.Security**.

## Choosing a Security Pattern

| Pattern | Best For | Complexity | Dependencies |
|---------|----------|------------|--------------|
| **[Message Encryption](MessageEncryption/)** | PII/PCI data protection | Medium | DataProtection API |
| **[Audit Logging](AuditLogging/)** | SOC2/HIPAA/GDPR compliance | Low | None (in-memory demo) |
| **[Azure Key Vault](AzureKeyVault/)** | Azure-native apps | Medium | Azure subscription |
| **[AWS Secrets Manager](AwsSecretsManager/)** | AWS-native apps | Medium | AWS account or LocalStack |

## Samples Overview

| Sample | What It Demonstrates | Local Dev Ready |
|--------|---------------------|-----------------|
| [MessageEncryption](MessageEncryption/) | Field-level encryption, key rotation, masking | Yes - uses DataProtection |
| [AuditLogging](AuditLogging/) | Compliance logging, PII redaction, security events | Yes - in-memory |
| [AzureKeyVault](AzureKeyVault/) | ICredentialStore, caching, DefaultAzureCredential | Requires Azure |
| [AwsSecretsManager](AwsSecretsManager/) | Secret retrieval/storage, IAM auth | Yes - LocalStack |

## Quick Start

### Message Encryption (Simplest)

```bash
cd samples/06-security/MessageEncryption
dotnet run
```

No external dependencies - uses ASP.NET Core DataProtection.

### Audit Logging (Compliance)

```bash
cd samples/06-security/AuditLogging
dotnet run
```

Demonstrates PII redaction and compliance mapping.

### AWS Secrets Manager (with LocalStack)

```bash
# Start LocalStack
docker run -d -p 4566:4566 localstack/localstack

# Run sample
cd samples/06-security/AwsSecretsManager
dotnet run
```

### Azure Key Vault

```bash
# Requires Azure CLI authentication
az login

cd samples/06-security/AzureKeyVault
dotnet run
```

## Security Patterns Comparison

### Encryption Approaches

| Approach | Use Case | Key Management |
|----------|----------|----------------|
| **Field-level encryption** | Encrypt specific PII fields | DataProtection or KMS |
| **Transport encryption** | Data in transit | TLS/HTTPS |
| **At-rest encryption** | Database/storage | Provider-managed |
| **End-to-end encryption** | Sensitive workflows | Application-managed |

### Compliance Mapping

| Requirement | Sample | Implementation |
|-------------|--------|----------------|
| SOC2 CC6.1 | AuditLogging | User identity tracking |
| SOC2 CC6.6 | AuditLogging | Automatic activity logging |
| HIPAA 164.312(b) | AuditLogging | Audit controls |
| HIPAA 164.312(d) | AuditLogging | User authentication tracking |
| GDPR Art. 30 | AuditLogging | Records of processing |
| GDPR Art. 32 | MessageEncryption | Pseudonymization |
| PCI DSS 3.4 | MessageEncryption | PAN encryption |
| PCI DSS 3.5 | AzureKeyVault, AwsSecretsManager | Key management |

### Secret Management Comparison

| Feature | Azure Key Vault | AWS Secrets Manager |
|---------|-----------------|---------------------|
| **Local Dev** | Requires Azure (or emulator) | LocalStack support |
| **Authentication** | DefaultAzureCredential | Credential chain |
| **Key Rotation** | Automatic (configurable) | Lambda-based |
| **Caching** | Recommended | Recommended |
| **RBAC** | Azure RBAC | IAM policies |

## Key Concepts

### ICredentialStore Interface

```csharp
public interface ICredentialStore
{
    Task<SecureString?> GetCredentialAsync(string key, CancellationToken ct);
}

public interface IWritableCredentialStore : ICredentialStore
{
    Task StoreCredentialAsync(string key, SecureString value, CancellationToken ct);
}
```

### PII Redaction Pattern

```csharp
// Automatic redaction in audit logs
var command = new UpdateCustomerCommand(
    Email: "john@example.com",      // Logged as [EMAIL REDACTED]
    SSN: "123-45-6789");            // Logged as [SSN REDACTED]
```

### Field-Level Encryption

```csharp
// Encrypt sensitive fields before storage/transport
var encryptedEmail = await encryptionService.EncryptMessageAsync(email, ct);
var event = new CustomerCreatedEvent(
    CustomerId: id,
    Email: MaskEmail(email),           // Visible: j***@example.com
    EncryptedEmail: encryptedEmail);   // Encrypted for authorized access
```

## Best Practices

### DO

- Use field-level encryption for PII (names, SSN, payment data)
- Implement audit logging for all sensitive operations
- Use cloud KMS (Key Vault, Secrets Manager) for production keys
- Cache secrets locally with short TTL (5-15 minutes)
- Redact sensitive data in all logs
- Use managed identity in production

### DON'T

- Store secrets in code or config files
- Log credential values (even partially)
- Use long-lived secrets without rotation
- Share KMS access broadly
- Encrypt everything (performance impact)

## Prerequisites

| Sample | Requirements |
|--------|-------------|
| MessageEncryption | .NET 9.0 SDK |
| AuditLogging | .NET 9.0 SDK |
| AzureKeyVault | .NET 9.0 SDK, Azure subscription, `az login` |
| AwsSecretsManager | .NET 9.0 SDK, Docker (for LocalStack) or AWS account |

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Security` | Core security abstractions, IMessageEncryptionService |
| `Excalibur.Dispatch.Security.Azure` | Azure Key Vault ICredentialStore |
| `Excalibur.Dispatch.Security.Aws` | AWS Secrets Manager ICredentialStore |

## Related Samples

- [OpenTelemetry](../07-observability/OpenTelemetry/) - Distributed tracing
- [Health Checks](../07-observability/HealthChecks/) - Monitoring integration

## Learn More

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [SOC2 Compliance](https://www.aicpa.org/soc2)
- [HIPAA Security Rule](https://www.hhs.gov/hipaa/for-professionals/security/)
- [GDPR Requirements](https://gdpr-info.eu/)
- [PCI DSS Standards](https://www.pcisecuritystandards.org/)
- [Azure Key Vault](https://docs.microsoft.com/azure/key-vault/)
- [AWS Secrets Manager](https://docs.aws.amazon.com/secretsmanager/)

---

*Category: Security | Sprint 431*
