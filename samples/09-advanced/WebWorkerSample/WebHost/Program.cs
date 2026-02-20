// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;

using WebHost;

using WebWorkerSample.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExcaliburWebServices(builder.Configuration, typeof(PingCommand).Assembly);
builder.Services.AddGlobalExceptionHandler();

DeliveryServiceCollectionExtensions.AddOutbox<InMemoryOutboxStore>(builder.Services);
DeliveryServiceCollectionExtensions.AddInbox<InMemoryInboxStore>(builder.Services);
OutboxServiceCollectionExtensions.AddOutboxHostedService(builder.Services);
OutboxServiceCollectionExtensions.AddInboxHostedService(builder.Services);

builder.Services.AddDispatchRouting(static options => options.DefaultRemoteBusName = "rabbit");

builder.Services.AddRabbitMqMessageBus(builder.Configuration);

var app = builder.Build();

app.UseExcaliburWebHost();

app.MapPost("/ping", static async (PingCommand command, IDispatcher dispatcher) =>
{
	var context = Excalibur.Dispatch.Messaging.DispatchContextInitializer.CreateDefaultContext();
	var result = await dispatcher.DispatchAsync<PingCommand, string>(command, context, cancellationToken: default).ConfigureAwait(false);
	return Results.Ok(result.ReturnValue);
});

await app.RunAsync().ConfigureAwait(false);
