# Dispatch ↔ Excalibur Boundary Guide

Dispatch and Excalibur live in the same repository but they are **two different frameworks** that solve different problems:

- **Dispatch** owns the *messaging pipeline* – handlers, middleware, transports, context propagation, and the thin hosting bridge needed to expose `IDispatcher`.
- **Excalibur** layers a *CQRS + hosting platform* on top of Dispatch – aggregates, event stores, sagas, compliance services, production-ready hosting templates, and long-running orchestration.

This document describes where capabilities belong between the two frameworks.

---

## Two Frameworks, One Upgrade Path

| Layer | Responsibilities | Primary Packages |
|-------|------------------|------------------|
| **Dispatch (Messaging Core)** | Message contracts, handler interfaces, middleware, transports, diagnostics hooks, minimal ASP.NET Core bridge | `Dispatch`, `Excalibur.Dispatch.Abstractions`, `Excalibur.Dispatch.Hosting.AspNetCore`, `Excalibur.Dispatch.Transport.*`, `Excalibur.Dispatch.Observability` |
| **Excalibur (CQRS + Hosting)** | Aggregates, repositories, event stores, sagas, leader election, compliance, opinionated hosting for ASP.NET Core/serverless | `Excalibur.Domain`, `Excalibur.EventSourcing.*`, `Excalibur.Application`, `Excalibur.Hosting.*`, `Excalibur.Compliance.*`, `Excalibur.LeaderElection.*` |

**Rule:** Excalibur may reference Dispatch packages, but Dispatch **must never** reference Excalibur.

---

## Capability Ownership

**Authoritative source:** `eng/governance/framework-governance.json` is the single source of truth for capability ownership, package naming policy, critical test mapping, and sample fitness classification.
**Generated ownership table:** `docs/architecture/capability-ownership-matrix.md`.
**Migration reference:** `docs/architecture/capability-migration-map.md`.

| Capability | Dispatch Owner (NuGet) | Excalibur Owner (NuGet) | Notes |
|------------|------------------------|-------------------------|-------|
| Message contracts (`IDispatchAction/Event/Document`) | `Excalibur.Dispatch.Abstractions` | N/A | All handlers must implement Dispatch interfaces. |
| Middleware + pipeline stages | `Dispatch`, `Excalibur.Dispatch.Middleware.*` | N/A | Custom middleware lives in Dispatch to stay host-agnostic. |
| Minimal hosting bridge (ASP.NET Core) | `Excalibur.Dispatch.Hosting.AspNetCore` | N/A | Provides only endpoint/DI helpers – no OpenAPI or compliance logic. |
| Rich hosting experiences (ASP.NET Core, Azure/AWS/GCP Functions, Web hooks) | — | `Excalibur.Hosting.*` | Ship multilingual host templates, diagnostics, OpenAPI, API versioning, etc. |
| Aggregates, repositories, sagas | — | `Excalibur.Domain`, `Excalibur.EventSourcing.*`, `Excalibur.Saga.*` | All CQRS state management lives here. |
| Event stores + serialization helpers | — | `Excalibur.EventSourcing.*` | Dispatch exposes serialization primitives (e.g., `IEventSerializer`, `EventTypeNameHelper`). |
| Compliance (audit logging, key escrow, masking) | Minimal hooks only | `Excalibur.Compliance.*` | Dispatch exposes interfaces; Excalibur ships providers. |
| Leader election + coordination | — | `Excalibur.LeaderElection.*` | Dispatch samples can reference these packages, but the implementations stay in Excalibur. |
| Samples | `samples/01-getting-started/DispatchOnly` | `samples/01-getting-started/EventSourcingIntro` | Use both to explain upgrade path. |

---

## API Surface Acceptance Criteria

A new public API is accepted in Dispatch only when all are true:

1. It is usable without CQRS/domain persistence concerns.
2. It does not require Excalibur package references.
3. It can be validated by Dispatch-level tests/conformance suites.
4. It does not duplicate an existing Excalibur orchestration API.

A new public API is accepted in Excalibur when any are true:

1. It is opinionated toward aggregates, event sourcing, outbox, saga, or host composition.
2. It depends on domain persistence or wrapper-level lifecycle orchestration.
3. It intentionally composes Dispatch primitives into higher-level defaults.

All ownership or API-surface changes require:

- updates to `framework-governance.json`,
- architecture doc updates (this file + migration map),
- passing governance and boundary CI gates.

---

## Hosting Experiences

### Dispatch-Only Hosting (Minimal Bridge)

Use when you just need MediatR-style message dispatching and want to own the rest of the stack:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDispatch(typeof(Program).Assembly);

var app = builder.Build();
app.MapDispatchEndpoints();           // thin router from Excalibur.Dispatch.Hosting.AspNetCore
app.MapPost("/orders", async (CreateOrder command, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(command, ct);
    return result.ToHttpResult();
});

app.Run();
```

The `Excalibur.Dispatch.Hosting.AspNetCore` package intentionally stops at routing + DI helpers. If you need health checks, OpenAPI, API versioning, telemetry, or CQRS defaults, switch to Excalibur.

#### Hosting Bridge Guard Rails

The Dispatch hosting bridge is protected by automated boundary tests that enforce the minimal surface. Adding new capabilities requires architectural review.

**Allowed Surface (7 public types only):**

| Type | Purpose |
|------|---------|
| `WebApplicationBuilderExtensions` | `AddDispatch()` DI extensions |
| `EndpointRouteBuilderExtensions` | `DispatchPost/Get/Put/DeleteAction()` endpoint helpers |
| `RouteMessageHandlerFactory` | Handler creation glue |
| `HttpContextExtensions` | HTTP context helpers |
| `MessageResultExtensions` | `IMessageResult → IResult` conversion |
| `ControllerBaseExtensions` | Controller helpers |
| `DispatcherWebExtensions` | Dispatcher web helpers |

**Forbidden in Excalibur.Dispatch.Hosting.AspNetCore:**

| Category | Forbidden Patterns | Rationale |
|----------|-------------------|-----------|
| OpenAPI/Swagger | `Swashbuckle.*`, `NSwag.*`, `Microsoft.OpenApi.*` | Moves to `Excalibur.Hosting` |
| API Versioning | `Asp.Versioning.*` | Moves to `Excalibur.Hosting` |
| Health Checks | `Microsoft.Extensions.Diagnostics.HealthChecks.*` | Moves to `Excalibur.Hosting` |
| Telemetry | `OpenTelemetry.*` middleware | Moves to `Excalibur.Hosting` |
| Compliance | `Excalibur.Compliance.*`, `Excalibur.Compliance.*` | Moves to `Excalibur.Compliance` |
| CQRS | Any `Excalibur.*` namespace | Wrapper-only features |
| Key Management | `Azure.Security.KeyVault.*` | Infrastructure concern |

**Test Enforcement:**

The boundary is enforced by reflection-based tests in `tests\unit\Excalibur.Dispatch.Hosting.Tests\AspNetCore\HostingBridgeBoundaryShould.cs`:

```bash
# Run hosting boundary tests
dotnet test --filter "Category=Unit&Component=Hosting"
```

These tests will fail if:
- Any forbidden assembly is referenced
- Any type outside the 7 allowed types is exposed
- The public type count changes (requires architectural review)

### Excalibur Hosting (Full Experience)

Use when you want aggregates, event stores, leader election, and opinionated DI glue:

```csharp
builder.Services
    .AddDispatch(typeof(Program).Assembly)
    .AddInMemoryEventSourcing()
    .AddSqlServerOutbox(configuration.GetConnectionString("Default")!);
```

`AddDispatch()` registers the messaging primitives, while the event sourcing and outbox extension methods add the appropriate providers.

---

## Package Selection Matrix

| Scenario | Recommended Packages | Notes |
|----------|---------------------|-------|
| MediatR replacement / vanilla API | `Dispatch`, `Excalibur.Dispatch.Abstractions`, *(optional)* `Excalibur.Dispatch.Hosting.AspNetCore` | Keep footprint minimal; build your own persistence & hosting. |
| Dispatch + custom transports | Above + `Excalibur.Dispatch.Transport.*` | Mix transports without pulling Excalibur. |
| CQRS read/write separation, aggregates, event sourcing | Dispatch packages + `Excalibur.Domain`, `Excalibur.EventSourcing`, provider-specific stores | Gain aggregates, snapshots, event stores, serializers (`EventTypeNameHelper`). |
| Enterprise hosting (OpenAPI, health, compliance) | Dispatch packages + `Excalibur.Hosting.*`, `Excalibur.Compliance.*`, `Excalibur.LeaderElection.*` | Use Excalibur wrappers for a batteries-included platform. |
| Serverless functions | Dispatch packages for local handlers, Excalibur hosting package for your platform (Azure Functions/Lambda/GCF). | Dispatch samples illustrate manual wiring; Excalibur provides templates. |

---

## Migration Path

1. **Start with Dispatch** – install `Dispatch` + `Excalibur.Dispatch.Abstractions`, wire handlers, and adopt middleware pipeline.
2. **Add transports/observability** – bring in the specific Dispatch transport or diagnostics packages you need.
3. **Adopt Excalibur layer-by-layer**:
   - Hosting: `Excalibur.Hosting.Web` (ASP.NET Core) or `Excalibur.Hosting.AzureFunctions`, etc.
   - Domain: `Excalibur.Domain`, `Excalibur.EventSourcing.*`, `Excalibur.Saga.*`.
   - Compliance & operations: `Excalibur.Compliance.*`, `Excalibur.LeaderElection.*`.
4. **Use the samples** – `samples/01-getting-started/DispatchOnly` shows Dispatch-only usage; `samples/01-getting-started/EventSourcingIntro` demonstrates the full Excalibur stack using the same commands/events.

Upgrading is incremental: every Excalibur package depends on Dispatch, so you never re-write handlers when switching hosts.

---

## References

- [DispatchOnly Sample](../../samples/01-getting-started/DispatchOnly/README.md)
- [EventSourcingIntro Sample](../../samples/01-getting-started/EventSourcingIntro/README.md)
- [README.md](../../README.md) – dispatch vs Excalibur entry points
- [docs/dispatch/\*](../dispatch/) – dispatcher, middleware, routing, and extensibility guides
- [docs-site](../../docs-site/docs/intro.md) – public-facing documentation with consumer quick starts

Maintain this document whenever capabilities move between frameworks. If a new feature does not clearly belong in the Dispatch column, escalate to SoftwareArchitect before merging.


