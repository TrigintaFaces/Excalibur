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

using MultiBusSample;

var builder = new HostApplicationBuilder(args);

// Configure Dispatch with routing rules
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
	_ = dispatch.UseRouting(routing =>
	{
		routing.Transport.Route<RabbitPingEvent>().To("rabbit");
		routing.Transport.Route<KafkaPingEvent>().To("kafka");
	});
});

// Configure outbox and inbox
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();

// Configure routing options
builder.Services.Configure<RoutingOptions>(static opts => opts.DefaultRemoteBusName = "rabbit");

// Add message buses
builder.Services.AddRabbitMqMessageBus(builder.Configuration);
builder.Services.AddKafkaMessageBus(builder.Configuration);

using var host = builder.Build();
await host.StartAsync().ConfigureAwait(false);

// Send test messages
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext();

#pragma warning disable CA1303 // Sample code
await dispatcher.DispatchAsync(new RabbitPingEvent("hello rabbit"), context, cancellationToken: default).ConfigureAwait(false);
await dispatcher.DispatchAsync(new KafkaPingEvent("hello kafka"), context, cancellationToken: default).ConfigureAwait(false);
#pragma warning restore CA1303
