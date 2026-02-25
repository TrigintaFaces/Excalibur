# Compliance Audit Logging Sample

This sample demonstrates compliance audit logging for enterprise applications, covering SOC2, HIPAA, and GDPR requirements with `Excalibur.Dispatch.Security`.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Quick Start

```bash
dotnet run
```

No external dependencies required - uses in-memory storage for demonstration.

## What This Sample Demonstrates

### Audit Logging Middleware

All commands and queries are automatically logged with timestamps and user context:

```csharp
public sealed class AuditLoggingMiddleware : IDispatchPipelineBehavior
{
    public async Task<DispatchResult> HandleAsync(
        object message,
        IMessageContext context,
        Func<object, IMessageContext, Task<DispatchResult>> next,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        var redactedPayload = RedactSensitiveData(message);

        _logger.LogInformation(
            "[AUDIT] Starting {MessageType} at {Timestamp}",
            message.GetType().Name,
            startTime);

        var result = await next(message, context);

        _logger.LogInformation(
            "[AUDIT] Completed {MessageType}. Payload: {Payload}",
            message.GetType().Name,
            redactedPayload);

        return result;
    }
}
```

### PII Redaction

Sensitive fields are automatically redacted in audit logs:

```csharp
// Original data
var command = new UpdateCustomerCommand(
    Email: "john.doe@example.com",
    PhoneNumber: "+1-555-123-4567",
    SocialSecurityNumber: "123-45-6789");

// Audit log shows:
// {"email":"[EMAIL REDACTED]","phoneNumber":"[PHONE REDACTED]",
//  "socialSecurityNumber":"[SSN REDACTED]"}
```

### Security Event Logging

Log security events with severity levels:

```csharp
await securityEventLogger.LogSecurityEventAsync(
    SecurityEventType.AuthorizationFailure,
    "User attempted admin access without privileges",
    SecuritySeverity.High,
    context);
```

### User Identity Capture

Track user context for all operations:

```csharp
context.SetItem("User:MessageId", "user-12345");
context.SetItem("Client:IP", "192.168.1.100");
context.SetItem("Client:UserAgent", "MyApp/1.0");
```

## Project Structure

```
AuditLogging/
├── Messages/
│   └── OrderCommand.cs         # Sample commands
├── Middleware/
│   └── AuditLoggingMiddleware.cs # Audit logging pipeline
├── Program.cs                   # Demo scenarios
├── appsettings.json            # Configuration
└── README.md                   # This file
```

## Redaction Patterns

| Data Type | Pattern | Redacted Value |
|-----------|---------|----------------|
| Email | `*@*.com` | `[EMAIL REDACTED]` |
| Phone | `+1-555-123-4567` | `[PHONE REDACTED]` |
| Credit Card | 13-19 digits | `[CARD REDACTED]` |
| SSN | `123-45-6789` | `[SSN REDACTED]` |
| Named Fields | `Password`, `Secret`, etc. | `[REDACTED]` |

### Configuring Sensitive Fields

```json
{
  "AuditLogging": {
    "SensitiveFields": [
      "Password",
      "Secret",
      "Token",
      "CreditCard",
      "SocialSecurityNumber"
    ]
  }
}
```

## Compliance Mapping

### SOC2 Type II

| Control | Implementation |
|---------|---------------|
| CC6.1 | User identity tracking in audit logs |
| CC6.6 | Automatic logging of all system activities |
| CC6.7 | Security event monitoring with severity |

### HIPAA

| Safeguard | Implementation |
|-----------|---------------|
| 164.312(b) | Audit controls for all data access |
| 164.312(c) | Integrity via structured, append-only logs |
| 164.312(d) | User authentication tracking |

### GDPR

| Article | Implementation |
|---------|---------------|
| Art. 30 | Records of processing activities |
| Art. 32 | Pseudonymization via field redaction |
| Art. 33 | Security event tracking for breach notification |

## Security Event Types

| Event Type | Severity | Description |
|------------|----------|-------------|
| `AuthenticationSuccess` | Low | Successful login |
| `AuthenticationFailure` | Medium | Failed login attempt |
| `AuthorizationFailure` | High | Access denied |
| `RateLimitExceeded` | Medium | Too many requests |
| `SuspiciousActivity` | Critical | Potential attack |
| `InjectionAttempt` | Critical | SQL/XSS detected |

## Production Configuration

### SQL Server Storage

```csharp
builder.Services.AddSingleton<ISecurityEventStore, SqlSecurityEventStore>();
```

### Elasticsearch Storage

```csharp
builder.Services.AddSingleton<ISecurityEventStore, ElasticsearchSecurityEventStore>();
```

### File-Based Storage

```csharp
builder.Services.AddSingleton<ISecurityEventStore, FileSecurityEventStore>(sp =>
    new FileSecurityEventStore("/var/log/audit/", sp.GetRequiredService<ILogger<FileSecurityEventStore>>()));
```

## Best Practices

1. **Always redact PII** - Never log sensitive data in plaintext
2. **Include user context** - Track who performed each operation
3. **Use structured logging** - Enable log analysis and alerting
4. **Set appropriate severity** - Critical for security incidents
5. **Retain logs appropriately** - Follow compliance requirements (e.g., 7 years for SOC2)
6. **Secure log access** - Logs contain sensitive metadata

## Audit Log Format

```json
{
  "timestamp": "2026-01-21T12:00:00Z",
  "eventType": "AuditLogAccess",
  "severity": "Low",
  "description": "Command executed: CreateOrderCommand (Duration: 45ms)",
  "userId": "user-12345",
  "sourceIp": "192.168.1.100",
  "messageType": "CreateOrderCommand",
  "correlationId": "abc-123-def",
  "payload": "{\"orderId\":\"ORD-001\",\"customerEmail\":\"[EMAIL REDACTED]\"}"
}
```

## Related Samples

- [MessageEncryption](../MessageEncryption/) - Field-level encryption
- [AzureKeyVault](../AzureKeyVault/) - Azure Key Vault integration
- [AwsSecretsManager](../AwsSecretsManager/) - AWS Secrets Manager integration

## Learn More

- [SOC2 Compliance](https://www.aicpa.org/soc2)
- [HIPAA Security Rule](https://www.hhs.gov/hipaa/for-professionals/security/)
- [GDPR Requirements](https://gdpr-info.eu/)
- [Excalibur.Dispatch.Security Package](../../../src/Dispatch/Excalibur.Dispatch.Security/)
