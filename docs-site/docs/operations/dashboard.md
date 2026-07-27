---
sidebar_position: 12
title: Operational Dashboard
description: Free, OSS, read-only-by-default operational dashboard for live outbox, dead-letter, inbox, saga, projection/CDC-lag, and leader-election state.
---

# Operational Dashboard

`Excalibur.Operations.Dashboard` is a **free, open-source, read-only-by-default** operational dashboard for the reliability subsystems Excalibur already instruments. Add one package, map it onto your ASP.NET Core app, and see live **outbox**, **dead-letter queue**, **inbox**, **saga**, **projection/CDC-lag**, and **leader-election** state — across every storage provider you have configured — with no paid license and no bespoke admin UI to build.

It is a thin aggregation layer over the framework's existing admin read-models and OpenTelemetry meters:

- **Point-in-time state** (depths, failed counts, oldest-message age, saga instances) comes from the per-subsystem admin read-models.
- **Rates** (throughput) are derived from the existing OpenTelemetry meters.

The dashboard ships two surfaces under one route prefix: a **minimal-API read API** (`{prefix}/api/...`) and an **embedded single-page app** (served at `{prefix}`). The UI renders only the panels backed by a configured subsystem, via a capability-discovery endpoint.

## Before You Start

- **.NET 10.0** and an ASP.NET Core host (the dashboard maps onto `IEndpointRouteBuilder`).
- One or more reliability subsystems configured (outbox, inbox, DLQ, sagas, leader election). Absent subsystems **fail open** — their panel reports "not configured" rather than erroring.
- Familiarity with [configuration](../configuration/index.md) and [observability](../observability/index.md).

## Installation

```bash
dotnet add package Excalibur.Operations.Dashboard
```

The base package includes the embedded SPA (it depends on `Excalibur.Operations.Dashboard.Spa`). For the projection/CDC-lag panel, also add the event-sourcing add-on:

```bash
dotnet add package Excalibur.Operations.Dashboard.EventSourcing
```

| Package | Purpose |
|---------|---------|
| `Excalibur.Operations.Dashboard` | Read API + embedded SPA for outbox, DLQ, inbox, saga, leader, throughput |
| `Excalibur.Operations.Dashboard.Spa` | The embedded Svelte SPA assets (pulled in transitively by the base package) |
| `Excalibur.Operations.Dashboard.EventSourcing` | Opt-in projection/CDC-lag panel (keeps the base package free of an event-sourcing dependency) |

## Quick Start

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ... register your subsystems (AddOutbox, AddSagas, leader election, etc.) ...

builder.Services.AddDashboard();

var app = builder.Build();

app.MapDashboard();   // read API at /dashboard/api, SPA at /dashboard

app.Run();
```

Browse to `/dashboard`. The SPA discovers which subsystems are configured and renders only their panels.

### Adding the projection/CDC-lag panel

The projection-lag panel lives in a separate package so the base dashboard has no event-sourcing dependency. Register it after `AddDashboard()`:

```csharp
builder.Services.AddDashboard();
builder.Services.AddProjectionLagDashboard();   // requires Excalibur.Operations.Dashboard.EventSourcing
```

The module resolves the projection-lag read-model as an optional service, so a host without event sourcing configured still fails open (reports not-configured).

## Configuration

`AddDashboard(Action<DashboardOptions>)` configures the dashboard. Options are validated at startup (`ValidateOnStart`), so a bad route prefix fails fast with `OptionsValidationException`.

```csharp
builder.Services.AddDashboard(options =>
{
    options.RoutePrefix = "/ops";            // default: "/dashboard"
    options.DefaultPageSize = 50;            // default: 50   (range 1–1000)
    options.MaxPageSize = 500;               // default: 500  (range 1–10000; larger requests are clamped)
    options.EnableMutatingActions = false;   // default: false
    options.MutatingActionsPolicy = null;    // default: null (any authenticated user)
});
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `RoutePrefix` | `string` | `/dashboard` | Path prefix for the read API (`{prefix}/api`) and SPA (`{prefix}`). Must start with `/` and use URL-path-safe characters. |
| `DefaultPageSize` | `int` | `50` | Page size used by paginated read endpoints when the caller does not specify one. |
| `MaxPageSize` | `int` | `500` | Upper bound for a page; larger requests are clamped. |
| `EnableMutatingActions` | `bool` | `false` | When `false`, mutating endpoints are **not mapped at all** (404). When `true`, they are mapped and require authorization. |
| `MutatingActionsPolicy` | `string?` | `null` | Authorization policy applied to mutating endpoints when enabled; `null` requires any authenticated user. |

## Read API

All read endpoints are mapped under `{RoutePrefix}/api`. Each subsystem module fails open: when its underlying store is not configured, it returns an empty/not-configured payload rather than a 500.

| Endpoint | Subsystem | Description |
|----------|-----------|-------------|
| `GET {prefix}/api/` | — | **Capability discovery.** Returns the configured subsystem keys and whether mutating actions are enabled. |
| `GET {prefix}/api/outbox` | outbox | Outbox statistics (depth, failed, scheduled, oldest-message age). |
| `GET {prefix}/api/dlq` | dlq | Dead-letter queue counts. |
| `GET {prefix}/api/dlq/entries?skip=&limit=` | dlq | Paginated dead-letter entries. |
| `GET {prefix}/api/inbox` | inbox | Inbox / deduplication statistics. |
| `GET {prefix}/api/leader` | leader | Leader-election snapshot (is-leader, current leader id, candidate id, fencing token). |
| `GET {prefix}/api/saga` | saga | Saga store statistics. |
| `GET {prefix}/api/saga/instances` | saga | Paginated / filtered saga instances. |
| `GET {prefix}/api/saga/stuck` | saga | Sagas that have not progressed within the stuck threshold. |
| `GET {prefix}/api/saga/{id}` | saga | A single saga instance by id. |
| `GET {prefix}/api/{subsystem}/throughput` | throughput | Rate for a subsystem, derived from OpenTelemetry meters. |
| `GET {prefix}/api/projections/lag` | projections | Per-subscription projection/CDC lag (requires `AddProjectionLagDashboard()`). |

### Capability discovery

The SPA (or your own client) calls `GET {prefix}/api/` first to learn which panels to render:

```json
{
  "subsystems": ["dlq", "inbox", "leader", "outbox", "saga"],
  "mutatingActionsEnabled": false
}
```

`subsystems` lists only the modules registered in DI; `mutatingActionsEnabled` reflects `DashboardOptions.EnableMutatingActions`.

## Security

### The read API is unauthenticated by default

The dashboard is **read-only by default**, and the read endpoints carry **no authorization requirement** out of the box. Some read payloads can be sensitive — dead-letter entries expose exception messages and correlation ids; saga instances expose tenant ids. **If you expose the dashboard where those reads are sensitive, gate them yourself.**

Because the read API and SPA are mapped under one route prefix, the simplest way to require authentication for the whole dashboard is to map it inside a parent route group that requires authorization. ASP.NET Core route-group conventions propagate to the endpoints mapped within them:

```csharp
// Require an authenticated user for the entire dashboard (reads + SPA).
var dashboard = app.MapGroup("").RequireAuthorization();
dashboard.MapDashboard();
```

Or apply a named policy:

```csharp
var dashboard = app.MapGroup("").RequireAuthorization("DashboardReaders");
dashboard.MapDashboard();
```

You can also restrict exposure at the host level (bind the dashboard to an internal-only port, or place it behind a reverse-proxy auth layer).

#### Gating reads with a built-in policy

As an alternative to the parent-route-group approach, set `ReadActionsPolicy` to require an authorization policy on **every read endpoint** — symmetric with `MutatingActionsPolicy` and matching the ASP.NET Core health-checks pattern. Leave it `null` (the default) to keep reads open.

```csharp
builder.Services.AddDashboard(options =>
{
    options.ReadActionsPolicy = "DashboardReaders";
});
```

### Bounded result sizes

List endpoints (dead-letter entries, stuck sagas) clamp their page size so a caller cannot request an unbounded result set. The page size is the requested `limit` clamped to `[1, MaxPageSize]`, falling back to `DefaultPageSize` (default `50`) when no limit is supplied; `MaxPageSize` defaults to `500`. Both are validated at startup (`DefaultPageSize` must not exceed `MaxPageSize`).

```csharp
builder.Services.AddDashboard(options =>
{
    options.DefaultPageSize = 50;
    options.MaxPageSize = 500;
});
```

### Mutating actions are opt-in and auth-gated

State-changing actions — currently **dead-letter replay** — are disabled by default. When `EnableMutatingActions` is `false`, the mutating endpoint group is **not mapped at all**: the routes return `404`, so there is no attack surface to authorize. When you set `EnableMutatingActions = true`:

- the mutating endpoints are mapped, **and**
- the group requires an authenticated, authorized caller (`RequireAuthorization`, using `MutatingActionsPolicy` if supplied).

```csharp
builder.Services.AddDashboard(options =>
{
    options.EnableMutatingActions = true;
    options.MutatingActionsPolicy = "DashboardAdmins";
});
```

Mutating endpoints (mapped only when enabled):

| Endpoint | Description |
|----------|-------------|
| `POST {prefix}/api/dlq/{id}/replay` | Replay a single dead-letter message. |
| `POST {prefix}/api/dlq/replay-batch` | Replay a batch of dead-letter messages. |

Replay is implemented over the existing `IDeadLetterQueue.ReplayAsync` / `IDeadLetterQueueAdmin.ReplayBatchAsync` contracts.

## The embedded single-page app

`MapDashboard()` also serves the SPA (equivalently, call `MapDashboardSpa()` on its own). The UI is a Svelte app served as **embedded static assets** — no external CDN — with a strict Content-Security-Policy, under the configured route prefix. Only `GET` routes are mapped, so serving the UI never exposes a mutating surface. The serving path uses `RequestDelegate` overloads (not reflection-bound handlers), so it stays **trim- and native-AOT-safe**; the read API DTOs serialize through a source-generated `JsonSerializerContext`, so the consumer path carries no `[RequiresUnreferencedCode]`.

If you only want the JSON read API (no UI), map just the API:

```csharp
app.MapDashboardApi();   // read API only, no SPA
```

## Extending with a custom subsystem panel

Subsystem panels are contributed as `IDashboardEndpointModule` implementations. `MapDashboardApi()` discovers and maps every module registered with `TryAddEnumerable` — there is no central endpoint table to edit. Implement the interface and register your module to expose an additional read-only panel:

```csharp
public sealed class MyDashboardModule : IDashboardEndpointModule
{
    public string Subsystem => "my-subsystem";

    public void MapEndpoints(RouteGroupBuilder group, DashboardOptions options)
    {
        group.MapGet("/my-subsystem", (MyStore? store) =>
            store is null ? Results.Json(new { configured = false }) : Results.Json(store.Snapshot()));
    }
}

// Registration
services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IDashboardEndpointModule, MyDashboardModule>());
```

Resolve your store as an **optional** (nullable) service so an absent subsystem fails open rather than throwing.

## See Also

- [Operations](./index.md) — operational guides index
- [Reliability Guarantees](./reliability-guarantees.md) — delivery/ordering/deduplication guarantees by path and provider
- [Observability](../observability/index.md) — the meters the throughput panel derives from
- [Sagas](../sagas/index.md) — saga state surfaced by the saga panel
