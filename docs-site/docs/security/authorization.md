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

**What you get:** Grant CRUD, activity group management, `GetService(Type)` ISP access to `IGrantQueryStore` and `IActivityGroupGrantStore`.

**What you do NOT get:** Dispatch pipeline, CQRS commands (`AddGrantCommand`, `RevokeGrantCommand`), authentication HTTP clients, audit middleware, event-sourced Grant aggregate.

### Full-Stack Setup (A3)

Register full A3 services using the builder pattern. `AddExcaliburA3()` internally calls `AddExcaliburA3Core()`, then adds CQRS, Dispatch pipeline, and authentication:

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

For custom store implementations:

```csharp
services.AddExcaliburA3()
    .UseGrantStore<MyGrantStore>()
    .UseActivityGroupStore<MyActivityGroupStore>();
```

The builder also registers Dispatch pipeline integration (`AddDispatchAuthorization()`) automatically.

:::tip Single-Tenant Applications
You do **not** need to configure a tenant to use A3. When you call `AddExcaliburA3()`, it automatically registers `ITenantId` with the default value `"Default"` (via `TenantDefaults.DefaultTenantId`). All tenant-scoped features — grants, authorization policies, audit logging — work transparently.

For **multi-tenant** applications that serve multiple tenants from a single instance, use the factory overload:
```csharp
// Resolve tenant per-request — A3 won't override it (TryAdd semantics)
services.TryAddTenantId(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    return httpContext?.Request.Headers["X-Tenant-ID"].FirstOrDefault()
        ?? TenantDefaults.DefaultTenantId;
});
services.AddExcaliburA3()
    .UsePostgres();
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

## Store Pattern

A3 uses a **store pattern** modeled after ASP.NET Core Identity (`IUserStore<T>`, `IRoleStore<T>`, `IdentityBuilder`). Store interfaces live in `Excalibur.A3.Abstractions` and each database provider implements them in its own package.

### Store Interfaces

| Interface | Methods | Purpose |
|-----------|---------|---------|
| `IGrantStore` | 5 + `GetService(Type)` | Core grant CRUD (get, getAll, save, delete, exists) |
| `IGrantQueryStore` | 2 | ISP sub-interface for advanced queries (matching, find) |
| `IActivityGroupStore` | 4 + `GetService(Type)` | Activity group operations (exists, findAll, deleteAll, create) |
| `IActivityGroupGrantStore` | 4 | Bridging ISP for activity-group grant operations |

Advanced features are accessed via the `GetService(Type)` escape hatch rather than adding optional methods to the core interface:

```csharp
// Access advanced query capabilities from IGrantStore
IGrantStore store = ...;
var queryStore = store.GetService(typeof(IGrantQueryStore)) as IGrantQueryStore;
if (queryStore is not null)
{
    var grants = await queryStore.GetMatchingGrantsAsync(
        userId, tenantId, grantType, qualifier, cancellationToken);
}
```

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
- `SoDOptions` with `ValidateDataAnnotations` + `ValidateOnStart`

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
| Authentication HTTP services (`ITokenValidator`) | -- | Yes | -- |
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

## What's Next

- [Encryption Architecture](encryption-architecture.md) - Data protection at rest and in transit
- [Audit Logging](audit-logging.md) - Detailed audit logging patterns
- [Message Context](../core-concepts/message-context.md) - Correlation and tenant propagation

## See Also

- [Audit Logging](./audit-logging.md) — Hash-chained, tamper-evident audit trails with SIEM integration
- [Encryption Providers](./encryption-providers.md) — Available encryption providers and configuration options
- [Custom Middleware](../middleware/custom.md) — Building custom middleware for the Dispatch pipeline
- [Pipeline Overview](../pipeline/index.md) — Understanding the Dispatch pipeline architecture and execution flow
