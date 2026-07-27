# Excalibur.Operations.Dashboard

A free, OSS, **read-only-by-default** operational dashboard for the Excalibur framework. One opt-in
package surfaces the live state of every reliability subsystem the framework already instruments:
outbox, dead-letter queue, inbox, sagas, projection / CDC lag, and leader election — plus an embedded
single-page app. Mutating actions (dead-letter replay) are **opt-in and auth-gated**. No paywall, no
license key.

## Getting started

```csharp
builder.Services.AddDashboard(options =>
{
    options.RoutePrefix = "/dashboard";
    // Read-only by default. Opt in to mutating actions (dead-letter replay) explicitly:
    // options.EnableMutatingActions = true;
});

var app = builder.Build();
app.MapDashboardApi();
```

`AddDashboard` registers validated `DashboardOptions` (validated at startup via `ValidateOnStart`) and
the subsystem read-endpoint module registry. `MapDashboardApi` maps a capability-discovery root endpoint
plus every registered subsystem's read endpoints under the configured route prefix.

## Design

- **Minimal API**, registered through `Microsoft.Extensions.DependencyInjection`.
- **AOT/trim-safe**: response DTOs serialize through a `System.Text.Json` source-generation context, so
  the consumer path carries no `[RequiresUnreferencedCode]`.
- **Fail-open**: a subsystem that is not configured yields an empty / not-configured payload, never a
  `500`.
- **Read-only by default**: mutating endpoints are not mapped at all unless `EnableMutatingActions` is
  set, and when set they additionally require an authenticated, authorized caller.
