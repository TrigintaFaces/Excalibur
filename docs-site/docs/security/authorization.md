---
sidebar_position: 2
title: Authorization & Audit (A3)
description: Activity-based authorization, grant management, token validation, and audit logging with Excalibur.A3.
---

# Authorization & Audit (A3)

Excalibur.A3 provides a unified **Authentication, Authorization, and Audit** (A3) system that integrates with the Dispatch pipeline. It supports activity-based authorization, fine-grained grants, token validation, and structured audit events.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  # Full-stack (CQRS, Dispatch pipeline, authentication services)
  dotnet add package Excalibur.A3

  # Lightweight / standalone (grant management + authorization only)
  dotnet add package Excalibur.A3.Core
  ```
- Full-stack: Familiarity with [Dispatch pipeline](../pipeline/index.md) and [security concepts](./index.md)
- Standalone: No prerequisites beyond basic .NET DI knowledge

## Packages

| Package | Dependencies | Purpose |
|---------|-------------|---------|
| `Excalibur.A3.Core` | A3.Abstractions, Domain, Dispatch.Abstractions | Lightweight core: in-memory stores, grant management, authorization evaluation |
| `Excalibur.A3` | A3.Core + Application, EventSourcing, Dispatch, ... | Full-stack: CQRS commands, Dispatch middleware, authentication HTTP services, audit pipeline |
| `Excalibur.A3.Abstractions` | -- | Provider-neutral interfaces: `IGrantStore`, `IActivityGroupStore`, `IA3Builder`, `Grant` |
| `Excalibur.A3.AspNetCore` | A3 | ASP.NET Core integration: bridges the authenticated request principal into grant evaluation and adds per-request resource scope for controller and minimal API endpoints |

:::tip Choose the Right Package

- **Building governance primitives, microservices, or lightweight tools?** Use `Excalibur.A3.Core` -- 3 dependencies, no database required.
- **Full application with CQRS, event sourcing, and Dispatch pipeline?** Use `Excalibur.A3` -- includes everything in A3.Core plus the full stack.
:::

## Setup

### Standalone Setup (A3.Core)

For standalone grant management and authorization without the Dispatch pipeline:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Minimal registration -- in-memory stores, no pipeline, no database
services.AddExcaliburA3Core();
```

This registers:
- `IGrantStore` → `InMemoryGrantStore` (singleton, thread-safe, `ConcurrentDictionary`-backed)
- `IActivityGroupStore` → `InMemoryActivityGroupStore` (singleton, thread-safe)
- Returns `IA3Builder` for overriding stores with custom implementations

To override the default in-memory stores:

```csharp
services.AddExcaliburA3Core()
    .UseGrantStore<MyGrantStore>()
    .UseActivityGroupStore<MyActivityGroupStore>();
```

:::info Implementing `IActivityGroupStore` yourself?
Its read members now take a tenant and the estate-wide delete reports the tenants it emptied. See
[Authorization grants require a tenant](../migration/authorization-tenant-required.md#if-you-implement-iactivitygroupstore-yourself)
for the before/after signatures.
:::

**What you get:** Grant CRUD, activity group management, `GetService(Type)` ISP access to `IGrantQueryStore` and `IActivityGroupGrantStore`.

**What you do NOT get:** Dispatch pipeline, CQRS commands (`AddGrantCommand`, `RevokeGrantCommand`), authentication HTTP clients, audit middleware, event-sourced Grant aggregate — **and no startup check on grant durability.**

:::warning Grants registered this way do not survive a restart, and nothing tells you

`AddExcaliburA3Core()` defaults `IGrantStore` to the in-memory store and installs no startup check, so the application starts normally on a store that loses every grant when the process ends.

The failure that follows does not look like an outage. A user whose grants have vanished is indistinguishable from a user who never had any, so the application comes back up, reports itself healthy, and **denies everything to everyone** — having confirmed every grant as saved beforehand. Any process replacement does it: a rolling update, a scale-out whose new instance starts with its own empty dictionary, a container restart, an idle-timeout recycle.

**In production, register a durable grant store** with `UseGrantStore<T>()`, as shown above.

`AddExcaliburA3()` behaves differently: it **refuses to start** on a volatile grant store unless the host registers a durable one or sets `GrantDurabilityOptions.AllowVolatileGrantStore` to `true` as a deliberate statement that losing grants on restart is acceptable. That option has **no effect** on the `AddExcaliburA3Core()` composition, which performs no such check.

:::

### Full-Stack Setup (A3)

Register full A3 services using the builder pattern. `AddExcaliburA3()` internally calls `AddExcaliburA3Core()`, then adds CQRS, Dispatch pipeline, and authentication:

:::caution Two prerequisites, or the container will not build

`AddExcaliburA3()` composes services that depend on two things it does not register itself. Supply
both **before** it, or building the provider throws — and in the Development environment
`WebApplicationBuilder` turns on `ValidateOnBuild`, so this is what you hit on the machine you are
developing on.

```csharp
// 1. An application-scoped distributed cache — TWO registrations, not one. The keyed wrapper wraps
//    whichever unkeyed cache the container holds, so the wrapper alone has nothing to wrap.
//    Requires the Excalibur.Dispatch.Caching package.
services.AddDistributedMemoryCache();                      // or Redis / SQL Server in production
services.AddApplicationScopedDistributedCache(o => o.Scope = "my-application");

// 2. An IAuthenticationToken to resolve the caller. The HTTP bridge ships in Excalibur.A3.AspNetCore.
services.AddHttpGrantAuthorization();
```

The `Scope` string is what keeps two applications sharing one cache from reading each other's grants.
:::

### How long an authorization decision stays cached

Authorization results are cached, so a grant you revoke can still authorize until its cache entry
expires. `AuthorizationCacheOptions.AbsoluteExpirationRelativeToNow` is the upper bound on that
window: it is measured from when the entry was written, and reads do not extend it, so the delay
between a revocation and its taking effect can never exceed it. The default is 5 minutes.

```csharp
services.Configure<AuthorizationCacheOptions>(o =>
    o.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1));
```

The value must be a positive duration; it is validated at startup, so an invalid one fails the host
rather than silently disabling the bound. Shorten it when your revocations must take effect sooner,
at the cost of more reads reaching the grant store.

```csharp
using Microsoft.Extensions.DependencyInjection;

// SQL providers (connection configured via IDataRequest/IDomainDb)
services.AddExcaliburA3()
    .UseSqlServer();

// Or PostgreSQL
services.AddExcaliburA3()
    .UsePostgres();

// NoSQL providers (options configured inline)
services.AddExcaliburA3()
    .UseCosmosDb(options => { options.DatabaseId = "mydb"; options.ContainerId = "grants"; });

services.AddExcaliburA3()
    .UseMongoDB(options => { options.DatabaseName = "mydb"; });

services.AddExcaliburA3()
    .UseDynamoDb(options => { options.TableName = "grants"; });

services.AddExcaliburA3()
    .UseFirestore(options => { options.ProjectId = "my-project"; });
```

#### Database schema for the SQL providers

The SQL Server and PostgreSQL stores **never create their tables at runtime**. Apply the schema yourself
before registering the store; without it, every operation fails on a missing table. The script ships
inside the provider package, under `scripts/` (in your NuGet package cache at
`~/.nuget/packages/excalibur.data.sqlserver/<version>/scripts/`, and likewise for
`excalibur.data.postgres`):

| Provider | Scripts |
|---|---|
| SQL Server | `002_CreateActivityGroupSchema.sql`, `003_CreateGrantSchema.sql` |
| PostgreSQL | `002_CreateActivityGroupSchema.sql`, `003_CreateGrantSchema.sql` |

Shipping a script does not migrate anything for you: you run it, once, like any other migration. Each
statement is guarded, so re-running it is safe. The SQL Server scripts are single batches with no `GO`
separators, so they also run when submitted as one command.

If you already have a schema of your own, adapt it to these contracts rather than running the scripts.

**Activity groups** (`002_CreateActivityGroupSchema.sql`):

| SQL Server column | PostgreSQL column | Type | Null | Notes |
|---|---|---|---|---|
| `authz.ActivityGroup.TenantId` | `authz.activity_group.tenant_id` | `NVARCHAR(64)` / `VARCHAR(64)` | No | Default `__untenanted__`. SQL Server uses a binary collation (`Latin1_General_BIN2`) so tenant comparison is exact. |
| `Name` | `name` | `NVARCHAR(128)` / `VARCHAR(128)` | No | The group name. Unique per tenant, not globally. |
| `ActivityName` | `activity_name` | `NVARCHAR(256)` / `VARCHAR(256)` | No | One activity the group confers; a group is stored one row per activity. |

**Primary key: `(TenantId, Name, ActivityName)`.** The widths are sized to fit SQL Server's 900-byte
limit on a clustered key (64 + 128 + 256 characters at two bytes each is 896); keep them within that limit
if you adapt the table, or long names will be refused on insert. The stores reject a longer name with an
`ArgumentException` before it reaches the database, so both providers accept exactly the same names. The tenant is part of the key, not merely a column.
Two tenants may each own a group with the same name, and those are separate groups. A key without the
tenant would make the second tenant's group collide with the first. Keep the tenant in the key if you
adapt the table.

**Grants** (`003_CreateGrantSchema.sql`). `authz.Grant` holds the grants in force; `authz.GrantHistory`
records every revocation. On PostgreSQL the tables are `authz."grant"` (`grant` is a reserved word) and
`authz.grant_history`, with the snake_case column names shown.

| SQL Server column | PostgreSQL column | Type | Null | Notes |
|---|---|---|---|---|
| `UserId` | `user_id` | `NVARCHAR(128)` / `VARCHAR(128)` | No | Part of the key. |
| `TenantId` | `tenant_id` | `NVARCHAR(64)` / `VARCHAR(64)` | No | Part of the key. Default `__untenanted__`. |
| `GrantType` | `grant_type` | `NVARCHAR(64)` / `VARCHAR(64)` | No | Part of the key. |
| `Qualifier` | `qualifier` | `NVARCHAR(192)` / `VARCHAR(192)` | No | Part of the key. |
| `FullName` | `full_name` | `NVARCHAR(256)` / `VARCHAR(256)` | Yes | |
| `ExpiresOn` | `expires_on` | `DATETIMEOFFSET` / `TIMESTAMPTZ` | Yes | `NULL` never expires. |
| `GrantedBy` | `granted_by` | `NVARCHAR(128)` / `VARCHAR(128)` | No | |
| `GrantedOn` | `granted_on` | `DATETIMEOFFSET` / `TIMESTAMPTZ` | No | |

**Primary key: `(UserId, TenantId, GrantType, Qualifier)`**, 896 bytes on SQL Server (128 + 64 + 64 + 192
characters at two bytes each), inside its 900-byte key limit. Saving a grant that already exists replaces
its details. The stores reject a longer value with an `ArgumentException` before it reaches the database.
`authz.GrantHistory` has the same eight columns plus `RevokedBy` and `RevokedOn`, and a surrogate key: one
grant can be revoked, granted again and revoked again, so its identity repeats in history.

**Comparisons are exact.** Every grant lookup compares with a binary collation (`Latin1_General_BIN2` on
SQL Server, `"C"` on PostgreSQL), so `Admin` and `admin` are different grants even on a table you created
with a case-insensitive default. On PostgreSQL, keep `expires_on` and `granted_on` as `TIMESTAMPTZ`:
expiry is compared with `now()`, and a column without a time zone is read in the session's time zone.

For custom store implementations:

```csharp
services.AddExcaliburA3()
    .UseGrantStore<MyGrantStore>()
    .UseActivityGroupStore<MyActivityGroupStore>();
```

The builder also registers Dispatch pipeline integration (`AddExcaliburAuthorization()`) automatically.

:::tip Single-Tenant Applications

`AddExcaliburA3()` registers the ambient `ITenantContext` (via `AddTenantContext()`). That context reports whichever tenant the current execution flow has established through `TenantContextHolder.BeginScope` — it does not select a tenant of its own, and no framework component substitutes one when the scope is empty. On `Excalibur.Hosting.Web`, the per-request middleware opens that scope from the request's `X-Tenant-Id` header or `tenantId` query value (both must parse as a `Guid`); when a request supplies neither, the scope is opened with no tenant and `ITenantContext.HasTenant` reports `false`. A single-tenant deployment that wants every operation to run under one identity opens the scope itself, using the framework's single-tenant identifier `TenantDefaults.DefaultTenantId`. Read that identifier from the constant rather than copying its literal value — it is the identity your rows are stored under, so a hardcoded copy that drifts from it silently changes which rows an operation can see. All tenant-scoped features — grants, authorization policies, audit search — read the ambient tenant.

For **multi-tenant** applications that serve multiple tenants from a single instance, establish the ambient tenant per request before A3 evaluates — the hosting middleware derives it from the request, or scope it explicitly:

```csharp
services.AddExcaliburA3()
    .UsePostgres();

// Per request: establish the ambient tenant before dispatching.
var tenantId = httpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault();
using (TenantContextHolder.BeginScope(tenantId))
{
    // A3 authorization, grants, and audit read the ambient ITenantContext here.
}
```

A3 **fails closed**: if authorization runs with no ambient tenant resolved, it throws rather than silently defaulting.
:::

## Authentication

### Authenticated Identity

A3 does not validate tokens, and provides no validation extension point. Validation belongs to your
host's authentication stack -- ASP.NET Core JWT bearer authentication, for example. A3 consumes the
identity that stack has already authenticated, through `IAuthenticationToken`, which your host registers:

```csharp
using System.Security.Claims;
using Excalibur.A3.Authentication;

// Project your host's validated principal onto IAuthenticationToken.
public sealed class ClaimsPrincipalAuthenticationToken(ClaimsPrincipal principal) : IAuthenticationToken
{
    public AuthenticationState AuthenticationState =>
        principal.Identity?.IsAuthenticated == true
            ? AuthenticationState.Authenticated
            : AuthenticationState.Anonymous;

    public string? UserId => principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public IEnumerable<Claim>? Claims => principal.Claims;
    public string? Login => principal.FindFirst(ClaimTypes.Name)?.Value;
    public string? FirstName => principal.FindFirst(ClaimTypes.GivenName)?.Value;
    public string? LastName => principal.FindFirst(ClaimTypes.Surname)?.Value;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string? Jwt { get; set; }
}

services.AddScoped<IAuthenticationToken, ClaimsPrincipalAuthenticationToken>();
```

`AddA3(...)` separately registers `IAuthenticationTokenProvider`, an `HttpClient`-backed provider used to
*acquire* a token for outbound calls. That is a different concern from validating an inbound one.

### Access Token

`IAccessToken` unifies authentication and authorization into a single object that combines `IAuthenticationToken` and `IAuthorizationPolicy`:

```csharp ignore
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

```csharp ignore
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

### ASP.NET Core Endpoints

Grant authorization also works on ordinary ASP.NET Core endpoints — MVC controller actions and minimal
API endpoints — for requests that never go through Dispatch.

```bash
dotnet add package Excalibur.A3.AspNetCore
```

Two pieces are needed that the framework-agnostic grant evaluator cannot supply on its own: the
identity and tenant have to come from the request, and a policy has to be able to name the resource
the request is about. One call registers both:

```csharp
builder.Services
    .AddExcaliburA3()
    .Services
    .AddHttpGrantAuthorization();

var app = builder.Build();

app.UseAuthentication();   // must run before UseAuthorization
app.UseAuthorization();
```

`AddHttpGrantAuthorization` performs no authentication of its own. It reads the `ClaimsPrincipal` that
your existing authentication scheme — JWT bearer, cookies, OpenID Connect — already established for the
request. Configure that scheme exactly as you would in any other ASP.NET Core application.

#### Applying a grant to an endpoint

Use `[RequireGrant]`. It is strongly typed, so a mistake is a compile error rather than a request that
fails at runtime, and it works on both hosting styles — a controller action carries it as an attribute,
and a minimal API endpoint passes an instance to `RequireAuthorization`:

```csharp
using Excalibur.A3.AspNetCore;

// Controller action
[HttpGet("/orders/{id}")]
[RequireGrant("Read", "Order", "id")]
public IActionResult GetById(string id) => Ok(id);

// Minimal API endpoint
app.MapGet("/orders/{id}", (string id) => Results.Ok(id))
   .RequireAuthorization(new RequireGrantAttribute("Read", "Order", "id"));
```

The third argument names a **route parameter**, not a resource identifier: it is read from the matched
endpoint's route values at request time, so one attribute covers every order. Drop it to require the
activity against the resource type without narrowing to a single resource.

#### Policy names

`[RequireGrant]` sets an equivalent policy name, and that name can also be written directly wherever a
seam accepts only a string. A grant policy name says which activity is required, on which resource type,
and optionally which resource:

| Name | Meaning |
|------|---------|
| `grant:Read:Order` | The caller holds `Read` for the `Order` resource type. |
| `grant:Read:Order:{id}` | The caller holds `Read` for the specific order named by route parameter `id`. |
| `grant:Read:Order:order-42` | The caller holds `Read` for the fixed resource `order-42`. |

The braced form is what expresses *may read **this** order*: the identifier is read from the matched
endpoint's route values at request time, so a single policy covers every order.

Build the name with `GrantPolicyName` rather than composing the string by hand:

```csharp
var policyName = GrantPolicyName.ForRouteValue("Read", "Order", "id");
```

Prefer `[RequireGrant]` where you can. Reach for the name when a seam accepts only a string, or when you
need `[Authorize(Policy = "grant:Read:Order:{id}")]` on a member where a constant is the only option.

A policy name that does not begin with `grant:` is left alone, so policies you register by name —
including those from `AddGrantAuthorization` — continue to resolve normally, as does a bare
`RequireAuthorization()` with no policy at all.

#### Claim mapping

The user and tenant are read from the first matching claim in an ordered candidate list, because
identity providers disagree about which claim carries them. The defaults cover OpenID Connect,
JWT-bearer and WS-Federation shaped principals; add your own at the front when they differ:

```csharp
builder.Services.AddHttpGrantAuthorization(options =>
{
    options.UserIdClaimTypes.Insert(0, "urn:my-idp:subject");
    options.TenantIdClaimTypes.Insert(0, "org");

    // A single-tenant host whose identity provider issues no tenant claim.
    options.DefaultTenantId = "default";
});
```

When the principal carries no tenant claim, the ambient tenant established by your own middleware is
used, and then `DefaultTenantId`. If none of the three yields a tenant, the request is denied — a
tenant is never assumed.

#### What a denial means

Authorization fails closed, and every denial that is not simply a missing grant is logged with its
cause, so a `403` is never left ambiguous:

- An **unauthenticated** caller receives a `401` challenge rather than a `403`.
- A caller whose **user or tenant cannot be resolved** is denied, and a warning names the cause.
- A **resource-scoped policy on an endpoint whose route declares no such parameter** is denied, and a
  warning names the parameter. It is never downgraded to the unscoped check, which would grant more
  than the policy asked for.
- A **missing registration** fails at host start with the call that fixes it, rather than turning
  every request into a `403`.

:::tip Two interfaces share a name

`Excalibur.A3.Authorization.IAuthorizationPolicyProvider` supplies the caller's **grants**;
`Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider` supplies ASP.NET Core **policies**.
They are different contracts. Alias the A3 one when both namespaces are in scope:

```csharp
using A3PolicyProvider = Excalibur.A3.Authorization.IAuthorizationPolicyProvider;
```
:::

A runnable example covering both hosting styles is in `samples/06-security/GrantAuthorizedApi`.

### Authorization Service

`IDispatchAuthorizationService` evaluates authorization using ASP.NET Core `IAuthorizationRequirement` and named policies:

```csharp ignore
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
        return new AuthorizationDecision(AuthorizationEffect.Permit);
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


### Wildcard Grants

Grant scopes support wildcard patterns for broad permission grants. A `GrantScope` is a wildcard if any segment is `*` or the qualifier ends with `.*` or `/*`.

**Wildcard patterns:**

| Pattern | Matches | Example |
|---------|---------|--------|
| `*:*:*` | All grants across all tenants | Global admin |
| `tenant-abc:*:*` | All grants within a tenant | Tenant admin |
| `tenant-abc:activity:Orders.*` | All qualifier prefixes under `Orders.` | `Orders.Create`, `Orders.Delete` |
| `tenant-abc:activity:orders/*` | All qualifier prefixes under `orders/` | `orders/create`, `orders/delete` |

**Validation:**

Use `GrantScope.Validate` to check wildcard patterns before creating grants. Invalid patterns (such as `**`, `*partial`, or mid-qualifier wildcards) are rejected:

```csharp
using Excalibur.A3.Authorization.Grants;

if (!GrantScope.Validate(tenantId: "*", grantType: "activity", qualifier: "Orders.*", out var error))
{
    // error describes the validation failure
    throw new ArgumentException(error);
}

// Valid -- create the wildcard grant
var scope = new GrantScope("*", "activity", "Orders.*");
```

**Specificity and matching:**

When multiple wildcard grants match a request, the most specific grant wins. The framework automatically ranks wildcards by specificity:

- Each non-wildcard segment (TenantId, GrantType) is more specific than `*`
- Exact qualifier beats suffix wildcards (`prefix.*`, `prefix/*`)
- Suffix wildcards beat full wildcard (`*`)
- Longer prefixes win tiebreakers between suffix patterns

The authorization policy uses a dual-index strategy: exact grants are checked first via O(1) hash lookup, then wildcard grants are evaluated in descending specificity order.

## Audit

Authorization answers *whether* an action was permitted; auditing records *that it happened*. They are
separate opt-ins — enabling authorization does not start recording an audit trail.

Register auditing on the Excalibur builder:

```csharp
services.AddExcalibur(excalibur => excalibur.AddAudit());
```

`AuditMiddleware` runs at the **end** of the dispatch pipeline (`DispatchMiddlewareStage.End`) and records
an entry for every action that implements `IAmAuditable`. Marking an action auditable is what opts it in —
there is no blanket "audit everything" switch, so an action that does not implement the interface produces
no entry no matter how it was authorized.

Because the middleware sits at the end of the pipeline, it observes the action after authorization has
already admitted it. **A denied action is rejected earlier and therefore does not reach this middleware** —
if you need a record of refused attempts, capture it at the authorization boundary rather than expecting it
in the audit trail.

For the durable audit store, retention, and the annotation surface, see
[Audit Logging](../compliance/audit-logging.md).

## Conditional Authorization (When Expressions)

The `[RequirePermission]` attribute supports a `When` property for runtime conditional checks. Expressions are parsed at startup and cached as ASTs for zero-allocation evaluation.

### Basic Usage

```csharp
[RequirePermission("orders.approve", When = "resource.Amount <= 10000")]
public class ApproveOrderCommand : IDispatchAction
{
    public decimal Amount { get; set; }
}
```

If the user has the `orders.approve` permission **and** the order amount is at most 10,000, authorization succeeds. Otherwise it fails even with a valid grant.

### Expression Grammar

Expressions support three attribute categories: `subject`, `action`, and `resource`.

```
subject.Role == 'admin'
resource.Amount > 10000
subject.Department == 'finance' AND resource.Amount <= 50000
NOT subject.IsExternal
(subject.Role == 'admin' OR subject.Role == 'manager') AND resource.Status != 'archived'
```

**Operators:** `==`, `!=`, `>`, `<`, `>=`, `<=`, `contains`, `startsWith`
**Logic:** `AND`, `OR`, `NOT`, parentheses
**Values:** string literals (`'value'`), numbers, `true`, `false`, `null`

### Advanced Examples

```csharp
// Time-based access
[RequirePermission("reports.view", When = "subject.Role == 'auditor'")]
public class ViewFinancialReport : IDispatchAction { }

// Multi-condition
[RequirePermission("transfers.execute",
    When = "resource.Amount <= 50000 AND subject.Department == 'treasury'")]
public class ExecuteTransfer : IDispatchAction
{
    public decimal Amount { get; set; }
    public string Department { get; set; } = string.Empty;
}
```

### How Attributes Are Resolved

| Category | Source |
|----------|--------|
| `subject.*` | Claims from the authenticated principal |
| `action.*` | Properties of the dispatched message |
| `resource.*` | Properties of the dispatched message (alias for action) |

### Audit Events

`IAuditEvent` captures structured audit data:

```csharp
using Excalibur.A3.Auditing;

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

### Audit Store

Audit persistence lives in `Excalibur.AuditLogging`. Applications write events through `IAuditLogger`;
storage is pluggable by implementing `IAuditStore`:

```csharp
using Excalibur.Compliance;
using Microsoft.Extensions.DependencyInjection;

// A shipped store is registered by its own extension method, alongside AddAuditLogging():
services.AddAuditLogging();
services.AddSqlServerAuditStore(options =>
{
    options.ConnectionString = configuration.GetConnectionString("Audit")!;
});

// Anywhere in the application:
public sealed class OrderService(IAuditLogger auditLogger)
{
    public Task<AuditEventId> RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        => auditLogger.LogAsync(auditEvent, cancellationToken);
}
```

`AddAuditLogging()` on its own registers an in-memory store, which is suitable for development and tests
but not for production retention. Add a provider package — `Excalibur.AuditLogging.SqlServer` or
`Excalibur.AuditLogging.Postgres` — and call its registration method for durable storage.

**The generic overload is for a store you wrote**, not for a shipped one:

```csharp
// Your own IAuditStore implementation:
services.AddAuditLogging<MyAuditStore>();
```

The shipped provider stores are `internal`, so they cannot be named as the type argument — register them
through `AddSqlServerAuditStore(...)` or `AddPostgresAuditStore(...)` instead.

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

## Store Pattern

A3 uses a **store pattern** modeled after ASP.NET Core Identity (`IUserStore<T>`, `IRoleStore<T>`, `IdentityBuilder`). Store interfaces live in `Excalibur.A3.Abstractions` and each database provider implements them in its own package.

### Store Interfaces

| Interface | Methods | Purpose |
|-----------|---------|---------|
| `IGrantStore` | 5 + `GetService(Type)` | Core grant CRUD (get, getAll, save, delete, exists) |
| `IGrantQueryStore` | 2 | ISP sub-interface for advanced queries (matching, find) |
| `IActivityGroupStore` | 4 + `GetService(Type)` | Activity group operations (exists, findAll, deleteAll, create) |
| `IActivityGroupGrantStore` | 4 | Bridging ISP for activity-group grant operations |
| `IActivityGroupGrantReplacement` | 2 | **Optional capability.** Replaces a set of activity-group grants in one atomic step |

#### Synchronizing grants from a remote authority

The `IActivityGroupService` sync methods are a **full refresh**: the authority's snapshot becomes the
whole set of grants. Replacing that set has to be atomic, or a reader caught between the delete and the
inserts is denied access the snapshot confers, and a sync that fails part-way leaves the set partial
until the next one succeeds.

**SQL Server, PostgreSQL and the in-memory store implement `IActivityGroupGrantReplacement` and do this
in one transaction.** Cosmos DB, DynamoDB, Firestore and MongoDB cannot, so composing one of them fails
at start-up until you say that you accept the non-atomic sync:

```csharp
services.Configure<ActivityGroupSyncOptions>(options =>
    options.GrantSyncAtomicity = GrantSyncAtomicity.BestEffort);
```

That logs one warning at start-up and runs the delete-then-insert. A store that implements the
capability ignores the setting.

**An empty response means different things to the two grant syncs.** For
`SyncActivityGroupGrantsAsync(userId)` an empty list is applied — that user now holds no activity-group
grants, and every one of theirs is revoked. For `SyncAllActivityGroupGrantsAsync()` it is refused and
nothing is deleted, because it would revoke every user's grants at once and an empty response cannot be
told apart from a filter or a schema change returning nothing. A body that did not deserialize into a
list is a failed fetch in both cases and is never read as "there are none".

Advanced features are accessed via the `GetService(Type)` escape hatch rather than adding optional methods to the core interface:

```csharp
// Access advanced query capabilities from IGrantStore
IGrantStore store = ...;
var queryStore = store.GetService(typeof(IGrantQueryStore)) as IGrantQueryStore;
if (queryStore is not null)
{
    // One tenant's grants. A null filter means "any"; an empty string is refused, never read as "all".
    var grants = await queryStore.GetMatchingGrantsAsync(
        tenantId, userId: null, grantType: "Role", qualifier: "Admin", cancellationToken);
}
```

`GetMatchingGrantsAsync` reads **one tenant**. It returns that tenant's non-revoked grants (expired ones
included) whose fields equal every filter you pass, compared exactly: case-sensitive, with no wildcards.
Pass `null` to leave a field unconstrained; an empty or whitespace value throws `ArgumentException`. A
single-tenant application passes `TenantScope.UntenantedSentinel` as the tenant.

`GetMatchingGrantsAcrossTenantsAsync` is the deliberate estate-wide read, for operator reports. Use it
only to read: nothing that revokes a grant should be driven from a result that spans every tenant. On
partitioned stores it costs a cross-partition query (Cosmos DB) or a table scan (DynamoDB).

### Builder Pattern (`IA3Builder`)

`AddExcaliburA3()` returns an `IA3Builder` that configures store providers via fluent `Use*()` methods:

```csharp
public interface IA3Builder
{
    IServiceCollection Services { get; }
    IA3Builder UseGrantStore<TStore>() where TStore : class, IGrantStore;
    IA3Builder UseActivityGroupStore<TStore>() where TStore : class, IActivityGroupStore;
}
```

Each provider package ships a single extension method (e.g., `UseSqlServer()`, `UseCosmosDb(Action<CosmosDbAuthorizationOptions>)`) that registers the appropriate store implementations. SQL providers use existing `IDataRequest` infrastructure for connection management; NoSQL providers accept an options callback with `ValidateOnStart()`.

### Available Providers

| Provider | Extension | Options |
|----------|-----------|---------|
| SQL Server | `.UseSqlServer()` | Connection via `IDataRequest` |
| PostgreSQL | `.UsePostgres()` | Connection via `IDataRequest` |
| Cosmos DB | `.UseCosmosDb(Action<CosmosDbAuthorizationOptions>)` | `DatabaseId`, `ContainerId` |
| MongoDB | `.UseMongoDB(Action<MongoDbAuthorizationOptions>)` | `DatabaseName` |
| DynamoDB | `.UseDynamoDb(Action<DynamoDbAuthorizationOptions>)` | `TableName` |
| Firestore | `.UseFirestore(Action<FirestoreAuthorizationOptions>)` | `ProjectId` |

## IAM Governance

The governance layer adds enterprise IAM capabilities (role management, access reviews, separation of duties, provisioning) on top of A3's grant infrastructure.

### Governance Packages

| Package | Dependencies | Purpose |
|---------|-------------|---------|
| `Excalibur.A3.Governance.Abstractions` | A3.Abstractions only | All governance interfaces, enums, records, and options: roles, access reviews, SoD, orphaned access, provisioning, JIT, non-human identity, API keys, entitlement reporting |
| `Excalibur.A3.Governance` | Governance.Abstractions + A3.Core | All governance implementations: 3 aggregates, in-memory stores, 3 background services, SoD middleware, entitlement provider. 8 builder extensions |

### Governance Setup

Add governance capabilities via the fluent `AddGovernance()` extension on `IA3Builder`:

```csharp
services.AddExcaliburA3Core()
    .AddGovernance(g => g
        .AddRoles(opts =>
        {
            opts.MaxHierarchyDepth = 3;
            opts.EnforceUniqueNames = true;
        })
        .AddAccessReviews(opts =>
        {
            opts.DefaultCampaignDuration = TimeSpan.FromDays(14);
            opts.DefaultExpiryPolicy = AccessReviewExpiryPolicy.RevokeUnreviewed;
        })
        .AddSeparationOfDuties(opts =>
        {
            opts.MinimumEnforcementSeverity = SoDSeverity.Critical;
            opts.DetectiveScanInterval = TimeSpan.FromHours(12);
        })
        .AddOrphanedAccessDetection(opts =>
        {
            opts.ScanIntervalHours = 12;
            opts.AutoRevokeDeparted = true;
        })
        .AddProvisioning()
        .AddNonHumanIdentity()
        .AddApiKeyManagement(opts =>
        {
            opts.MaxKeysPerPrincipal = 5;
            opts.DefaultExpirationDays = 90;
        }));
```

`AddRoles()` registers:
- `IRoleStore` -> `InMemoryRoleStore` (singleton fallback, `TryAddSingleton`)
- `RoleAwareAuthorizationEvaluator` decorator (makes roles authorize)
- `RoleOptions` with `ValidateDataAnnotations`

`AddAccessReviews()` registers:
- `IAccessReviewStore` -> `InMemoryAccessReviewStore` (singleton fallback, `TryAddSingleton`)
- `AccessReviewOptions` with `ValidateDataAnnotations` + `ValidateOnStart`
- `AccessReviewExpiryService` background service for expired campaign processing
- `IAccessReviewNotifier` -> `NullAccessReviewNotifier` fallback (`TryAddSingleton`)

`AddSeparationOfDuties()` registers:
- `ISoDPolicyStore` -> `InMemorySoDPolicyStore` (singleton fallback, `TryAddSingleton`)
- `ISoDEvaluator` -> `DefaultSoDEvaluator` (`TryAddSingleton`)
- `SoDPreventiveMiddleware` as `IDispatchMiddleware` (blocks conflicting grant requests)
- `SoDDetectiveScanService` as `IHostedService` (periodic scanning)
- `SoDPolicyPresenceStartupCheck` as `IHostedService` (see below)
- `SoDOptions` with `ValidateDataAnnotations` + `ValidateOnStart`

:::warning Startup fails if the policy store is empty
`SoDPolicyPresenceStartupCheck` throws at startup when the configured `ISoDPolicyStore` loads **zero
policies**. With no policies, every separation-of-duties check reports "no conflicts" regardless of the
grants held — enforcement is registered and advertised while being completely inert, and that result is
indistinguishable at evaluation time from a genuinely conflict-free request.

The default `InMemorySoDPolicyStore` starts empty, so a host that calls `AddSeparationOfDuties()` without
loading a policy set will not start. Load the policies the deployment expects, or do not register
separation-of-duties enforcement on hosts that genuinely have none.
:::

`AddOrphanedAccessDetection()` registers:
- `IOrphanedAccessDetector` -> `DefaultOrphanedAccessDetector` (`TryAddSingleton`)
- `OrphanedAccessScanService` as `IHostedService` (periodic scanning)
- `OrphanedAccessOptions` with `ValidateDataAnnotations` + `ValidateOnStart`
- **Note:** You must register `IUserStatusProvider` yourself -- no default is provided

`AddProvisioning()` registers:
- `IProvisioningStore` -> `InMemoryProvisioningStore` (`TryAddSingleton`)
- `IProvisioningWorkflowConfiguration` -> `DefaultSingleApproverWorkflow` (`TryAddSingleton`)
- `IGrantRiskAssessor` -> `DefaultGrantRiskAssessor` (`TryAddSingleton`)
- `ProvisioningCompletionService` for grant creation after approval
- `JitAccessExpiryService` background service (when JIT enabled)
- `ProvisioningOptions` and `JitAccessOptions` with `ValidateDataAnnotations` + `ValidateOnStart`

`AddNonHumanIdentity()` registers:
- `IPrincipalTypeProvider` -> `DefaultPrincipalTypeProvider` (returns `Human`, `TryAddSingleton`)

`AddApiKeyManagement()` registers:
- `IApiKeyManager` -> `InMemoryApiKeyManager` (SHA-256 hashed, `TryAddSingleton`)
- `ApiKeyOptions` with `ValidateDataAnnotations` + `ValidateOnStart`

### Role Management

Roles are event-sourced aggregates that map to one or more activity groups. Role assignment reuses the existing Grant infrastructure (`GrantType = "Role"`, `Qualifier = roleName`).

**Role lifecycle (state machine):**

```
Active ←→ Inactive → Deprecated (one-way, audit-only)
```

- **Active:** Can be assigned to users
- **Inactive:** Temporarily suspended, can be reactivated
- **Deprecated:** Permanently archived, exists for audit. Throws `InvalidOperationException` on modification

**`IRoleStore` interface (5 methods + `GetService`):**

| Method | Returns | Purpose |
|--------|---------|---------|
| `GetRoleAsync(roleId, ct)` | `RoleSummary?` | Get by ID |
| `GetRolesAsync(tenantId?, ct)` | `IReadOnlyList<RoleSummary>` | List (optional tenant filter) |
| `SaveRoleAsync(role, ct)` | `Task` | Upsert |
| `DeleteRoleAsync(roleId, ct)` | `bool` | Delete, returns false if not found |
| `GetService(Type)` | `object?` | ISP escape hatch |

### Access Review Campaigns

Access reviews enable organizations to periodically verify that users still need their access -- required for compliance with SOC 2, FedRAMP, SOX, HIPAA, GDPR, and NIST 800-53.

**Campaign lifecycle:**

```
Created → InProgress → Completed (all items decided)
                     → Expired (deadline passed, expiry policy applied)
```

**Scoping reviews:**

Reviews can target all grants or be scoped to a specific role, user, or tenant using `AccessReviewScope`:

```csharp
// Review all grants system-wide
var scope = new AccessReviewScope(AccessReviewScopeType.AllGrants, null);

// Review grants for a specific role
var scope = new AccessReviewScope(AccessReviewScopeType.ByRole, "Admin");

// Review grants for a specific user
var scope = new AccessReviewScope(AccessReviewScopeType.ByUser, "user-123");
```

**Review decisions:**

Each grant item in a campaign receives one of three outcomes:
- `Approved` -- access confirmed
- `Revoked` -- access removed
- `Delegated` -- decision forwarded to another reviewer

**Expiry policies:**

When a campaign expires with unreviewed items, the configured `AccessReviewExpiryPolicy` determines behavior:

| Policy | Behavior |
|--------|----------|
| `DoNothing` | Mark expired for audit, leave access unchanged |
| `RevokeUnreviewed` | Automatically revoke unreviewed items (with retry + exponential backoff) |
| `NotifyAndExtend` | Notify reviewers and extend the deadline |

**Configuration:**

```csharp
services.AddExcaliburA3Core()
    .AddGovernance(g => g
        .AddAccessReviews(opts =>
        {
            opts.DefaultCampaignDuration = TimeSpan.FromDays(14);
            opts.DefaultExpiryPolicy = AccessReviewExpiryPolicy.RevokeUnreviewed;
            opts.ExpiryCheckInterval = TimeSpan.FromMinutes(30);
            opts.MaxRetryAttempts = 3;
            opts.RetryBaseDelay = TimeSpan.FromSeconds(5);
            opts.AutoStartOnCreation = false;
        }));
```

**Query store (`IAccessReviewStore`):**

| Method | Purpose |
|--------|---------|
| `GetCampaignAsync(campaignId, ct)` | Retrieve a campaign summary |
| `SaveCampaignAsync(campaign, ct)` | Save/update a campaign summary |
| `GetCampaignsByStateAsync(state?, ct)` | List campaigns by state |
| `DeleteCampaignAsync(campaignId, ct)` | Remove a campaign |
| `GetService(Type)` | ISP escape hatch for extensions |

:::tip Override the In-Memory Store

`AddAccessReviews()` registers `InMemoryAccessReviewStore` as a fallback via `TryAddSingleton`. Replace it with a persistent implementation by registering your own `IAccessReviewStore` before calling `AddAccessReviews()`, or by replacing the registration afterward.
:::

### Separation of Duties (SoD)

SoD policies prevent users from holding toxic permission combinations -- required for SOC 2, SOX Section 404, FedRAMP AC-5, and NIST 800-53.

**Defining policies:**

Policies reference either role names or activity names. N-way conflicts are supported (any 2 of N items is a violation):

```csharp
var policy = new SoDPolicy(
    PolicyId: "sod-treasury",
    Name: "Treasury Segregation",
    Description: "No user should approve and submit treasury transactions",
    Severity: SoDSeverity.Critical,
    PolicyScope: SoDPolicyScope.Role,
    ConflictingItems: ["TreasuryApprover", "TreasurySubmitter"],
    TenantId: null,  // global policy
    CreatedBy: "compliance-admin");
```

**Enforcement modes:**

| Mode | Description | Enabled By |
|------|-------------|------------|
| **Preventive** | Blocks `AddGrantCommand` if granting access would create a conflict | `SoDOptions.EnablePreventiveEnforcement` (default: true) |
| **Detective** | Background service periodically scans all users for existing violations | `SoDOptions.EnableDetectiveScanning` (default: true) |

**Severity levels:**

| Severity | Behavior |
|----------|----------|
| `Warning` | Logged but allowed (below default enforcement threshold) |
| `Violation` | Blocked by default (matches `MinimumEnforcementSeverity`) |
| `Critical` | Always blocked and escalated |

**Evaluating conflicts programmatically:**

```csharp
ISoDEvaluator evaluator = ...; // injected

// Check a user's current grants for conflicts
var conflicts = await evaluator.EvaluateCurrentAsync("user-123", cancellationToken);

// Check if granting a role would create a conflict
var hypothetical = await evaluator.EvaluateHypotheticalAsync(
    "user-123", "TreasuryApprover", cancellationToken);
```

**Policy store (`ISoDPolicyStore`):**

| Method | Purpose |
|--------|---------|
| `GetPolicyAsync(policyId, ct)` | Retrieve a policy |
| `GetAllPoliciesAsync(ct)` | List all policies |
| `SavePolicyAsync(policy, ct)` | Save/update a policy |
| `DeletePolicyAsync(policyId, ct)` | Remove a policy |
| `GetService(Type)` | ISP escape hatch |

**Configuration:**

```csharp
services.AddExcaliburA3Core()
    .AddGovernance(g => g
        .AddSeparationOfDuties(opts =>
        {
            opts.EnablePreventiveEnforcement = true;
            opts.EnableDetectiveScanning = true;
            opts.MinimumEnforcementSeverity = SoDSeverity.Violation;
            opts.DetectiveScanInterval = TimeSpan.FromHours(24);
        }));
```

:::tip Override SoD Stores

Like access review stores, `AddSeparationOfDuties()` registers `InMemorySoDPolicyStore` as a fallback. Override with your persistent implementation via `TryAddSingleton` replacement.
:::

### Orphaned Access Detection

Detects grants held by users who are no longer active -- required for FedRAMP AC-2, SOC 2, and NIST 800-53.

**Setup:**

```csharp
// You MUST register your own IUserStatusProvider -- no default is provided
services.AddSingleton<IUserStatusProvider, MyHrSystemStatusProvider>();

services.AddExcaliburA3Core()
    .AddGovernance(g => g
        .AddOrphanedAccessDetection(opts =>
        {
            opts.ScanIntervalHours = 12;
            opts.InactiveGracePeriodDays = 30;
            opts.AutoRevokeDeparted = true;
            opts.AutoRevokeAfterGracePeriod = false;
        }));
```

**How it works:**

1. Background service scans all grants at the configured interval
2. For each user, calls `IUserStatusProvider.GetStatusAsync` to check their status
3. Maps user status to a recommended action:

| User Status | Recommended Action |
|-------------|-------------------|
| `Active` | Skip (no action) |
| `Inactive` (within grace period) | `Flag` for review |
| `Inactive` (past grace period) | `Revoke` (if `AutoRevokeAfterGracePeriod` enabled) |
| `Departed` | `Revoke` (if `AutoRevokeDeparted` enabled) |
| `Unknown` or provider error | `Investigate` |

4. Returns an `OrphanedAccessReport` with all findings

:::warning IUserStatusProvider Required

Unlike other governance features, orphaned access detection requires you to provide an `IUserStatusProvider` implementation that connects to your identity/HR system. No in-memory fallback exists because the detector needs real user status data to function.
:::

### Provisioning Workflows (Phase 3 Foundation)

Approval-based workflows for access requests with risk scoring.

**Provisioning request lifecycle:**

```
Pending → InReview → Approved → Provisioned
                   → Denied      → Failed
```

**Key concepts:**

- `ProvisioningRequest` -- event-sourced aggregate managing the approval lifecycle
- `IProvisioningWorkflowConfiguration` -- determines which approval steps apply based on scope and risk
- `IGrantRiskAssessor` -- returns a risk score (0-100) for grant requests; default returns 0
- `IProvisioningStore` -- read-model store for request summaries (4 methods + `GetService`)
- `ApprovalStep` / `ApprovalStepTemplate` -- define who must approve and under what conditions

**Setup:**

```csharp
services.AddExcaliburA3Core()
    .AddGovernance(g => g
        .AddProvisioning());
```

**JIT (Just-In-Time) access:**

Enable temporary role elevation with automatic revocation:

```csharp
services.AddExcaliburA3Core()
    .AddGovernance(g => g
        .AddProvisioning());  // JIT configured via ProvisioningOptions.EnableJitAccess
```

JIT grants have a configurable duration (default: 4 hours, max: 24 hours). A background service (`JitAccessExpiryService`) automatically revokes expired JIT grants.

### Non-Human Identity Governance

Classify and govern service accounts, bots, and API keys alongside human identities.

**Principal classification:**

```csharp
services.AddExcaliburA3Core()
    .AddGovernance(g => g
        .AddNonHumanIdentity());
```

`PrincipalType` classifies identities as `Human`, `ServiceAccount`, `Bot`, or `ApiKey`. The default `IPrincipalTypeProvider` returns `Human` -- override for your identity system.

**API key management:**

```csharp
services.AddExcaliburA3Core()
    .AddGovernance(g => g
        .AddNonHumanIdentity()
        .AddApiKeyManagement(opts =>
        {
            opts.MaxKeysPerPrincipal = 5;
            opts.DefaultExpirationDays = 90;
            opts.MinimumExpirationDays = 1;
        }));
```

`IApiKeyManager` provides full API key lifecycle:

| Method | Purpose |
|--------|---------|
| `CreateKeyAsync(request, ct)` | Create key (plaintext returned once, SHA-256 stored) |
| `RevokeKeyAsync(keyId, ct)` | Revoke a key |
| `ValidateKeyAsync(apiKey, ct)` | Validate plaintext key |
| `GetKeysByPrincipalAsync(principalId, ct)` | List active keys for a principal |
| `GetService(Type)` | ISP escape hatch |

:::tip Security Properties

API keys are stored as SHA-256 hashes -- plaintext is never persisted. The plaintext key is returned exactly once at creation. Mandatory expiry is enforced, and the number of active keys per principal is bounded.
:::

### Entitlement Reporting

Generate compliance-ready snapshots of who has access to what -- required for SOC 2, FedRAMP, SOX, HIPAA, and NIST 800-53 audits.

**Setup:**

```csharp
services.AddExcaliburA3Core()
    .AddGovernance(g => g
        .AddEntitlementReporting());
```

**Generating reports:**

```csharp
IEntitlementReportProvider provider = ...; // injected

// User-scoped snapshot
var userSnapshot = await provider.GenerateUserSnapshotAsync("user-123", ct);

// Tenant-scoped snapshot
var tenantSnapshot = await provider.GenerateTenantSnapshotAsync("tenant-abc", ct);

// Specialized reports
var orphaned = await provider.GenerateReportAsync(
    EntitlementReportType.OrphanedGrants, tenantId: null, ct);
var expiring = await provider.GenerateReportAsync(
    EntitlementReportType.ExpiringGrants, tenantId: "tenant-abc", ct);
var sodViolations = await provider.GenerateReportAsync(
    EntitlementReportType.SoDViolations, tenantId: null, ct);
```

**Report types:**

| Type | Description |
|------|------------|
| `UserEntitlements` | All entitlements for a specific user |
| `TenantEntitlements` | All entitlements within a tenant |
| `OrphanedGrants` | Grants held by inactive/departed/unknown principals |
| `ExpiringGrants` | Grants expiring within a configurable window |
| `SoDViolations` | Grants violating separation-of-duties policies |
| `UnreviewedGrants` | Grants never reviewed in an access review campaign |

**Formatting reports:**

```csharp
IReportFormatter formatter = ...; // injected (JsonReportFormatter by default)

var bytes = await formatter.FormatAsync(snapshot, ct);
// formatter.ContentType == "application/json"
```

The built-in `JsonReportFormatter` uses `System.Text.Json` source generation for AOT safety. Implement `IReportFormatter` for custom formats (CSV, PDF, etc.).

:::tip Graceful Degradation

The entitlement report provider aggregates data from all governance subsystems. If an optional subsystem (e.g., orphaned access detection, SoD evaluator) is not registered, reports that need it return empty entries with a warning log -- they do not throw.
:::

## Package Comparison

| Capability | `Excalibur.A3.Core` | `Excalibur.A3` | `A3.Governance` |
|------------|:-------------------:|:--------------:|:---------------:|
| Grant CRUD (`IGrantStore`) | Yes | Yes | Yes (via A3.Core) |
| Activity group management (`IActivityGroupStore`) | Yes | Yes | Yes (via A3.Core) |
| In-memory stores (dev/test/standalone) | Yes | Yes | Yes |
| ISP sub-interfaces (`IGrantQueryStore`, `IActivityGroupGrantStore`) | Yes | Yes | Yes |
| Atomic grant replacement (`IActivityGroupGrantReplacement`) | Yes | Yes | Yes (via A3.Core) |
| `IA3Builder` with `UseGrantStore<T>()` / `UseActivityGroupStore<T>()` | Yes | Yes | Yes |
| Role management (`IRoleStore`, `AddRoles()`) | -- | -- | Yes |
| Access review campaigns (`IAccessReviewStore`, `AddAccessReviews()`) | -- | -- | Yes |
| Separation of duties (`ISoDEvaluator`, `AddSeparationOfDuties()`) | -- | -- | Yes |
| Orphaned access detection (`IOrphanedAccessDetector`, `AddOrphanedAccessDetection()`) | -- | -- | Yes |
| Provisioning workflows (`IProvisioningStore`, `AddProvisioning()`) | -- | -- | Yes |
| JIT access (temporary elevation with auto-revoke) | -- | -- | Yes |
| Non-human identity (`IPrincipalTypeProvider`, `AddNonHumanIdentity()`) | -- | -- | Yes |
| API key management (`IApiKeyManager`, `AddApiKeyManagement()`) | -- | -- | Yes |
| Entitlement reporting (`IEntitlementReportProvider`, `AddEntitlementReporting()`) | -- | -- | Yes |
| CQRS commands (`AddGrantCommand`, `RevokeGrantCommand`) | -- | Yes | -- |
| Dispatch pipeline middleware (auth, audit) | -- | Yes | -- |
| Authentication HTTP services (`IAuthenticationTokenProvider`) | -- | Yes | -- |
| Event-sourced Grant aggregate | -- | Yes | -- |
| Audit message publishing | -- | Yes | -- |
| NuGet transitive dependencies | 3 packages | 8+ packages | 4 packages |

### Dependency Graphs

```
Standalone (lightweight):
  Your App → A3.Core → A3.Abstractions + Domain + Dispatch.Abstractions

Full stack (unchanged):
  Your App → A3 → A3.Core + Application + EventSourcing + Dispatch + ...

Governance (lightweight):
  Your App → A3.Governance → A3.Governance.Abstractions + A3.Core
```

## External Policy Engines

For organizations using centralized policy engines, A3 supports delegation to OPA or Cedar via HTTP adapters.

### Open Policy Agent (OPA)

```xml
<PackageReference Include="Excalibur.A3.Policy.Opa" />
```

```csharp
services.AddExcaliburA3(a3 =>
{
    a3.UseOpaPolicy(opa =>
    {
        opa.BaseUrl = "http://localhost:8181";
        opa.PolicyPath = "/v1/data/excalibur/authz";
    });
});
```

The OPA adapter sends authorization requests as JSON to your OPA server and maps the response back to A3's grant model. Uses `IHttpClientFactory` for connection management.

### Cedar (AWS Verified Permissions)

```xml
<PackageReference Include="Excalibur.A3.Policy.Cedar" />
```

```csharp
services.AddExcaliburA3(a3 =>
{
    a3.UseCedarPolicy(cedar =>
    {
        cedar.Mode = CedarMode.AwsVerifiedPermissions;
        cedar.BaseUrl = "https://verifiedpermissions.us-east-1.amazonaws.com";
        cedar.PolicyStoreId = "ps-example123";
    });
});
```

Cedar supports two modes:
- **`CedarMode.Local`** -- Evaluate policies locally using a Cedar engine
- **`CedarMode.AwsVerifiedPermissions`** -- Delegate to AWS Verified Permissions via HTTP

:::caution One evaluator at a time

`UseOpaPolicy()` and `UseCedarPolicy()` both register `IAuthorizationEvaluator`. Calling both replaces the previous registration -- the **last call wins**. Choose one policy engine per application.
:::

## What's Next

- [Encryption Architecture](encryption-architecture.md) - Data protection at rest and in transit
- [Audit Logging](audit-logging.md) - Detailed audit logging patterns
- [Message Context](../core-concepts/message-context.md) - Correlation and tenant propagation

## See Also

- [Audit Logging](./audit-logging.md) — Hash-chained, tamper-evident audit trails with SIEM integration
- [Encryption Providers](./encryption-providers.md) — Available encryption providers and configuration options
- [Custom Middleware](../middleware/custom.md) — Building custom middleware for the Dispatch pipeline
- [Pipeline Overview](../pipeline/index.md) — Understanding the Dispatch pipeline architecture and execution flow
