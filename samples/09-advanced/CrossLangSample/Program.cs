// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in
// the project root or online.
//
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Abstractions.Pipeline;
using Excalibur.Dispatch.Delivery;
using Tests.Shared.Handlers.Statistics;
using Excalibur.Dispatch.Serialization;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Inbox;
using Excalibur.Dispatch.Delivery.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using examples.CrossLangSample;

var builder = new HostApplicationBuilder(args);

// Configure Dispatch and routing
builder.Services.AddDispatch(dispatch =>
{
	dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	_ = dispatch.AddDispatchSerializer<JsonMessageSerializer>(version: 0);
	_ = dispatch.WithRoutingRules(rules =>
	{
		rules.AddRule<PingEvent>((_, _) => "rabbit");
	});
});

// Configure outbox and inbox processors
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();

// Route messages to RabbitMQ by default
builder.Services.Configure<RoutingOptions>(opts => opts.DefaultRemoteBusName = "rabbit");

// Register RabbitMQ message bus
builder.Services.AddRabbitMqMessageBus(builder.Configuration);

using var host = builder.Build();
await host.StartAsync().ConfigureAwait(false);

// Dispatch a sample event
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext();

#pragma warning disable CA1303 // Sample output
await dispatcher.DispatchAsync(new PingEvent("hello cross-lang"), context).ConfigureAwait(false);
#pragma warning restore CA1303
