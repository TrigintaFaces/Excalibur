// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// AWS SQS Transport Sample
// ========================
// This sample demonstrates how to use Excalibur.Dispatch.Transport.AwsSqs for
// publishing and consuming messages via AWS SQS with LocalStack.
//
// Prerequisites:
// 1. Start LocalStack: docker-compose up -d
// 2. Create queue: awslocal sqs create-queue --queue-name dispatch-orders
// 3. Run the sample: dotnet run
//
// For production AWS:
// - Configure AWS credentials via environment variables, AWS CLI, or IAM role
// - Set UseLocalStack to false in appsettings.json

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Amazon;
using Amazon.Runtime;
using Amazon.SQS;

using AwsSqsSample.Messages;

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
// Configure Dispatch with AWS SQS routing
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer for message payloads
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);

	// Route OrderPlacedEvent to AWS SQS transport
	_ = dispatch.UseRouting(routing =>
		routing.Transport.Route<OrderPlacedEvent>().To("sqs"));
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
	opts.DefaultRemoteBusName = "sqs");

// ============================================================
// Configure AWS SQS transport
// ============================================================
// Get configuration from appsettings.json
var region = builder.Configuration["AwsSqs:Region"] ?? "us-east-1";
var serviceUrl = builder.Configuration["AwsSqs:ServiceUrl"];
var queueUrl = builder.Configuration["AwsSqs:QueueUrl"]
			   ?? throw new InvalidOperationException(
				   "AWS SQS queue URL not configured. " +
				   "Set 'AwsSqs:QueueUrl' in appsettings.json or environment variable.");

var useLocalStack = builder.Configuration.GetValue("AwsSqs:UseLocalStack", true);

// Register AWS SQS client with LocalStack or production configuration
if (useLocalStack && !string.IsNullOrEmpty(serviceUrl))
{
	// LocalStack configuration
	_ = builder.Services.AddSingleton<IAmazonSQS>(_ =>
	{
		var config = new AmazonSQSConfig
		{
			RegionEndpoint = RegionEndpoint.GetBySystemName(region),
			ServiceURL = serviceUrl,
			UseHttp = true,
		};
		return new AmazonSQSClient(
			new BasicAWSCredentials("test", "test"),
			config);
	});
}
else
{
	// Production AWS configuration (uses default credential chain)
	_ = builder.Services.AddSingleton<IAmazonSQS>(_ =>
	{
		var config = new AmazonSQSConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(region), };
		return new AmazonSQSClient(config);
	});
}

// Add AWS SQS message bus (ADR-098 Single Entry Point)
builder.Services.AddAwsSqsTransport("sqs", sqs =>
{
	_ = sqs.UseRegion(region)
		.MapQueue<OrderPlacedEvent>(queueUrl);
});

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting AWS SQS Sample...");
logger.LogInformation("Using {Mode} mode", useLocalStack ? "LocalStack" : "Production AWS");
logger.LogInformation("Queue URL: {QueueUrl}", queueUrl);

if (useLocalStack)
{
	logger.LogInformation("Ensure LocalStack is running: docker-compose up -d");
	logger.LogInformation("Create queue if needed: awslocal sqs create-queue --queue-name dispatch-orders");
}

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

logger.LogInformation("Messages published to AWS SQS queue: {QueueUrl}", queueUrl);
logger.LogInformation("Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
