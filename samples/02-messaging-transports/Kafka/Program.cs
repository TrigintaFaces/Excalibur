// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Kafka Transport Sample
// ======================
// This sample demonstrates how to use Excalibur.Dispatch.Transport.Kafka for
// publishing and consuming messages via Apache Kafka.
//
// Prerequisites:
// 1. Start Kafka: docker-compose up -d
// 2. Run the sample: dotnet run
//
// Kafka will be available at localhost:9092

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Routing;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Kafka;

using KafkaSample.Messages;

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
// Configure Dispatch with Kafka routing
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer for message payloads
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);

	// Route SensorReadingEvent to Kafka transport
	_ = dispatch.UseRouting(routing =>
		routing.Transport.Route<SensorReadingEvent>().To("kafka"));
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
	opts.DefaultRemoteBusName = "kafka");

// ============================================================
// Configure Kafka transport (ADR-098 Single Entry Point)
// ============================================================
// Get bootstrap servers from configuration, with fallback for local Docker
var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
					   ?? "localhost:9092";

builder.Services.AddKafkaTransport("kafka", kafka =>
{
	_ = kafka.BootstrapServers(bootstrapServers)
		.ConfigureProducer(producer =>
		{
			_ = producer.ClientId("dispatch-sensor-producer")
				.Acks(KafkaAckLevel.All)
				.CompressionType(KafkaCompressionType.Snappy);
		})
		.ConfigureConsumer(consumer =>
		{
			_ = consumer.GroupId("dispatch-sensor-consumer");
		})
		.MapTopic<SensorReadingEvent>("sensor-readings");
});

// Add CloudEvents support for Kafka (separate from transport builder)
builder.Services.UseCloudEventsForKafka();

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting Kafka Sample...");
logger.LogInformation("Make sure Kafka is running: docker-compose up -d");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Publish sample messages
// ============================================================
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext();

logger.LogInformation("Publishing sample SensorReadingEvent messages...");

// Simulate sensor readings from multiple devices
var sensors = new[] { "SENSOR-001", "SENSOR-002", "SENSOR-003" };
var random = new Random(42);

for (var i = 0; i < 10; i++)
{
	var sensorId = sensors[i % sensors.Length];
	var reading = new SensorReadingEvent(
		SensorId: sensorId,
		Temperature: 20.0 + (random.NextDouble() * 10),
		Humidity: 40.0 + (random.NextDouble() * 30),
		Timestamp: DateTimeOffset.UtcNow);

	_ = await dispatcher.DispatchAsync(reading, context, cancellationToken: default).ConfigureAwait(false);
	logger.LogInformation(
		"Published: Sensor={SensorId}, Temp={Temperature:F1}°C",
		reading.SensorId,
		reading.Temperature);

	// Small delay between readings
	await Task.Delay(100).ConfigureAwait(false);
}

logger.LogInformation("Messages published to Kafka topic 'sensor-readings'");
logger.LogInformation("Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
