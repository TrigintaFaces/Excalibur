# Operational Dashboard Sample

Wire the free, OSS Excalibur **operational dashboard** into any ASP.NET Core host with two calls.

## What it shows

```csharp
builder.Services.AddDashboard();   // validated options + read-only subsystem modules
app.MapDashboard();                // read API + embedded single-page app (SPA)
```

The dashboard surfaces live **outbox**, **dead-letter**, **inbox**, **saga**,
**projection / CDC-lag**, and **leader-election** state across every configured storage
provider, via minimal-API endpoints plus an embedded SPA.

## Endpoints (default route prefix `/dashboard`)

| Endpoint | Purpose |
| --- | --- |
| `GET /dashboard` | Embedded single-page app |
| `GET /dashboard/api` | Capability discovery — which subsystem panels are backed by a configured provider |
| `GET /dashboard/api/...` | Per-subsystem read endpoints (present when the provider is configured) |

## Design notes

- **Read-only by default.** Mutating actions (e.g. dead-letter replay) are **off**; while
  disabled the endpoints do not exist at all (404, zero attack surface), not merely
  auth-gated. Enable them with `EnableMutatingActions = true` — they then require an
  authenticated, authorized caller (`MutatingActionsPolicy`).
- **Fail open.** Each subsystem module resolves its store at request time through an
  optional (nullable) dependency, so an absent subsystem is simply not advertised rather
  than throwing. This sample configures **no** storage provider, so the capability root
  reports zero subsystems — register a provider to light up its panel.
- **Optional read authorization.** Set `ReadActionsPolicy` to require an authorized caller
  on the read API too (symmetric with the mutating group).

## Authorization — two viable options

The dashboard gates its read and mutating endpoint groups with the standard ASP.NET Core
`RequireAuthorization(policyName)` seam. You supply the policy **names** via `DashboardOptions`;
the dashboard doesn't care how the policy is satisfied. Both approaches below are first-class —
pick whichever matches how your app already does authorization (or mix them).

### Option A — standard ASP.NET Core authorization policies (roles / claims / custom)

Use plain ASP.NET Core policies when your app authorizes with roles, claims, or a custom
requirement. No Excalibur-specific types involved.

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("DashboardRead",  p => p.RequireRole("ops", "sre"))
    .AddPolicy("DashboardAdmin", p => p.RequireRole("ops-admin"));

builder.Services.AddDashboard(options =>
{
    options.ReadActionsPolicy     = "DashboardRead";
    options.EnableMutatingActions = true;
    options.MutatingActionsPolicy = "DashboardAdmin";
});
```

### Option B — Excalibur A3 grant-based authorization

Use A3 when your app authorizes with **grants** (activity + resource scoped). The turnkey
`AddGrantAuthorization(...)` helper registers a named policy backed by an A3 grant so you never
hand-build a `GrantsAuthorizationRequirement`.

```csharp
// Register grant-backed policies (activity + resource types) — the turnkey helper.
builder.Services.AddGrantAuthorization("DashboardRead",  activityName: "dashboard.read",   resourceTypes: ["Dashboard"]);
builder.Services.AddGrantAuthorization("DashboardAdmin", activityName: "dashboard.replay", resourceTypes: ["DeadLetter"]);

builder.Services.AddDashboard(options =>
{
    options.ReadActionsPolicy     = "DashboardRead";    // reads require the grant
    options.EnableMutatingActions = true;
    options.MutatingActionsPolicy = "DashboardAdmin";   // replay requires the grant
});
```

> The A3 grant-evaluation services (grant stores, cache, current-user/tenant) plus an
> authentication scheme must be registered for enforcement — `AddGrantAuthorization` wires the
> policy + handler; the caller's grants are evaluated at request time via A3's
> `GrantsAuthorizationHandler` (`policy.IsAuthorized(activity, resourceId)`).

Both options resolve to the same seam: the dashboard calls `RequireAuthorization(policyName)` and
references neither ASP.NET-specific roles nor A3 — it depends only on the authorization abstraction.

> **Granularity note.** Today the dashboard applies one read policy to the whole read group and
> one mutating policy to the whole mutating group (both options). A3 grants are activity+resource
> scoped, so per-subsystem grants (e.g. `dashboard.outbox.read` vs `dashboard.dlq.replay`) are a
> possible future enhancement — tracked separately.

## Run

```bash
dotnet run
```

Then open <http://localhost:5000/dashboard> (or the port shown in the console).
