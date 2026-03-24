---
sidebar_position: 8
title: "Tutorial: Secure Order System in 45 Minutes"
description: Extend the order system with CommandBase, grant-based authorization (A3), and audit logging using Excalibur's compliance infrastructure.
---

# Secure Order System in 45 Minutes

This tutorial extends the [Event-Sourced Order System](./event-sourcing-tutorial.md) with enterprise security: structured commands via `CommandBase`, grant-based authorization via Excalibur.A3, and compliance-grade audit logging.

:::tip Prerequisites
- Completed the [Event-Sourced Order System](./event-sourcing-tutorial.md) tutorial
- SQL Server available (LocalDB, Docker, or remote instance)
- Familiarity with authorization concepts (roles, permissions, grants)
:::

## What You'll Add

- **`CommandBase` / `QueryBase`** — structured commands with correlation IDs, tenant isolation, and transaction control
- **Grant-based authorization** — fine-grained permissions using Excalibur.A3 (`AddGrant`, `RevokeGrant`)
- **Audit logging** — compliance-grade event logging with chain integrity verification

## Step 1: Add Packages

```bash
cd OrderSystem
dotnet add package Excalibur.Application
dotnet add package Excalibur.A3
dotnet add package Excalibur.A3.Abstractions
dotnet add package Excalibur.Dispatch.AuditLogging
dotnet add package Excalibur.Dispatch.Compliance.Abstractions
```

## Step 2: Upgrade to Structured Commands

Replace the simple `IDispatchAction` records with `CommandBase` and `QueryBase`. These add correlation tracking, tenant isolation, and transaction configuration.

```csharp title="Messages/OrderCommands.cs"
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Requests;
using Excalibur.Application.Requests;
using OrderSystem.Domain;
using OrderSystem.ReadModels;
using OrderSystem.Security;

namespace OrderSystem.Messages;

// Commands inherit from AuthorizeCommandBase for authorization support.
// [RequirePermission] ties the command to a grant type — the authorization
// middleware checks this BEFORE the handler runs.
// IAmAuditable tells the audit middleware to log execution automatically.

[RequirePermission(OrderGrants.CreateOrder)]
public sealed class CreateOrderCommand : AuthorizeCommandBase<Guid>, IAmAuditable
{
    public CreateOrderCommand(
        string customerId,
        List<OrderLineData> lines,
        Guid correlationId,
        string? tenantId = null) : base(correlationId, tenantId)
    {
        CustomerId = customerId;
        Lines = lines;
    }

    public string CustomerId { get; set; }
    public List<OrderLineData> Lines { get; set; }
}

[RequirePermission(OrderGrants.CancelOrder, ResourceIdProperty = nameof(OrderId))]
public sealed class CancelOrderCommand : AuthorizeCommandBase<bool>, IAmAuditable
{
    public CancelOrderCommand(
        Guid orderId,
        string reason,
        Guid correlationId,
        string? tenantId = null) : base(correlationId, tenantId)
    {
        OrderId = orderId;
        Reason = reason;
    }

    public Guid OrderId { get; set; }
    public string Reason { get; set; }
}
```

```csharp title="Messages/OrderQueries.cs"
using Excalibur.Application.Requests.Queries;
using OrderSystem.ReadModels;

namespace OrderSystem.Messages;

// Queries inherit from QueryBase for correlation and tenant support
public sealed class GetOrderQuery : QueryBase<OrderSummary?>
{
    public GetOrderQuery(Guid orderId, Guid correlationId, string? tenantId = null)
        : base(correlationId, tenantId)
    {
        OrderId = orderId;
    }

    public Guid OrderId { get; set; }
}

public sealed class GetCustomerOrdersQuery : QueryBase<IReadOnlyList<OrderSummary>>
{
    public GetCustomerOrdersQuery(string customerId, Guid correlationId, string? tenantId = null)
        : base(correlationId, tenantId)
    {
        CustomerId = customerId;
    }

    public string CustomerId { get; set; }
}
```

### What `CommandBase` Gives You

| Feature | How It Works |
|---------|-------------|
| **Correlation ID** | Tracks a command across services via `CorrelationId` |
| **Tenant isolation** | `TenantId` scopes data access in multi-tenant systems |
| **Transaction control** | Configure `TransactionBehavior`, `TransactionIsolation`, `TransactionTimeout` per command |
| **Activity tracing** | Auto-generates `ActivityName`, `ActivityDisplayName` for OpenTelemetry |
| **Message identity** | Every command gets a unique `Id` (UUID) and typed `MessageType` |

### How Commands Connect to Grants

Two things work together:

1. **`[RequirePermission("Orders.Create")]`** — decorates the command class, declaring which grant type is required
2. **`AuthorizeCommandBase<TResponse>`** — extends `CommandBase<TResponse>` with an `IAccessToken` property carrying the caller's identity

The A3 authorization middleware reads the `[RequirePermission]` attribute, extracts the caller's identity from `IAccessToken`, and checks if a matching grant exists in the `IGrantStore`. If no grant matches, the command is rejected with a `ForbiddenException` — the handler never executes.

You can also specify which property holds the resource ID for per-resource grants:

```csharp
// "Orders.Cancel" grant required, scoped to the specific OrderId
[RequirePermission(OrderGrants.CancelOrder, ResourceIdProperty = nameof(OrderId))]
public sealed class CancelOrderCommand : AuthorizeCommandBase<bool>, IAmAuditable { ... }
```

Multiple `[RequirePermission]` attributes use AND logic — all must pass.

## Step 3: Define Grant Types

Grants are permission entries: "user X has permission Y in tenant Z". Define your application's grant types as constants.

```csharp title="Security/OrderGrants.cs"
namespace OrderSystem.Security;

public static class OrderGrants
{
    public const string CreateOrder = "Orders.Create";
    public const string CancelOrder = "Orders.Cancel";
    public const string ViewOrder = "Orders.View";
    public const string ViewAllOrders = "Orders.ViewAll";
}
```

## Step 4: Update Handlers

Handlers now receive structured commands. The authorization check happens in middleware *before* the handler — if the caller lacks the required grant, the handler never runs.

```csharp title="Handlers/CreateOrderHandler.cs"
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;
using OrderSystem.Domain;
using OrderSystem.Messages;

namespace OrderSystem.Handlers;

public class CreateOrderHandler(
    IEventSourcedRepository<OrderAggregate, Guid> repository)
    : IActionHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct)
    {
        var order = new OrderAggregate(Guid.NewGuid());
        order.Create(command.CustomerId, command.Lines);
        order.Confirm();

        await repository.SaveAsync(order, ct);

        return order.Id;
    }
}
```

```csharp title="Handlers/CancelOrderHandler.cs"
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;
using OrderSystem.Domain;
using OrderSystem.Messages;

namespace OrderSystem.Handlers;

public class CancelOrderHandler(
    IEventSourcedRepository<OrderAggregate, Guid> repository)
    : IActionHandler<CancelOrderCommand, bool>
{
    public async Task<bool> HandleAsync(CancelOrderCommand command, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(command.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {command.OrderId} not found.");

        order.Cancel(command.Reason);
        await repository.SaveAsync(order, ct);

        return true;
    }
}
```

## Step 5: Add Audit Logging

Inject `IAuditLogger` to log security-sensitive operations explicitly. Commands marked with `IAmAuditable` are also logged automatically by the A3 audit middleware.

```csharp title="Handlers/GrantManagementEndpoints.cs"
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Compliance;

namespace OrderSystem.Handlers;

public static class GrantManagementEndpoints
{
    public static void MapGrantEndpoints(this WebApplication app)
    {
        // Grant a permission to a user
        app.MapPost("/admin/grants", async (
            GrantRequest req,
            IDispatcher dispatcher,
            IAuditLogger auditLogger,
            CancellationToken ct) =>
        {
            var command = new AddGrantCommand(
                userId: req.UserId,
                fullName: req.FullName,
                grantType: req.GrantType,
                qualifier: req.Qualifier,
                expiresOn: req.ExpiresOn,
                correlationId: Guid.NewGuid(),
                tenantId: req.TenantId);

            var result = await dispatcher.DispatchAsync(command, ct);

            // Explicit audit log for grant operations
            await auditLogger.LogAsync(new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = AuditEventType.Authorization,
                Action = "GrantPermission",
                Outcome = result.IsSuccess ? AuditOutcome.Success : AuditOutcome.Failure,
                Timestamp = DateTimeOffset.UtcNow,
                ActorId = "admin",
                ActorType = "User",
                ResourceId = req.UserId,
                ResourceType = "Grant",
                TenantId = req.TenantId,
                Metadata = new Dictionary<string, string>
                {
                    ["GrantType"] = req.GrantType,
                    ["Qualifier"] = req.Qualifier,
                },
            }, ct);

            return result.IsSuccess ? Results.Created() : Results.BadRequest();
        });

        // Revoke a permission
        app.MapDelete("/admin/grants", async (
            RevokeRequest req,
            IDispatcher dispatcher,
            IAuditLogger auditLogger,
            CancellationToken ct) =>
        {
            var command = new RevokeGrantCommand(
                userId: req.UserId,
                grantType: req.GrantType,
                qualifier: req.Qualifier,
                correlationId: Guid.NewGuid(),
                tenantId: req.TenantId);

            var result = await dispatcher.DispatchAsync(command, ct);

            await auditLogger.LogAsync(new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = AuditEventType.Authorization,
                Action = "RevokePermission",
                Outcome = result.IsSuccess ? AuditOutcome.Success : AuditOutcome.Failure,
                Timestamp = DateTimeOffset.UtcNow,
                ActorId = "admin",
                ActorType = "User",
                ResourceId = req.UserId,
                ResourceType = "Grant",
                TenantId = req.TenantId,
            }, ct);

            return result.IsSuccess ? Results.NoContent() : Results.BadRequest();
        });
    }
}

public record GrantRequest(
    string UserId, string FullName, string GrantType,
    string Qualifier, string? TenantId, DateTimeOffset? ExpiresOn);

public record RevokeRequest(
    string UserId, string GrantType, string Qualifier, string? TenantId);
```

## Step 6: Wire It Up

```csharp title="Program.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.AspNetCore;
using OrderSystem.Domain;
using OrderSystem.Handlers;
using OrderSystem.Messages;
using OrderSystem.ReadModels;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OrderDb")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=OrderSystem;Trusted_Connection=true;";

// 1. Dispatch (messaging + handler discovery)
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.AddDispatchAuthorization(); // Authorization middleware
});

// 2. Event Sourcing (aggregates + event store)
builder.Services.AddExcaliburEventSourcing(es =>
{
    es.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate(id));
});
builder.Services.AddSqlServerEventSourcing(opts => opts.ConnectionString = connectionString);
builder.Services.AddSqlServerProjectionStore<OrderSummary>(opts => opts.ConnectionString = connectionString);

// 3. A3 Grant-Based Authorization
builder.Services.AddExcaliburA3();

// 4. Audit Logging (in-memory for demo; use a persistent store in production)
builder.Services.AddAuditLogging();

var app = builder.Build();

// --- Order Endpoints ---

app.MapPost("/orders", (CreateOrderRequest req, IDispatcher dispatcher, CancellationToken ct) =>
{
    var command = new CreateOrderCommand(
        req.CustomerId, req.Lines, Guid.NewGuid(), req.TenantId);
    return dispatcher
        .DispatchAsync<CreateOrderCommand, Guid>(command, ct)
        .ToCreatedResult(id => $"/orders/{id}");
});

app.MapGet("/orders/{id:guid}", (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
{
    var query = new GetOrderQuery(id, Guid.NewGuid());
    return dispatcher
        .DispatchAsync<GetOrderQuery, OrderSummary?>(query, ct)
        .Match(
            onSuccess: summary => summary is not null ? Results.Ok(summary) : Results.NotFound(),
            onFailure: problem => Results.Problem(detail: problem?.Detail));
});

app.MapGet("/orders/customer/{customerId}",
    (string customerId, IDispatcher dispatcher, CancellationToken ct) =>
    {
        var query = new GetCustomerOrdersQuery(customerId, Guid.NewGuid());
        return dispatcher
            .DispatchAsync<GetCustomerOrdersQuery, IReadOnlyList<OrderSummary>>(query, ct)
            .ToApiResult();
    });

app.MapPost("/orders/{id:guid}/cancel",
    (Guid id, CancelRequest req, IDispatcher dispatcher, CancellationToken ct) =>
    {
        var command = new CancelOrderCommand(id, req.Reason, Guid.NewGuid());
        return dispatcher
            .DispatchAsync<CancelOrderCommand, bool>(command, ct)
            .ToNoContentResult();
    });

// --- Grant Management Endpoints ---
app.MapGrantEndpoints();

app.Run();

record CreateOrderRequest(string CustomerId, List<OrderLineData> Lines, string? TenantId = null);
record CancelRequest(string Reason);
```

## Step 7: Run and Test

```bash
dotnet run
```

```bash
# 1. Grant "create order" permission to user alice
curl -X POST http://localhost:5000/admin/grants \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "alice",
    "fullName": "Alice Smith",
    "grantType": "Orders.Create",
    "qualifier": "*",
    "tenantId": "tenant-1",
    "expiresOn": null
  }'

# 2. Create an order (now authorized)
curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-001",
    "lines": [
      { "productId": "prod-1", "productName": "Widget", "price": 29.99, "quantity": 2 }
    ],
    "tenantId": "tenant-1"
  }'

# 3. Revoke the permission
curl -X DELETE http://localhost:5000/admin/grants \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "alice",
    "grantType": "Orders.Create",
    "qualifier": "*",
    "tenantId": "tenant-1"
  }'
```

## How the Security Layers Work Together

```
CreateOrderCommand (with IAccessToken)
    │
    ▼
AuthorizationMiddleware (from A3)
    │ checks: does caller have "Orders.Create" grant?
    │ NO  → ForbiddenException (403) — handler never runs
    │ YES ↓
    ▼
CreateOrderHandler
    │ executes business logic
    │ saves aggregate via IEventSourcedRepository
    ▼
AuditMiddleware (from A3)
    │ command implements IAmAuditable
    │ logs ActivityAudited event with outcome
    ▼
Response returned to caller
```

### Three Layers of Protection

| Layer | Package | What It Does |
|-------|---------|-------------|
| **Authorization** | `Excalibur.A3` | Checks grants before handler executes — unauthorized commands are rejected |
| **Audit logging** | `Excalibur.Dispatch.AuditLogging` | Records who did what, when, and whether it succeeded |
| **Structured commands** | `Excalibur.Application` | Correlation IDs, tenant isolation, and transaction control on every command |

## Key Concepts

### Grant Model

A grant is a permission record: "user X has permission Y in scope Z until time T".

```
Grant {
    UserId:    "alice"
    GrantType: "Orders.Create"     ← what permission
    Qualifier: "*"                 ← scope (wildcard = all)
    TenantId:  "tenant-1"         ← tenant boundary
    ExpiresOn: null                ← no expiration
    GrantedBy: "admin"
    GrantedOn: 2026-03-18T10:00Z
}
```

Grants are event-sourced aggregates themselves — every grant/revoke is a domain event in the A3 event store.

### Audit Event Structure

Every audit entry follows RFC 7807-aligned structured logging:

```csharp
new AuditEvent
{
    EventId = "...",                          // Unique ID
    EventType = AuditEventType.Authorization, // Category
    Action = "GrantPermission",               // What happened
    Outcome = AuditOutcome.Success,           // Result
    Timestamp = DateTimeOffset.UtcNow,        // When
    ActorId = "admin",                        // Who
    ResourceId = "alice",                     // On what
    ResourceType = "Grant",                   // What kind
    TenantId = "tenant-1",                    // Tenant scope
}
```

### Audit Chain Integrity

The audit store supports chain verification — each event's hash includes the previous event's hash, creating a tamper-evident chain. Verify with:

```csharp
var result = await auditLogger.VerifyIntegrityAsync(
    startDate: DateTimeOffset.UtcNow.AddDays(-7),
    endDate: DateTimeOffset.UtcNow,
    cancellationToken);
// result.IsValid == true if chain is unbroken
```

## Production Checklist

Before going to production, swap out the demo defaults:

| Demo Default | Production Replacement |
|-------------|----------------------|
| `AddAuditLogging()` (in-memory) | `AddAuditLogging<SqlServerAuditStore>()` or custom `IAuditStore` |
| No encryption | `UseAuditLogEncryption()` for PII fields |
| No retention | `AddAuditRetention(opts => opts.RetentionPeriod = TimeSpan.FromDays(365))` |
| No alerting | `AddAuditAlerting(opts => opts.EvaluationMode = EvaluationMode.RealTime)` |
| In-memory grant store | `UseGrantStore<SqlServerGrantStore>()` on the A3 builder |

## Next Steps

| Want to... | Add this |
|-----------|----------|
| Resource-level authorization | Use `ResourceCommandBase<TResourceType, TResponse>` for per-resource grants |
| RBAC on audit data itself | `AddRbacAuditStore()` to restrict who can read audit logs |
| Datadog audit export | `Excalibur.Dispatch.AuditLogging.Datadog` package |
| Time-expiring grants | Set `ExpiresOn` on grants — expired grants are automatically inactive |
| Custom grant stores | Implement `IGrantStore` and register via `UseGrantStore<T>()` |

## Tutorial Series

This is part 3 of the Excalibur tutorial series:

1. [Build an Order System in 15 Minutes](./order-system-tutorial.md) — Dispatch-only messaging
2. [Event-Sourced Order System in 30 Minutes](./event-sourcing-tutorial.md) — Aggregates, event store, projections
3. **Secure Order System in 45 Minutes** — Authorization, audit logging, structured commands *(you are here)*
