// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Multi-Bus Sample (RabbitMQ + Kafka)
// ====================================
// This sample demonstrates how to register both RabbitMQ and Kafka transports
// and route different message types to each broker using Dispatch routing rules.
//
// Prerequisites:
// 1. Start RabbitMQ and Kafka: docker-compose up -d
// 2. Run the sample: dotnet run
//
// RabbitMQ Management UI: http://localhost:15672 (guest/guest)

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Outbox.InMemory;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Routing;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Kafka;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using MultiBusSample;

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
// Configure Dispatch with multi-transport routing
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Configure JSON serialization
	_ = dispatch.WithSerialization(config => config.UseSystemTextJson());

	// Route each event type to its dedicated transport
	_ = dispatch.UseRouting(routing =>
	{
		routing.Transport.Route<RabbitPingEvent>().To("rabbitmq");
		routing.Transport.Route<KafkaPingEvent>().To("kafka");
	});
});

// ============================================================
// Configure outbox/inbox for reliable messaging
// ============================================================
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInMemoryInboxStore();
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();

// Set default remote bus for outbox processing
builder.Services.Configure<RoutingOptions>(static opts =>
	opts.DefaultRemoteBusName = "rabbitmq");

// ============================================================
// Configure RabbitMQ transport (ADR-098 Single Entry Point)
// ============================================================
var rabbitConnectionString = builder.Configuration["RabbitMq:ConnectionString"]
							?? "amqp://guest:guest@localhost:5672/";

builder.Services.AddRabbitMQTransport("rabbitmq", rmq =>
{
	_ = rmq.ConnectionString(rabbitConnectionString)
		.ConfigureExchange(exchange =>
		{
			_ = exchange.Name("dispatch.multibus")
				.Type(RabbitMQExchangeType.Topic)
				.AutoDelete(true); // Auto-delete in development
		})
		.ConfigureCloudEvents(ce =>
		{
			ce.Exchange.Persistence = RabbitMqPersistence.Persistent;
		});
});

// ============================================================
// Configure Kafka transport (ADR-098 Single Entry Point)
// ============================================================
var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
							?? "localhost:9092";

builder.Services.AddKafkaTransport("kafka", kafka =>
{
	_ = kafka.BootstrapServers(kafkaBootstrapServers)
		.ConfigureProducer(producer =>
		{
			_ = producer.ClientId("dispatch-multibus-producer")
				.Acks(KafkaAckLevel.All);
		})
		.ConfigureConsumer(consumer =>
		{
			_ = consumer.GroupId("dispatch-multibus-consumer");
		})
		.MapTopic<KafkaPingEvent>("multibus-ping");
});

// Add CloudEvents support for Kafka
builder.Services.UseCloudEventsForKafka();

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting Multi-Bus Sample (RabbitMQ + Kafka)...");
logger.LogInformation("Make sure both brokers are running: docker-compose up -d");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Dispatch sample messages to both transports
// ============================================================
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext();

logger.LogInformation("Publishing messages to RabbitMQ and Kafka...");

// Send to RabbitMQ
var rabbitPing = new RabbitPingEvent("hello rabbit");
_ = await dispatcher.DispatchAsync(rabbitPing, context, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("Published to RabbitMQ: {Text}", rabbitPing.Text);

// Send to Kafka
var kafkaPing = new KafkaPingEvent("hello kafka");
_ = await dispatcher.DispatchAsync(kafkaPing, context, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("Published to Kafka: {Text}", kafkaPing.Text);

logger.LogInformation("Messages published to both transports. Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
