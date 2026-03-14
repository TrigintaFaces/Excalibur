// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Remote Bus Sample (RabbitMQ)
// ============================
// This sample demonstrates how to configure Dispatch with a RabbitMQ transport
// and the Excalibur outbox/inbox processors for reliable remote messaging.
//
// Prerequisites:
// 1. Start RabbitMQ: docker run -p 5672:5672 -p 15672:15672 rabbitmq:3-management
// 2. Run the sample: dotnet run
//
// Management UI: http://localhost:15672 (guest/guest)

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Inbox.InMemory;
using Excalibur.Outbox.InMemory;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Routing;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RemoteBusSample;

// Build configuration
var builder = new HostApplicationBuilder(args);

// Add appsettings.json configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure logging for visibility
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Information);
});

// ============================================================
// Configure Dispatch with RabbitMQ routing
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer for message payloads
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);

	// Route PingEvent to RabbitMQ transport
	_ = dispatch.UseRouting(routing =>
		routing.Transport.Route<PingEvent>().To("rabbitmq"));
});

// ============================================================
// Configure outbox/inbox for reliable messaging
// ============================================================
// The outbox pattern ensures messages are persisted before sending,
// providing at-least-once delivery guarantees.
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();

// Set default remote bus for outbox processing
builder.Services.Configure<RoutingOptions>(static opts =>
	opts.DefaultRemoteBusName = "rabbitmq");

// ============================================================
// Configure RabbitMQ transport (ADR-098 Single Entry Point)
// ============================================================
// Get connection string from configuration, with fallback for local Docker
var connectionString = builder.Configuration["RabbitMq:ConnectionString"]
					   ?? "amqp://guest:guest@localhost:5672/";

builder.Services.AddRabbitMQTransport("rabbitmq", rmq =>
{
	_ = rmq.ConnectionString(connectionString)
		.ConfigureExchange(exchange =>
		{
			_ = exchange.Name("dispatch.remote")
				.Type(RabbitMQExchangeType.Topic)
				.AutoDelete(true); // Auto-delete in development
		})
		.ConfigureCloudEvents(ce =>
		{
			ce.Exchange.Persistence = RabbitMqPersistence.Persistent;
		});
});

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting Remote Bus Sample (RabbitMQ)...");
logger.LogInformation("Make sure RabbitMQ is running: docker run -p 5672:5672 -p 15672:15672 rabbitmq:3-management");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Dispatch sample messages
// ============================================================
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext();

logger.LogInformation("Publishing sample PingEvent...");

var ping = new PingEvent("hello remote");
_ = await dispatcher.DispatchAsync(ping, context, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("Published: PingEvent Message={Message}", ping.Message);

// Also demonstrate the command handler (in-process, not routed to transport)
var command = new PingCommand { Text = "hello command" };
var result = await dispatcher.DispatchAsync(command, context, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("Command result: {Result}", result);

logger.LogInformation("Messages published. Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
