# Dispatch ↔ Excalibur Boundary Guide

Dispatch is the standalone messaging foundation. Excalibur composes Dispatch into a CQRS and hosting layer:

- **Dispatch** owns the *messaging pipeline* – handlers, middleware, transports, context propagation, and the thin hosting bridge needed to expose `IDispatcher`.
- **Excalibur** layers a *CQRS + hosting platform* on top of Dispatch – aggregates, event stores, sagas, compliance services, hosting composition, and long-running orchestration.

This contributor guide describes where capabilities belong. Application installation and recipes belong in the consumer-facing `docs-site/` documentation.

---

## Two Frameworks, One Upgrade Path

| Layer | Responsibilities | Primary Packages |
|-------|------------------|------------------|
| **Dispatch (Messaging Core)** | Message contracts, handler interfaces, middleware, transports, diagnostics hooks, minimal ASP.NET Core bridge | `Excalibur.Dispatch`, `Excalibur.Dispatch.Abstractions`, `Excalibur.Dispatch.Hosting.AspNetCore`, `Excalibur.Dispatch.Transport.*`, `Excalibur.Dispatch.Observability` |
| **Excalibur (CQRS + Hosting)** | Aggregates, repositories, event stores, sagas, leader election, compliance, opinionated hosting for ASP.NET Core/serverless | `Excalibur.Domain`, `Excalibur.EventSourcing.*`, `Excalibur.Application`, `Excalibur.Hosting.*`, `Excalibur.Compliance.*`, `Excalibur.LeaderElection.*` |

**Rule:** Excalibur composes Dispatch primitives; Dispatch must not acquire wrapper-feature dependencies such as domain persistence or saga orchestration. Both layers use the `Excalibur` name prefix, so namespaces alone do not establish ownership. The core NuGet package ID is `Excalibur.Dispatch`.

---

## Capability Ownership

**Authoritative source:** `eng/governance/framework-governance.json` is the single source of truth for capability ownership, package naming policy, critical test mapping, and sample fitness classification.
**Generated ownership table:** `docs/architecture/capability-ownership-matrix.md`.
**Migration reference:** `docs/architecture/capability-migration-map.md`.

| Capability | Dispatch Owner (NuGet) | Excalibur Owner (NuGet) | Notes |
|------------|------------------------|-------------------------|-------|
| Message contracts (`IDispatchAction/Event/Document`) | `Excalibur.Dispatch.Abstractions` | N/A | Messages and handlers use Dispatch contracts. |
| Middleware + pipeline stages | `Excalibur.Dispatch`, `Excalibur.Dispatch.Patterns` | N/A | Reusable messaging policies remain independent of domain persistence. |
| Minimal hosting bridge (ASP.NET Core) | `Excalibur.Dispatch.Hosting.AspNetCore` | N/A | Endpoint/DI helpers, request scope, authorization and content negotiation; no OpenAPI or compliance providers. |
| Rich hosting experiences (ASP.NET Core and Azure/AWS/GCP Functions) | — | `Excalibur.Hosting.*` | Opinionated application and platform hosting composition. |
| Aggregates, repositories, sagas | — | `Excalibur.Domain`, `Excalibur.EventSourcing.*`, `Excalibur.Saga.*` | All CQRS state management lives here. |
| Event stores and event persistence | — | `Excalibur.EventSourcing.*` | Messaging serialization primitives remain in Dispatch; event-store composition belongs in Excalibur. |
| Compliance (audit logging, key escrow, masking) | Minimal hooks only | `Excalibur.Compliance.*` | Dispatch exposes interfaces; Excalibur ships providers. |
| Leader election + coordination | — | `Excalibur.LeaderElection.*` | Dispatch samples can reference these packages, but the implementations stay in Excalibur. |
| Samples | `samples/01-getting-started/DispatchOnly` | `samples/01-getting-started/EventSourcingIntro` | Use both to explain upgrade path. |

---

## API Surface Acceptance Criteria

A new public API is accepted in Dispatch only when all are true:

1. It is usable without CQRS/domain persistence concerns.
2. It does not require wrapper-feature package references.
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

This complete `Program.cs` exercises the typed endpoint API with entry-assembly handler discovery. Compile it with references to `Excalibur.Dispatch` and `Excalibur.Dispatch.Hosting.AspNetCore` (project references for repository development). This JIT-hosted echo example demonstrates dispatch, not durable order processing.

```csharp
using DispatchBoundaryExample;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
builder.AddDispatch();

var app = builder.Build();
app.DispatchPostAction<Echo, string>("/echo");
app.Run();

namespace DispatchBoundaryExample
{
    public sealed record Echo(string Text) : IDispatchAction<string>;

    public sealed class EchoHandler : IActionHandler<Echo, string>
    {
        public Task<string> HandleAsync(Echo action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(action.Text);
        }
    }
}
```

Map endpoints explicitly through the HTTP verb helpers in [EndpointRouteBuilderExtensions.cs](../../src/Dispatch/Excalibur.Dispatch.Hosting.AspNetCore/EndpointRouteBuilderExtensions.cs). These reflection-based APIs have trimming/dynamic-code annotations; compiling this example does not establish Native AOT support. `builder.AddDispatch()` discovers entry-assembly handlers when no callback is supplied; a callback takes control of handler registration.

The bridge includes routing, result conversion, request-scope reuse, authorization integration and serializer content negotiation. Applications can compose ordinary ASP.NET Core health checks, OpenAPI or telemetry alongside Dispatch without adopting CQRS. Opinionated Excalibur hosting is a separate composition choice.

#### Hosting Bridge Guard Rails

The Dispatch hosting bridge is protected by automated boundary tests that enforce the minimal surface. Adding new capabilities requires architectural review.

The checked-in [public API baseline](../../src/Dispatch/Excalibur.Dispatch.Hosting.AspNetCore/PublicAPI.Shipped.txt) and boundary allowlist currently specify **14 public types**:

| Type | Purpose |
|------|---------|
| `WebApplicationBuilderExtensions` | `WebApplicationBuilder.AddDispatch()` composition |
| `EndpointRouteBuilderExtensions` | `DispatchPost/Get/Put/DeleteAction()` endpoint helpers |
| `RouteMessageHandlerFactory` | Handler creation glue |
| `HttpContextExtensions` | HTTP context helpers |
| `MessageResultExtensions` | `IMessageResult → IResult` conversion |
| `ControllerBaseExtensions` | Controller helpers |
| `DispatcherWebExtensions` | Dispatcher web helpers |
| `DispatchAspNetCoreAuthorizationExtensions` | Register ASP.NET Core authorization integration |
| `AspNetCoreAuthorizationMiddleware` | Enforce authorization in dispatch |
| `AspNetCoreAuthorizationOptions` | Configure authorization behavior |
| `DispatchContentNegotiationExtensions` | Register serializer formatters |
| `DispatchInputFormatter` | Read negotiated request content |
| `DispatchOutputFormatter` | Write negotiated response content |
| `DispatchAspNetCoreScopeExtensions` | Reuse the active HTTP request scope |

Builder extensions live in `Microsoft.AspNetCore.Builder`; service registration extensions live in `Microsoft.Extensions.DependencyInjection`. Other types use `Excalibur.Dispatch.Hosting.AspNetCore` and its `ContentNegotiation` namespace. Consult the API baseline for complete signatures.

**Forbidden in Excalibur.Dispatch.Hosting.AspNetCore:**

| Category | Forbidden Patterns | Rationale |
|----------|-------------------|-----------|
| OpenAPI/Swagger | `Swashbuckle.*`, `NSwag.*`, `Microsoft.OpenApi.*` | Moves to `Excalibur.Hosting` |
| API Versioning | `Asp.Versioning.*` | Moves to `Excalibur.Hosting` |
| Health Checks | `Microsoft.Extensions.Diagnostics.HealthChecks.*` | Moves to `Excalibur.Hosting` |
| Telemetry | `OpenTelemetry.*` middleware | Moves to `Excalibur.Hosting` |
| Compliance | Compliance implementation namespaces | Owned by `Excalibur.Compliance.*` |
| CQRS | Domain, event-store, data, saga and application dependencies | Wrapper features; the shared `Excalibur` prefix alone is not forbidden |
| Key Management | `Azure.Security.KeyVault.*` | Infrastructure concern |

**Test Enforcement:**

The boundary is enforced by [HostingBridgeBoundaryShould.cs](../../tests/unit/Excalibur.Dispatch.Hosting.Tests/AspNetCore/HostingBridgeBoundaryShould.cs). These restrictions apply to the bridge assembly, not to applications consuming it:

```bash
# From the repository root, build and run the actual hosting boundary population.
pwsh ./eng/build.ps1 -Test -Project tests/unit/Excalibur.Dispatch.Hosting.Tests/Excalibur.Dispatch.Hosting.Tests.csproj -TestFilter "FullyQualifiedName~HostingBridgeBoundaryShould" -ResultsPrefix hosting-boundary
```

These tests check specific referenced-assembly name patterns, namespaces of types
defined in the bridge, the simple-name public-type allowlist, required types and the
exported-type count. They are evidence for those checks, not exhaustive enforcement
of every architectural rule in the table. For example, `HealthChecks` is a defined-type
namespace restriction; the dependency allowlist accepts `Microsoft.Extensions.*` and
does not independently prohibit every health-check assembly reference. Architectural
review must also inspect dependency changes. Public-surface changes require review
and corresponding API-baseline/test updates.

### Excalibur Hosting (Full Experience)

Use when you want aggregates, event stores, leader election, and opinionated DI glue:

Use the [EventSourcingIntro composition](../../samples/01-getting-started/EventSourcingIntro/README.md) as a source reference when adding domain and provider registrations. Messaging registration and event-store/outbox registration have distinct contracts; do not assume their builder return types can be chained interchangeably.

---

## Package Selection Matrix

| Scenario | Recommended Packages | Notes |
|----------|---------------------|-------|
| MediatR replacement / vanilla API | `Excalibur.Dispatch`, `Excalibur.Dispatch.Abstractions`, *(optional)* `Excalibur.Dispatch.Hosting.AspNetCore` | Keep footprint minimal; build your own persistence & hosting. |
| Dispatch + custom transports | Above + `Excalibur.Dispatch.Transport.*` | Mix transports without pulling Excalibur. |
| CQRS read/write separation, aggregates, event sourcing | Dispatch packages + `Excalibur.Domain`, `Excalibur.EventSourcing`, provider-specific stores | Gain aggregates, snapshots, event stores, serializers (`MessageNameHelper`). |
| Enterprise hosting (OpenAPI, health, compliance) | Dispatch packages + `Excalibur.Hosting.*`, `Excalibur.Compliance.*`, `Excalibur.LeaderElection.*` | Use Excalibur wrappers for a batteries-included platform. |
| Serverless functions | Dispatch packages for local handlers, Excalibur hosting package for your platform (Azure Functions/Lambda/GCF). | Dispatch samples illustrate manual wiring; Excalibur provides templates. |

---

## Migration Path

1. **Start with Dispatch** – install `Excalibur.Dispatch` + `Excalibur.Dispatch.Abstractions`, wire handlers, and adopt middleware pipeline.
2. **Add transports/observability** – bring in the specific Dispatch transport or diagnostics packages you need.
3. **Adopt Excalibur layer-by-layer**:
   - Hosting: `Excalibur.Hosting.Web` (ASP.NET Core) or `Excalibur.Hosting.AzureFunctions`, etc.
   - Domain: `Excalibur.Domain`, `Excalibur.EventSourcing.*`, `Excalibur.Saga.*`.
   - Compliance & operations: `Excalibur.Compliance.*`, `Excalibur.LeaderElection.*`.
4. **Use the samples** – `samples/01-getting-started/DispatchOnly` shows messaging usage; `samples/01-getting-started/EventSourcingIntro` adds an aggregate and in-memory event-store composition. They use related order scenarios with separate CLR message/domain-event contracts, not a shared set of commands and events or the entire Excalibur platform.

Compose Excalibur capabilities incrementally, preserving compatible message and handler contracts. Dependencies and migration requirements differ by package; there is no universal promise that every Excalibur package depends on Dispatch or that host changes require no adaptation.

For event-store work, review the [Global Stream Ordering contract](../../src/Excalibur/Excalibur.EventSourcing/ARCHITECTURE.md), actual allocator/feed implementations and [benchmark evidence](../../benchmarks/BENCHMARKS.md). Transactional ordering, subscriber checkpoints, schema migration, recovery and measured throughput are separate obligations. Proposed watermark or partitioned profiles do not silently weaken supported ordering guarantees.

---

## References

- [DispatchOnly Sample](../../samples/01-getting-started/DispatchOnly/README.md)
- [EventSourcingIntro Sample](../../samples/01-getting-started/EventSourcingIntro/README.md)
- [README.md](../../README.md) – dispatch vs Excalibur entry points
- [docs/dispatch/\*](../dispatch/) – dispatcher, middleware, routing, and extensibility guides
- [docs-site](../../docs-site/docs/intro.md) – public-facing documentation with consumer quick starts

Maintain this document whenever capabilities move between frameworks. If a new feature does not clearly belong in the Dispatch column, escalate to SoftwareArchitect before merging.


