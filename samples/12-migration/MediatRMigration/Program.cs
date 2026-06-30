using Excalibur.Dispatch.Compat.MediatR;

using MediatRMigration.Behaviors;
using MediatRMigration.Messages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Ported MediatR application (EPIC w2zq7d / AC-13).
//
// The ONLY edits to migrate this app off the (now-commercial) MediatR were the two mechanical
// changes the WS2 analyzer code-fixes apply automatically:
//   • `using MediatR;`           ->  `using Excalibur.Dispatch.Compat.MediatR;`  (this file)
//   • `services.AddMediatR(...)` ->  `services.AddMediatRCompat(...)`            (below)
// Every request, handler, notification, and pipeline behavior is source-identical to the original.
// ─────────────────────────────────────────────────────────────────────────────────────────────

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMediatRCompat(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Ping>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

using var host = builder.Build();
var mediator = host.Services.GetRequiredService<IMediator>();

// Request / response (single handler, unwrapped response).
var pong = await mediator.Send(new Ping("hello"));
Console.WriteLine($"Send(Ping)        -> {pong}");

// Notification (publish fan-out to all registered handlers).
await mediator.Publish(new Pinged("world"));

// Streaming request (async sequence).
Console.Write("CreateStream(3)   -> ");
await foreach (var n in mediator.CreateStream(new Countdown(3)))
{
    Console.Write($"{n} ");
}

Console.WriteLine();
