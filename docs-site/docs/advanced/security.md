---
sidebar_position: 1
title: Security Guide
description: Security hardening guide for Excalibur applications
---

# Security Guide

This guide covers security hardening for Excalibur applications, including encryption, authentication, authorization, and audit logging.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Security
  ```
- Familiarity with [middleware](../middleware/index.md) and [encryption providers](../security/encryption-providers.md)

## Overview

Excalibur provides multiple security capabilities for building compliant, secure applications:

| Capability | Description | Standards |
|------------|-------------|-----------|
| [Encryption](#encryption) | Field-level AES-256-GCM encryption | SOC 2 CC6, GDPR Art. 32 |
| [Authorization](#authorization) | Role-based access control | SOC 2 CC5 |
| [Audit Logging](#audit-logging) | Tamper-evident hash chains | SOC 2 CC4 |
| [Key Management](#key-management) | Cloud KMS integration | SOC 2 CC6.1 |

---

## Encryption

### Field-Level Encryption

Encrypt sensitive fields using the `[PersonalData]` and `[Sensitive]` attributes:

```csharp
public class Customer
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    [PersonalData]  // GDPR personal data - encrypted at rest
    public string Email { get; set; }

    [PersonalData]
    [Sensitive]  // Also marked as confidential
    public string SocialSecurityNumber { get; set; }
}
```

### Configuration

```csharp
builder.Services.AddAzureKeyVaultKeyManagement(options =>
{
    options.VaultUri = new Uri(configuration["KeyVault:Uri"]!);
    // Uses DefaultAzureCredential by default (supports managed identity)
});
```

### Encryption Providers

| Provider | Use Case | Configuration |
|----------|----------|---------------|
| Azure Key Vault | Production, Azure | `AddAzureKeyVaultKeyManagement()` |
| AWS KMS | Production, AWS | `AddAwsKmsKeyManagement()` |
| HashiCorp Vault | Multi-cloud | `AddVaultKeyManagement()` |

### Transport Encryption

All network communication should use TLS 1.2 or higher:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(https =>
    {
        https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
    });
});
```

---

## Authorization

Excalibur uses **interface-based authorization** where messages declare their authorization requirements by implementing specific interfaces. The `AuthorizationMiddleware` automatically enforces these requirements during message processing.

### Authorization Interfaces

| Interface | Purpose | Key Properties |
|-----------|---------|----------------|
| `IRequireAuthorization` | Base interface for all authorized messages | `ActivityName` |
| `IRequireRoleAuthorization` | Role-based authorization | `RequiredRoles` |
| `IRequireActivityAuthorization` | Activity/resource-based authorization | `ResourceId`, `ResourceTypes` |
| `IRequireCustomAuthorization` | Custom authorization requirements | `AuthorizationRequirements` |

### Basic Authorization

Implement `IRequireAuthorization` to require a specific activity permission:

```csharp
using Excalibur.A3.Authorization;

public class DeleteUserCommand : IDispatchAction, IRequireAuthorization
{
    public Guid UserId { get; set; }

    // Required activity permission
    public string ActivityName => "Users.Delete";
}
```

### Role-Based Authorization

Implement `IRequireRoleAuthorization` to require specific roles:

```csharp
public class AdminOnlyCommand : IDispatchAction, IRequireRoleAuthorization
{
    public string ActivityName => "Admin.Execute";

    // Require one or more roles
    public IReadOnlyCollection<string>? RequiredRoles => new[] { "Admin", "SuperUser" };
}
```

### Activity-Based Authorization (Resource-Specific)

Implement `IRequireActivityAuthorization` for resource-level permissions:

```csharp
public class UpdateOrderCommand : IDispatchAction, IRequireActivityAuthorization
{
    public Guid OrderId { get; set; }

    public string ActivityName => "Orders.Update";

    // Resource being accessed
    public string? ResourceId => OrderId.ToString();

    // Resource types for permission lookup
    public string[] ResourceTypes => new[] { "Order" };
}

public class TransferFundsCommand : IDispatchAction, IRequireActivityAuthorization
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }

    public string ActivityName => Amount > 10000
        ? "Transfers.HighValue"
        : "Transfers.Create";

    public string? ResourceId => FromAccountId.ToString();
    public string[] ResourceTypes => new[] { "Account" };
}
```

### Custom Authorization Requirements

Implement `IRequireCustomAuthorization` for complex authorization scenarios:

```csharp
using Microsoft.AspNetCore.Authorization;

public class ComplexCommand : IDispatchAction, IRequireCustomAuthorization
{
    public string ActivityName => "Complex.Execute";

    public IEnumerable<IAuthorizationRequirement> AuthorizationRequirements => new[]
    {
        new ClaimsAuthorizationRequirement("department", new[] { "Engineering" }),
        new MinimumAgeRequirement(18)
    };
}
```

### Authorization Middleware

The `AuthorizationMiddleware` is built-in and automatically processes messages that implement `IRequireAuthorization`:

```csharp
// Registration - middleware is automatically included with A3 services
builder.Services.AddExcaliburA3Services(SupportedDatabase.SqlServer);

// Or register the middleware manually if needed
builder.Services.AddDispatch(options =>
{
    options.AddMiddleware<AuthorizationMiddleware>();
});
```

The middleware:
1. Checks if the message implements `IRequireAuthorization`
2. Builds a `ClaimsPrincipal` from the current `IAccessToken`
3. Constructs appropriate authorization requirements based on the interface type
4. Calls `IDispatchAuthorizationService.AuthorizeAsync()`
5. Returns `403 Forbidden` if authorization fails

### Authorization Result

Authorization results are stored in the message context and can be accessed via `IMessageContextAccessor`:

```csharp
// Using IActionHandler with IMessageContextAccessor for context access
public class MyCommandHandler : IActionHandler<MyCommand>
{
    private readonly IMessageContextAccessor _contextAccessor;

    public MyCommandHandler(IMessageContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public async Task HandleAsync(MyCommand command, CancellationToken ct)
    {
        // Access authorization result from context (stored as extension method)
        var context = _contextAccessor.MessageContext;
        var authResult = context?.AuthorizationResult() as IAuthorizationResult;

        // Note: Authorization middleware typically blocks unauthorized requests
        // before the handler is called. This is for additional fine-grained checks.
        if (authResult is not null && !authResult.IsAuthorized)
        {
            throw new ForbiddenException("MyCommand", "execute");
        }

        // Process authorized command...
    }
}
```

---

## Attribute-Based Authorization

For simpler authorization scenarios, Excalibur supports attribute-based authorization using the `[RequirePermission]` attribute. This provides a cleaner syntax when you don't need complex authorization logic.

### RequirePermission Attribute

Apply the attribute directly to your message class:

```csharp
using Excalibur.A3.Authorization;

[RequirePermission("users.delete")]
public class DeleteUserCommand : IDispatchAction
{
    public Guid UserId { get; set; }
}
```

### Attribute Properties

| Property | Type | Description |
|----------|------|-------------|
| `Permission` | string | The required permission name (e.g., "users.delete") |
| `ResourceTypes` | string[] | Optional resource types for resource-level authorization |
| `ResourceIdProperty` | string | Property name containing the resource ID |
| `When` | string? | Reserved for future conditional expressions (not yet implemented) |

### Resource-Level Authorization

Specify resource types and the property containing the resource ID:

```csharp
[RequirePermission("orders.update",
    ResourceTypes = new[] { "Order" },
    ResourceIdProperty = nameof(OrderId))]
public class UpdateOrderCommand : IDispatchAction
{
    public Guid OrderId { get; set; }
    public string Status { get; set; }
}
```

### Resource-Level with Multiple Resource Types

Combine resource types for cross-resource permission checks:

```csharp
[RequirePermission("transfers.create",
    ResourceTypes = new[] { "Account" },
    ResourceIdProperty = nameof(FromAccount))]
public class TransferFundsCommand : IDispatchAction
{
    public decimal Amount { get; set; }
    public Guid FromAccount { get; set; }
    public Guid ToAccount { get; set; }
}
```

### Multiple Permissions

Apply multiple attributes for AND logic (all permissions required):

```csharp
[RequirePermission("accounts.read")]
[RequirePermission("audit.view")]
public class GetAccountAuditHistoryQuery : IDispatchAction
{
    public Guid AccountId { get; set; }
}
```

### Combining Interfaces and Attributes

When a message implements `IRequireAuthorization` AND has `[RequirePermission]`, both are evaluated with AND logic:

```csharp
// Both interface AND attribute requirements must pass
[RequirePermission("admin.elevated")]
public class ElevatedAdminCommand : IDispatchAction, IRequireRoleAuthorization
{
    public string ActivityName => "Admin.Elevated";
    public IReadOnlyCollection<string>? RequiredRoles => new[] { "Admin" };
}
```

### Performance: AttributeAuthorizationCache

The framework uses `AttributeAuthorizationCache` for cached reflection lookups, avoiding repeated attribute scanning:

```csharp
// Internal caching - no configuration needed
// Attributes are cached on first access per message type
// Subsequent authorizations use cached metadata
```

---

## Interface vs Attribute: When to Use Each

| Approach | Use When | Benefits |
|----------|----------|----------|
| **Interfaces** | Complex authorization with custom requirements | Full control, type-safe, supports computed properties |
| **Attributes** | Simple permission checks | Cleaner syntax, less boilerplate, declarative |
| **Both** | Layered security requirements | AND logic ensures both pass |

### Interface Pattern (Complex Scenarios)

```csharp
// Use interfaces when authorization logic is dynamic
public class TransferFundsCommand : IDispatchAction, IRequireActivityAuthorization
{
    public decimal Amount { get; set; }

    // Dynamic activity based on amount
    public string ActivityName => Amount > 10000
        ? "Transfers.HighValue"
        : "Transfers.Standard";

    public string? ResourceId => FromAccountId.ToString();
    public string[] ResourceTypes => new[] { "Account" };
}
```

### Attribute Pattern (Simple Scenarios)

```csharp
// Use attributes when authorization is static and simple
[RequirePermission("users.delete")]
public class DeleteUserCommand : IDispatchAction
{
    public Guid UserId { get; set; }
}
```

---

## Audit Logging

### Tamper-Evident Audit Logs

The framework provides hash-chained audit logs for compliance:

```csharp
// Default in-memory audit store (for development/testing)
builder.Services.AddAuditLogging();

// Or use SQL Server for production (includes options configuration)
builder.Services.AddSqlServerAuditStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Compliance");
    options.SchemaName = "audit";
    options.EnableHashChain = true;
});
```

Hash chaining is automatically enabled via `AuditHasher`. For SOC 2 compliance, use a persistent audit store with appropriate retention policies at the database level.

### Audit Event Recording

```csharp
public class OrderService
{
    private readonly IAuditLogger _auditLogger;

    public async Task PlaceOrderAsync(Order order, CancellationToken ct)
    {
        // Business logic...

        await _auditLogger.LogAsync(new AuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = AuditEventType.DataModification,
            Action = "OrderPlaced",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = order.CustomerId.ToString(),
            ResourceType = "Order",
            ResourceId = order.Id.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["total"] = order.Total.ToString(),
                ["items"] = order.Items.Count.ToString()
            }
        }, ct);
    }
}
```

### Audit Middleware

```csharp
builder.Services.AddDispatch(options =>
{
    options.AddMiddleware<AuditLoggingMiddleware>();
});

public class AuditLoggingMiddleware : IDispatchMiddleware
{
    private readonly IAuditLogger _auditLogger;

    public AuditLoggingMiddleware(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        var outcome = AuditOutcome.Success;

        try
        {
            return await nextDelegate(message, context, cancellationToken);
        }
        catch (Exception)
        {
            outcome = AuditOutcome.Failure;
            throw;
        }
        finally
        {
            await _auditLogger.LogAsync(new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = AuditEventType.DataAccess,
                Action = message.GetType().Name,
                Outcome = outcome,
                Timestamp = startTime,
                ActorId = context.CorrelationId ?? "system",
                CorrelationId = context.CorrelationId,
            }, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
```

---

## Key Management

### Key Rotation

Configure automatic key rotation for compliance:

```csharp
builder.Services.AddKeyRotation(options =>
{
    options.Policy = KeyRotationPolicy.Default;  // 90-day rotation
    options.AutoRotateEnabled = true;
    options.WarningDaysBeforeRotation = 14;
});

// KeyRotationService is a BackgroundService registered via AddKeyRotation
```

### Key Rotation Policies

| Policy | Max Age | Use Case |
|--------|---------|----------|
| `Default` | 90 days | SOC 2 / PCI DSS compliance |
| `HighSecurity` | 30 days | Financial data, FIPS required |
| `Archival` | 365 days | Long-term storage |

### Multi-Region Keys

For disaster recovery and data sovereignty:

```csharp
builder.Services.AddMultiRegionKeyManagement(options =>
{
    options.PrimaryRegion = "us-east-1";
    options.ReplicaRegions = new[] { "eu-west-1", "ap-south-1" };
    options.SyncStrategy = KeySyncStrategy.CloudNative;
});
```

---

## Secret Management

### Azure Key Vault

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri(configuration["KeyVault:Uri"]),
    new DefaultAzureCredential());

builder.Services.AddAzureKeyVaultKeyManagement(options =>
{
    options.VaultUri = new Uri(configuration["KeyVault:Uri"]);
    options.RequirePremiumTier = true;  // HSM-backed keys
});
```

### AWS Secrets Manager

```csharp
builder.Configuration.AddSecretsManager(options =>
{
    options.SecretFilter = entry => entry.Name.StartsWith("dispatch/");
});

builder.Services.AddAwsKmsKeyManagement(options =>
{
    options.KeyId = configuration["AWS:KmsKeyId"];
    options.UseFipsEndpoint = true;
});
```

### HashiCorp Vault

```csharp
builder.Services.AddVaultKeyManagement(options =>
{
    options.Address = configuration["Vault:Address"];
    options.TransitMountPath = "transit";
    options.AuthMethod = VaultAuthMethod.AppRole;
});
```

---

## Input Validation

### FluentValidation Integration

```csharp
builder.Services.AddDispatch(options =>
{
    options.AddMiddleware<ValidationMiddleware>();
});

builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderCommandValidator>();

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required");

        RuleFor(x => x.Total)
            .GreaterThan(0)
            .WithMessage("Total must be positive");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must have at least one item");
    }
}
```

### SQL Injection Prevention

Always use parameterized queries with Dapper:

```csharp
// CORRECT - Parameterized query
var order = await connection.QuerySingleOrDefaultAsync<Order>(
    "SELECT * FROM Orders WHERE Id = @Id",
    new { Id = orderId });

// INCORRECT - SQL injection vulnerability
var order = await connection.QuerySingleOrDefaultAsync<Order>(
    $"SELECT * FROM Orders WHERE Id = '{orderId}'");  // NEVER do this!
```

---

## CORS and CSP

### CORS Configuration

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://app.example.com")
            .AllowCredentials()
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type");
    });
});
```

### Content Security Policy

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add(
        "Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self'");
    await next();
});
```

---

## Security Headers

```csharp
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    headers.Add("X-Content-Type-Options", "nosniff");
    headers.Add("X-Frame-Options", "DENY");
    headers.Add("X-XSS-Protection", "1; mode=block");
    headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    headers.Add("Permissions-Policy", "geolocation=(), microphone=()");

    await next();
});
```

---

## Security Checklist

### Development

- [ ] Use parameterized queries (Dapper)
- [ ] Validate all user input
- [ ] Never log sensitive data
- [ ] Use secrets manager for credentials

### Production

- [ ] Enable TLS 1.2+ only
- [ ] Configure CORS appropriately
- [ ] Enable security headers
- [ ] Configure key rotation
- [ ] Enable audit logging
- [ ] Set up alerting for security events

### Compliance

- [ ] Review [SOC 2 Checklist](../../docs/compliance/checklists/soc2.md)
- [ ] Review [GDPR Checklist](../../docs/compliance/checklists/gdpr.md)
- [ ] Document security controls
- [ ] Schedule penetration testing

---

## Related Documentation

- [Encryption Architecture](../security/encryption-architecture.md) - Data protection and key management
- [Audit Logging Guide](../security/audit-logging.md) - Audit implementation
- [Compliance Checklists](../compliance/index.md) - Regulatory guidance
- [Deployment Guide](deployment.md) - Secure deployment patterns

## See Also

- [Encryption Providers](../security/encryption-providers.md) — Detailed guide to AES-256-GCM, Azure Key Vault, AWS KMS, and HashiCorp Vault providers
- [Authorization](../security/authorization.md) — In-depth authorization patterns and policy configuration
- [Audit Logging](../security/audit-logging.md) — Tamper-evident hash chain implementation and compliance logging
- [Encryption Architecture](../security/encryption-architecture.md) — Data protection design and key management internals
