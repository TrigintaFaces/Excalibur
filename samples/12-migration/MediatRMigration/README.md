# MediatR → Excalibur.Dispatch Migration Sample

Demonstrates **drop-in, source-compatible migration** off the now-commercial MediatR onto
`Excalibur.Dispatch`, via the `Excalibur.Dispatch.Compat.MediatR` package (EPIC w2zq7d, FR-18 / AC-13).

## The migration is a namespace swap

This is an ordinary MediatR application. Migrating it required **only** the two mechanical edits the
WS2 Roslyn analyzer code-fixes apply automatically — no handler, request, notification, or behavior
source changed:

| Before (MediatR) | After (compat) |
| --- | --- |
| `using MediatR;` | `using Excalibur.Dispatch.Compat.MediatR;` |
| `services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...))` | `services.AddMediatRCompat(cfg => cfg.RegisterServicesFromAssembly(...))` |

## What it shows

- **Request/response** — `Ping : IRequest<string>` + `PingHandler : IRequestHandler<Ping,string>` via `IMediator.Send`.
- **Publish fan-out** — `Pinged : INotification` dispatched to two `INotificationHandler<Pinged>` handlers.
- **Pipeline behavior** — open `LoggingBehavior<,>` registered with `AddOpenBehavior`, wrapping each request.
- **Streaming** — `Countdown : IStreamRequest<int>` via `IMediator.CreateStream`.

## AOT-safe

The compat surface registers handlers via an **AOT-safe source generator** (no reflection on the
dispatch path); the sample passes `PublishAot=true` IL-trim validation (see the `c37y1v` AOT gate).

## Run

```bash
dotnet run --project samples/12-migration/MediatRMigration
```

Expected output:

```
[pipeline] -> Ping
[pipeline] <- Ping
Send(Ping)        -> pong:hello
[log]   Pinged: world
[audit] Pinged: world
CreateStream(3)   -> 3 2 1
```
