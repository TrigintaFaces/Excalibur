# Excalibur.Dispatch.AspNetCore

Experience metapackage bundling Excalibur.Dispatch with ASP.NET Core hosting and observability. Provides a single `AddDispatchAspNetCore()` call for the common web scenario.

## Installation

```bash
dotnet add package Excalibur.Dispatch.AspNetCore
```

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDispatchAspNetCore(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();
app.MapControllers();
app.Run();
```

## What's Included

This metapackage bundles:

- `Excalibur.Dispatch` — Core messaging framework (dispatcher, pipeline, middleware)
- `Excalibur.Dispatch.Hosting.AspNetCore` — ASP.NET Core integration (controller/minimal-API helpers, `HttpContext` → message-context bridge)
- `Excalibur.Dispatch.Observability` — OpenTelemetry metrics and tracing (`AddDispatchInstrumentation()`)

`AddDispatchAspNetCore()` wires:

- The core dispatcher and pipeline (`AddDispatch`)
- Observability instrumentation (`UseObservability`)
- **Ambient-scope resolution** (`AddDispatchAmbientScope`) — scoped message handlers (and handlers with
  scoped dependencies such as `IUnitOfWork` / `IDb` / `DbContext`) resolve from, and share state with,
  the **active request scope**.

## Scoped handlers

With this package, dispatching a scoped handler "just works" — including via the context-less
`dispatcher.DispatchAsync(message, ct)` overload — because the handler is resolved from the active
request scope rather than the root container. Outside a request (background work), each dispatch gets a
fresh scope that is disposed when the handler completes.

## OpenTelemetry

To export metrics and traces, subscribe Dispatch instrumentation on your OpenTelemetry builder:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddDispatchInstrumentation())
    .WithTracing(t => t.AddDispatchInstrumentation());
```

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
