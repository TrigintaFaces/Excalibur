---
sidebar_position: 5
title: Authorization
description: Activity-based and grant-based authorization middleware for the Dispatch pipeline using Excalibur.A3.
---

# Authorization

Dispatch supports multiple authorization models. Choose the approach that matches your scenario:

| Approach | Attribute | Identity Source | Package | Best For |
|----------|-----------|----------------|---------|----------|
| **ASP.NET Core Bridge** | `[Authorize]` | `HttpContext.User` | `Excalibur.Dispatch.Hosting.AspNetCore` | ASP.NET Core apps with standard policies |
| **A3 Activity-Based** | `[RequirePermission]` | `IAccessToken` | `Excalibur.A3` | Grant-based, activity-driven authorization |
| **Dispatch Core** | Config-based | `IMessageContext` | `Excalibur.Dispatch` | Custom authorization without ASP.NET Core |

All three can co-exist in the same pipeline — they check different attributes and use different identity sources.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the package for your authorization approach:
  ```bash
  # For A3 activity-based authorization
  dotnet add package Excalibur.A3
  # For ASP.NET Core bridge
  dotnet add package Excalibur.Dispatch.Hosting.AspNetCore
  ```
- Familiarity with [middleware concepts](index.md) and [pipeline stages](../pipeline/index.md)

## ASP.NET Core Authorization Bridge

**Package:** `Excalibur.Dispatch.Hosting.AspNetCore`

For ASP.NET Core applications, use the built-in bridge that reads standard `[Authorize]` attributes and evaluates policies via `IAuthorizationService`. See [Built-in Middleware — Authorization](built-in.md#authorization-middleware) for setup and usage.

---

## A3 Activity-Based Authorization

Dispatch provides grant-based authorization middleware through the **Excalibur.A3** package. Authorization is grant-based and activity-driven — messages declare what activity they represent, and the middleware checks whether the current user has a grant for that activity.

### Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.A3` | Authorization middleware, grant management, `IDispatchAuthorizationService` |
| `Excalibur.A3.Abstractions` | Provider-neutral interfaces: `IAuthorizationEvaluator`, `AuthorizationDecision` |

## How It Works

The `AuthorizationMiddleware` runs at the `DispatchMiddlewareStage.Authorization` stage. It checks messages in two ways:

1. **Interface-based** — The message implements `IRequireAuthorization` (or a derived interface), declaring its `ActivityName`.
2. **Attribute-based** — The message class is decorated with `[RequirePermission("activity.name")]`.

When both are present, **AND logic** applies — both must pass.

If authorization fails, the middleware returns a `MessageProblemDetails` with status 403. The handler is never invoked.

## Setup

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDispatch(builder =>
{
    builder.AddDispatchAuthorization();
});
```

`AddDispatchAuthorization()` registers:
- `GrantsAuthorizationHandler` — evaluates `GrantsAuthorizationRequirement` against grants
- `IDispatchAuthorizationService` — orchestrates authorization checks
- `AttributeAuthorizationCache` — caches `[RequirePermission]` attribute lookups
- `AuthorizationMiddleware` — the pipeline middleware

## Declaring Authorization Requirements

### Interface-Based: IRequireAuthorization

The base interface for messages that need authorization. It declares an `ActivityName`:

```csharp
using Excalibur.A3.Authorization;

public record CreateOrderAction(string CustomerId)
    : IDispatchAction, IRequireAuthorization
{
    public string ActivityName => "Orders.Create";
}
```

The middleware will check whether the current user has a grant for `"Orders.Create"`.

### Activity Authorization with Resources

Use `IRequireActivityAuthorization` when the permission is scoped to a specific resource:

```csharp
using Excalibur.A3.Authorization;

public record UpdateOrderAction(Guid OrderId, string NewStatus)
    : IDispatchAction, IRequireActivityAuthorization
{
    public string ActivityName => "Orders.Update";
    public string? ResourceId => OrderId.ToString();
    public string[] ResourceTypes => ["Order"];
}
```

### Role-Based Authorization

Use `IRequireRoleAuthorization` when the message requires specific roles:

```csharp
using Excalibur.A3.Authorization;

public record DeleteUserAction(Guid UserId)
    : IDispatchAction, IRequireRoleAuthorization
{
    public string ActivityName => "Users.Delete";
    public IReadOnlyCollection<string>? RequiredRoles => ["Admin"];
}
```

### Custom Authorization Requirements

Use `IRequireCustomAuthorization` to supply arbitrary `IAuthorizationRequirement` instances:

```csharp
using Excalibur.A3.Authorization;
using Microsoft.AspNetCore.Authorization;

public record BulkExportAction(string[] TenantIds)
    : IDispatchAction, IRequireCustomAuthorization
{
    public string ActivityName => "Export.Bulk";

    public IEnumerable<IAuthorizationRequirement> AuthorizationRequirements =>
    [
        new GrantsAuthorizationRequirement("Export.Bulk", ["Tenant"]),
        new MinimumAgeRequirement(18)  // custom requirement
    ];
}
```

### Attribute-Based: [RequirePermission]

For simple permission checks without implementing an interface:

```csharp
using Excalibur.A3.Authorization;

[RequirePermission("users.delete")]
public record DeleteUserAction(Guid UserId) : IDispatchAction;

// With resource scoping
[RequirePermission("orders.update", ResourceIdProperty = nameof(OrderId))]
public record UpdateOrderAction(Guid OrderId, string NewStatus) : IDispatchAction;

// Multiple permissions (AND logic — all must pass)
[RequirePermission("orders.create")]
[RequirePermission("inventory.reserve")]
public record CreatePriorityOrderAction(string CustomerId) : IDispatchAction;
```

`ResourceIdProperty` tells the middleware which property contains the resource ID — it extracts the value via reflection at runtime.

### IAmAuthorizable

Messages that carry their own `IAccessToken` implement `IAmAuthorizable`:

```csharp
using Excalibur.A3.Authorization.Requests;

public record TransferFundsAction(decimal Amount, string ToAccount)
    : IDispatchAction, IAmAuthorizable
{
    public string ActivityName => "Funds.Transfer";
    public IAccessToken? AccessToken { get; set; }
}
```

## The Grant Model

Grants are the core authorization primitive. A `Grant` is an event-sourced aggregate with a composite key `"{UserId}:{Scope}"` where `Scope` is `"{TenantId}:{GrantType}:{Qualifier}"`.

### Grant Lifecycle

```csharp
using Excalibur.A3.Authorization.Grants;

// Create a grant
var grant = new Grant(
    userId: "user-123",
    fullName: "Jane Smith",
    tenantId: "tenant-abc",
    grantType: "Activity",
    qualifier: "Orders.Create",
    expiresOn: DateTimeOffset.UtcNow.AddYears(1),
    grantedBy: "admin-456");

// Check grant state
grant.IsActive();   // true if not expired and not revoked
grant.IsExpired();   // true if ExpiresOn <= UtcNow
grant.IsRevoked();   // true if RevokedOn has a value

// Revoke a grant
grant.Revoke(revokedBy: "admin-789");
```

### GrantScope

Scopes are structured as `TenantId:GrantType:Qualifier`:

```csharp
using Excalibur.A3.Authorization.Grants;

var scope = new GrantScope("tenant-abc", "Activity", "Orders.Create");
// scope.ToString() => "tenant-abc:Activity:Orders.Create"

// Parse from string
var parsed = GrantScope.FromString("tenant-abc:Activity:Orders.Create");
```

## IAuthorizationPolicy

The `IAuthorizationPolicy` interface provides grant-checking methods used by the middleware and `IAccessToken`:

```csharp
IAuthorizationPolicy policy = ...;

// Check if authorized for an activity
bool canCreate = policy.IsAuthorized("Orders.Create");

// Check if authorized for a specific resource
bool canUpdate = policy.IsAuthorized("Orders.Update", resourceId: "order-123");

// Check grants directly
bool hasGrant = policy.HasGrant("Orders.Create");
bool hasTypedGrant = policy.HasGrant<CreateOrderActivity>();
bool hasResourceGrant = policy.HasGrant("Order", "order-123");
```

## IAccessToken

`IAccessToken` combines `IAuthenticationToken` and `IAuthorizationPolicy` into a single object — it provides both identity and authorization checks:

```csharp
using Excalibur.A3;

IAccessToken token = ...;

// Authentication
string userId = token.UserId;

// Authorization (via IAuthorizationPolicy)
bool canCreate = token.IsAuthorized("Orders.Create");
bool hasGrant = token.HasGrant<CreateOrderActivity>();
```

## IAuthorizationEvaluator

For provider-neutral evaluation using structured subject/action/resource:

```csharp
using Excalibur.A3.Abstractions.Authorization;

public class MyService
{
    private readonly IAuthorizationEvaluator _evaluator;

    public async Task DoWorkAsync(CancellationToken ct)
    {
        var decision = await _evaluator.EvaluateAsync(
            subject: new AuthorizationSubject("user-123", null, null),
            action: new AuthorizationAction("Orders.Create", null),
            resource: new AuthorizationResource("Order", "order-456", null),
            cancellationToken: ct);

        if (decision.Effect != AuthorizationEffect.Permit)
        {
            // Handle denial — decision.Reason contains details
        }
    }
}
```

## Authorization Results

When authorization fails, the middleware returns a 403 result:

```csharp
var result = await dispatcher.DispatchAsync(action, ct);

if (!result.IsSuccess && result.ProblemDetails is { Status: 403 } problem)
{
    // problem.Title == "Authorization Failed"
    // problem.Detail contains failure reason
}
```

## Next Steps

- [Validation](validation.md) — Input validation middleware
- [Custom Middleware](custom.md) — Build your own middleware
- [Authorization & Audit (A3)](../security/authorization.md) — Full A3 documentation including audit events

## See Also

- [Security: Authorization](../security/authorization.md) - Full A3 authorization documentation including audit events and grant management
- [Middleware Overview](index.md) - How middleware fits into the Dispatch pipeline and stage ordering
- [Custom Middleware](custom.md) - Build your own middleware for application-specific cross-cutting concerns
