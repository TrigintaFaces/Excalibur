// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Azure Service Bus Transport Sample
// ===================================
// This sample demonstrates how to use Excalibur.Dispatch.Transport.AzureServiceBus for
// publishing and consuming messages via Azure Service Bus.
//
// Prerequisites:
// 1. Create an Azure Service Bus namespace in Azure Portal
// 2. Create a queue named "dispatch-orders"
// 3. Update appsettings.json with your connection string
// 4. Run the sample: dotnet run
//
// For local development without Azure:
// - Use Azure Service Bus Emulator: https://docs.microsoft.com/azure/service-bus-messaging/overview-emulator
// - Or use Azurite for basic testing scenarios

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using AzureServiceBusSample.Messages;

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Routing;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
// Configure Dispatch with Azure Service Bus routing
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer for message payloads
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);

	// Route OrderPlacedEvent to Azure Service Bus transport
	_ = dispatch.UseRouting(routing =>
		routing.Transport.Route<OrderPlacedEvent>().To("azureservicebus"));
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
	opts.DefaultRemoteBusName = "azureservicebus");

// ============================================================
// Configure Azure Service Bus transport
// ============================================================
// Get configuration from appsettings.json
var connectionString = builder.Configuration["AzureServiceBus:ConnectionString"]
					   ?? throw new InvalidOperationException(
						   "Azure Service Bus connection string not configured. " +
						   "Set 'AzureServiceBus:ConnectionString' in appsettings.json or environment variable.");

var queueName = builder.Configuration["AzureServiceBus:QueueName"] ?? "dispatch-orders";

builder.Services.AddAzureServiceBusTransport("azureservicebus", sb =>
{
	_ = sb.ConnectionString(connectionString)
		.MapEntity<OrderPlacedEvent>(queueName)
		.ConfigureProcessor(processor =>
		{
			_ = processor.MaxConcurrentCalls(int.TryParse(
				builder.Configuration["AzureServiceBus:MaxConcurrentCalls"],
				out var concurrent)
				? concurrent
				: 10);
			_ = processor.PrefetchCount(int.TryParse(
				builder.Configuration["AzureServiceBus:PrefetchCount"],
				out var prefetch)
				? prefetch
				: 50);
		});
});

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting Azure Service Bus Sample...");
logger.LogInformation("Ensure your Azure Service Bus namespace is configured in appsettings.json");
logger.LogInformation("Queue: {QueueName}", queueName);

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

logger.LogInformation("Messages published to Azure Service Bus queue: {QueueName}", queueName);
logger.LogInformation("Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
