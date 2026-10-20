// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

using Excalibur.Outbox.InMemory;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Routing;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Build configuration
var builder = new HostApplicationBuilder(args);

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

	// Configure JSON serialization
	_ = dispatch.WithSerialization(serialization => serialization.RegisterSystemTextJson().UseSystemTextJson());
	// Route OrderPlacedEvent to AWS SQS transport
	_ = dispatch.UseRouting(routing =>
	{
		routing.Transport.Route<OrderPlacedEvent>().To("sqs");
		routing.Endpoints.Route<OrderPlacedEvent>().To("sqs");
	});
	_ = dispatch.UseMiddleware<RoutingMiddleware>();
});

// ============================================================
// Configure outbox/inbox for reliable messaging
// ============================================================
// Register volatile outbox/inbox services for experiments. This demo publishes
// directly; registration alone does not stage its messages in the outbox.
builder.Services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
	outbox.UseInMemory().EnableBackgroundProcessing()));
builder.Services.AddInMemoryInboxStore();
// The inbox/outbox processors require an EXPLICIT dead-letter decision. There is no default, so
// that no deployment can ever silently discard a message it failed to deliver. This sample chooses
// discard because it has no durable store to dead-letter into; a real deployment should register a
// durable queue instead -- AddSqlServerDeadLetterQueue(o => o.ConnectionString = ...) -- so
// exhausted messages are kept for inspection and replay rather than dropped. SQL Server is the
// only durable IDeadLetterQueue the framework ships; on any other store, implement the interface.
builder.Services.AddSingleton<IDeadLetterQueue>(NullDeadLetterQueue.Instance);

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
if (useLocalStack)
{
	ArgumentException.ThrowIfNullOrEmpty(serviceUrl);
	// LocalStack configuration
	_ = builder.Services.AddKeyedSingleton<IAmazonSQS>("sqs", (_, _) =>
	{
		var config = new AmazonSQSConfig
		{
			RegionEndpoint = RegionEndpoint.GetBySystemName(region),
			ServiceURL = serviceUrl,
			UseHttp = true,
			MaxErrorRetry = 0,
			MaxStaleConnectionRetries = 0,
		};
		return new AmazonSQSClient(
			new BasicAWSCredentials("test", "test"),
			config);
	});
}

// Production uses the transport's default credential chain and separate send/receive clients.

// Add AWS SQS message bus (Single Entry Point)
builder.Services.AddAwsSqsTransport("sqs", sqs =>
{
	_ = sqs.UseRegion(region)
		.UseMaxRetryAttempts(2)
		.UseRequestTimeout(TimeSpan.FromSeconds(10))
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

logger.LogInformation("Publishing sample OrderPlacedEvent messages...");

// Simulate multiple orders
var orders = new[]
{
	new OrderPlacedEvent("ORD-001", "CUST-100", 99.99m), new OrderPlacedEvent("ORD-002", "CUST-101", 249.50m),
	new OrderPlacedEvent("ORD-003", "CUST-100", 15.00m),
};

foreach (var order in orders)
{
	var context = DispatchContextInitializer.CreateDefaultContext();
	var result = await dispatcher.DispatchAsync(order, context, cancellationToken: default).ConfigureAwait(false);
	if (!result.Succeeded)
	{
		logger.LogError("SQS dispatch failed for {OrderId}: {Error}", order.OrderId, result.ErrorMessage);
		throw new InvalidOperationException("Failed to dispatch the sample order to SQS.");
	}
	logger.LogInformation(
		"Published: OrderId={OrderId}, Amount={Amount:C}",
		order.OrderId,
		order.TotalAmount);
}

logger.LogInformation("Messages published to AWS SQS queue: {QueueUrl}", queueUrl);
if (builder.Configuration.GetValue<bool>("Sample:RunOnce"))
{
	await host.StopAsync().ConfigureAwait(false);
	return;
}
logger.LogInformation("Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
