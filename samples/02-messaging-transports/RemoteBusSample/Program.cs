// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Routing;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using RemoteBusSample;

var builder = new HostApplicationBuilder(args);

// Configure Dispatch and routing
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
	_ = dispatch.UseRouting(routing => routing.Transport.Route<PingEvent>().To("rabbit"));
});

// Configure outbox and inbox processors
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();

// Route messages to RabbitMQ by default
builder.Services.Configure<RoutingOptions>(static opts => opts.DefaultRemoteBusName = "rabbit");

// Register RabbitMQ message bus
builder.Services.AddRabbitMqMessageBus(builder.Configuration);

using var host = builder.Build();
await host.StartAsync().ConfigureAwait(false);

// Dispatch a sample event
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext();

#pragma warning disable CA1303 // Sample output
await dispatcher.DispatchAsync(new PingEvent("hello remote"), context, cancellationToken: default).ConfigureAwait(false);
#pragma warning restore CA1303
