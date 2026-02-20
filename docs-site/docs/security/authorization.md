---
sidebar_position: 2
title: Authorization & Audit (A3)
description: Activity-based authorization, grant management, token validation, and audit logging with Excalibur.A3.
---

# Authorization & Audit (A3)

Excalibur.A3 provides a unified **Authentication, Authorization, and Audit** (A3) system that integrates with the Dispatch pipeline. It supports activity-based authorization, fine-grained grants, token validation, and structured audit events.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.A3
  dotnet add package Excalibur.A3.Abstractions
  ```
- Familiarity with [Dispatch pipeline](../pipeline/index.md) and [security concepts](./index.md)

## Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.A3` | Authorization service, policies, grants, audit publisher |
| `Excalibur.A3.Abstractions` | Provider-neutral interfaces: `ITokenValidator`, `IAuditSink`, `IAuditEvent`, `IAuthorizationEvaluator`, `Grant` |

## Setup

Register A3 services via `IDispatchBuilder` or `IServiceCollection`:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Via IDispatchBuilder
builder.AddDispatchAuthorization();

// Or directly on IServiceCollection
services.AddDispatchAuthorization();
```

:::tip Single-Tenant Applications
You do **not** need to configure a tenant to use A3. When you call `AddExcaliburA3Services()`, it automatically registers `ITenantId` with the default value `"Default"` (via `TenantDefaults.DefaultTenantId`). All tenant-scoped features — grants, authorization policies, audit logging — work transparently.

For **multi-tenant** applications that serve multiple tenants from a single instance, use the factory overload:
```csharp
// Resolve tenant per-request — A3 won't override it (TryAdd semantics)
services.TryAddTenantId(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    return httpContext?.Request.Headers["X-Tenant-ID"].FirstOrDefault()
        ?? TenantDefaults.DefaultTenantId;
});
services.AddExcaliburA3Services(SupportedDatabase.Postgres);
```
:::

## Authentication

### Token Validation

Implement `ITokenValidator` to validate tokens from any provider (JWT, opaque, API keys):

```csharp
using Excalibur.A3.Authentication;

public class JwtTokenValidator : ITokenValidator
{
    public async Task<AuthenticationResult> ValidateAsync(
        string token,
        CancellationToken cancellationToken)
    {
        // Validate JWT and extract claims
        var principal = new AuthenticatedPrincipal(
            SubjectId: "user-123",
            TenantId: "tenant-abc",
            Claims: new Dictionary<string, string>
            {
                ["role"] = "admin",
                ["email"] = "user@example.com"
            });

        return new AuthenticationResult(Succeeded: true, Principal: principal);
    }
}
```

### Access Token

`IAccessToken` unifies authentication and authorization into a single object that combines `IAuthenticationToken` and `IAuthorizationPolicy`:

```csharp
using Excalibur.A3;

// IAccessToken provides both identity and authorization checks
IAccessToken token = ...;

// Authentication
string userId = token.UserId;
string tenantId = token.TenantId;

// Authorization
bool canCreate = token.IsAuthorized("Orders.Create");
bool hasGrant = token.HasGrant<CreateOrderActivity>();
```

## Authorization

### Activity-Based Authorization

Actions that require authorization implement `IRequireAuthorization`:

```csharp
using Excalibur.A3.Authorization;

public class CreateOrderAction : IRequireAuthorization
{
    public string ActivityName => "Orders.Create";
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}
```

For actions that carry an access token, implement `IAmAuthorizable`:

```csharp
using Excalibur.A3.Authorization.Requests;

public class DeleteOrderAction : IAmAuthorizable
{
    public string ActivityName => "Orders.Delete";
    public IAccessToken? AccessToken { get; set; }
    public Guid OrderId { get; set; }
}
```

### Authorization Policies

`IAuthorizationPolicy` provides tenant-scoped, activity-based authorization checks:

```csharp
using Excalibur.A3.Authorization;

// Check authorization against a policy
IAuthorizationPolicy policy = ...;

// Is the user authorized for this activity?
bool authorized = policy.IsAuthorized("Orders.Create");

// Does the user have a specific grant?
bool hasGrant = policy.HasGrant("Orders.Create");

// Type-safe grant check
bool hasTypedGrant = policy.HasGrant<CreateOrderActivity>();

// Resource-scoped grant check
bool hasResourceGrant = policy.HasGrant("Order", orderId.ToString());
```

### Authorization Service

`IDispatchAuthorizationService` evaluates authorization using ASP.NET Core `IAuthorizationRequirement` and named policies:

```csharp
using Excalibur.A3.Authorization;

public class OrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IDispatchAuthorizationService _authService;

    public OrderHandler(IDispatchAuthorizationService authService)
    {
        _authService = authService;
    }

    public async Task HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        // Check against requirements
        var result = await _authService.AuthorizeAsync(
            user, resource: null, new OrderCreationRequirement());

        // Or check against a named policy
        var policyResult = await _authService.AuthorizeAsync(
            user, resource: null, "OrderCreationPolicy");

        if (!result.IsAuthorized)
        {
            throw new UnauthorizedAccessException();
        }
    }
}
```

### Authorization Evaluator

For provider-neutral evaluation, implement `IAuthorizationEvaluator`:

```csharp
using Excalibur.A3.Authorization;

public class CustomEvaluator : IAuthorizationEvaluator
{
    public async Task<AuthorizationDecision> EvaluateAsync(
        AuthorizationSubject subject,
        AuthorizationAction action,
        AuthorizationResource resource,
        CancellationToken cancellationToken)
    {
        // Evaluate subject + action + resource triple
        return new AuthorizationDecision(AuthorizationEffect.Allow);
    }
}
```

## Grants

Grants are fine-grained permissions assigned to users, scoped by tenant and resource.

### Grant Model

```csharp
using Excalibur.A3.Authorization.Grants;

// Grant is an event-sourced aggregate
var grant = new Grant(
    userId: "user-123",
    fullName: "John Doe",
    tenantId: "tenant-abc",
    grantType: "activity-group",
    qualifier: "OrderManagement",
    expiresOn: DateTimeOffset.UtcNow.AddDays(90),
    grantedBy: "admin-456",
    grantedOn: DateTimeOffset.UtcNow);
```

### Managing Grants

Use dispatch commands to add and revoke grants:

```csharp
var correlationId = Guid.NewGuid();

// Add a grant
await dispatcher.DispatchAsync(new AddGrantCommand(
    userId: "user-123",
    fullName: "John Doe",
    grantType: "activity-group",
    qualifier: "OrderManagement",
    expiresOn: DateTimeOffset.UtcNow.AddDays(90),
    correlationId: correlationId,
    tenantId: "tenant-abc"), cancellationToken);

// Revoke a specific grant
await dispatcher.DispatchAsync(new RevokeGrantCommand(
    userId: "user-123",
    grantType: "activity-group",
    qualifier: "OrderManagement",
    correlationId: correlationId,
    tenantId: "tenant-abc"), cancellationToken);

// Revoke all grants for a user
await dispatcher.DispatchAsync(new RevokeAllGrantsCommand(
    userId: "user-123",
    fullName: "John Doe",
    correlationId: correlationId,
    tenantId: "tenant-abc"), cancellationToken);
```

### Grant Events

Grant changes emit domain events for audit trails:

- `GrantAdded` / `IGrantAdded` - Emitted when a grant is created
- `GrantRevoked` / `IGrantRevoked` - Emitted when a grant is revoked

## Audit

### Audit Events

`IAuditEvent` captures structured audit data:

```csharp
using Excalibur.A3.Abstractions.Auditing;

var auditEvent = new AuditEvent(
    timestampUtc: DateTimeOffset.UtcNow,
    tenantId: "tenant-abc",
    actorId: "user-123",
    action: "CreateOrder",
    resource: "Order/order-456",
    outcome: "Success",
    correlationId: correlationId,
    attributes: new Dictionary<string, string>
    {
        ["amount"] = "99.99",
        ["currency"] = "USD"
    });
```

### Audit Sink

Implement `IAuditSink` to persist audit events to your chosen store:

```csharp
using Excalibur.A3.Abstractions.Auditing;

public class SqlAuditSink : IAuditSink
{
    public async ValueTask WriteAsync(
        IAuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        // Persist to database, send to log aggregator, etc.
    }
}
```

### Audit Message Publisher

`IAuditMessagePublisher` publishes audit messages to external systems:

```csharp
using Excalibur.A3.Audit;

public class KafkaAuditPublisher : IAuditMessagePublisher
{
    public async Task PublishAsync<TMessage>(
        TMessage message,
        IActivityContext context,
        CancellationToken cancellationToken)
    {
        // Publish audit event to Kafka, Azure Event Hub, etc.
    }
}
```

## What's Next

- [Encryption Architecture](encryption-architecture.md) - Data protection at rest and in transit
- [Audit Logging](audit-logging.md) - Detailed audit logging patterns
- [Message Context](../core-concepts/message-context.md) - Correlation and tenant propagation

## See Also

- [Audit Logging](./audit-logging.md) — Hash-chained, tamper-evident audit trails with SIEM integration
- [Encryption Providers](./encryption-providers.md) — Available encryption providers and configuration options
- [Custom Middleware](../middleware/custom.md) — Building custom middleware for the Dispatch pipeline
- [Pipeline Overview](../pipeline/index.md) — Understanding the Dispatch pipeline architecture and execution flow
