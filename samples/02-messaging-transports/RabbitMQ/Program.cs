// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// RabbitMQ Transport Sample
// =========================
// This sample demonstrates how to use Excalibur.Dispatch.Transport.RabbitMQ for
// publishing and consuming messages via RabbitMQ.
//
// Prerequisites:
// 1. Start RabbitMQ: docker-compose up -d
// 2. Run the sample: dotnet run
//
// Management UI: http://localhost:15672 (guest/guest)

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Data.InMemory.Outbox;
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

using RabbitMQSample.Messages;

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

	// Route OrderPlacedEvent to RabbitMQ transport
	_ = dispatch.UseRouting(routing =>
		routing.Transport.Route<OrderPlacedEvent>().To("rabbitmq"));
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
			_ = exchange.Name("dispatch.events")
				.Type(RabbitMQExchangeType.Topic)
				.AutoDelete(true); // Auto-delete in development
		})
		.ConfigureCloudEvents(ce =>
		{
			ce.Persistence = RabbitMqPersistence.Persistent;
		});
});

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting RabbitMQ Sample...");
logger.LogInformation("Make sure RabbitMQ is running: docker-compose up -d");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Publish sample messages
// ============================================================
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext();

logger.LogInformation("Publishing sample OrderPlacedEvent messages...");

// Simulate multiple orders
var orders = new[]
{
	new OrderPlacedEvent("ORD-001", "CUST-100", 99.99m), new OrderPlacedEvent("ORD-002", "CUST-101", 249.50m),
	new OrderPlacedEvent("ORD-003", "CUST-100", 15.00m),
};

foreach (var order in orders)
{
	_ = await dispatcher.DispatchAsync(order, context, cancellationToken: default).ConfigureAwait(false);
	logger.LogInformation(
		"Published: OrderId={OrderId}, Amount={Amount:C}",
		order.OrderId,
		order.TotalAmount);
}

logger.LogInformation("Messages published. Check RabbitMQ Management UI at http://localhost:15672");
logger.LogInformation("Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
