// Multi-Provider Queue Processor Sample
// Demonstrates consuming from transport providers with MSSQL event store and ElasticSearch projections
//
// All transport providers use ADR-098 single entry points:
// - Azure Service Bus (AddAzureServiceBusTransport)
// - Kafka (AddKafkaTransport)
// - RabbitMQ (AddRabbitMQTransport)
// - AWS SQS (AddAwsSqsTransport)
// - Google Pub/Sub (AddGooglePubSubTransport)

#pragma warning disable CA1506 // Avoid excessive class coupling - acceptable for sample Program.cs

using Excalibur.Dispatch.Configuration;

using MultiProviderQueueProcessor.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// =============================================================================
// 1. Configure Event Store (SQL Server)
// =============================================================================
builder.Services.AddSqlServerEventSourcing(options =>
{
	options.ConnectionString = builder.Configuration.GetConnectionString("EventStore")
							   ?? throw new InvalidOperationException("EventStore connection string is required");

	// Optional: customize table names (these are the defaults)
	options.EventStoreTable = "Events";
	options.SnapshotStoreTable = "Snapshots";
	options.OutboxTable = "EventSourcedOutbox";
});

// Register aggregate repositories
builder.Services.AddScoped<OrderRepository>();

// =============================================================================
// 2. Configure ElasticSearch Projections
// =============================================================================
// Note: ElasticSearch services require configuration section "ElasticSearch" with:
// - Url, Urls[], or CloudId for connection
// - Optional: Username/Password, ApiKey, or Base64ApiKey for auth
// See Excalibur.Data.ElasticSearch for full configuration options.
builder.Services.AddElasticsearchServices(
	builder.Configuration,
	registry: null);

// Add projection services if needed
builder.Services.AddElasticsearchProjections(builder.Configuration);

// =============================================================================
// 3. Configure Dispatch Pipeline with Transport Providers
// =============================================================================

var messagingConfig = builder.Configuration.GetSection("CloudMessaging");

// Register Dispatch with assembly scanning - discovers all handlers implementing
// IActionHandler<>, IEventHandler<>, IDocumentHandler<>
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// =============================================================================
// 3b. Configure Transport Providers (ADR-098 Single Entry Points)
// =============================================================================

// Azure Service Bus
var azureConfig = messagingConfig.GetSection("Providers:azure-servicebus");
var azureConnectionString = azureConfig["ConnectionString"];
if (!string.IsNullOrEmpty(azureConnectionString))
{
	_ = builder.Services.AddAzureServiceBusTransport("azure-servicebus", asb =>
	{
		_ = asb.ConnectionString(azureConnectionString)
			.ConfigureProcessor(processor =>
			{
				_ = processor.MaxConcurrentCalls(azureConfig.GetValue("MaxConcurrentCalls", 10))
					.PrefetchCount(azureConfig.GetValue("PrefetchCount", 20));
			})
			.MapEntity<object>(azureConfig["QueueName"] ?? "dispatch-events");
	});
}

// Kafka
var kafkaConfig = messagingConfig.GetSection("Providers:kafka");
var kafkaServers = kafkaConfig["BootstrapServers"];
if (!string.IsNullOrEmpty(kafkaServers))
{
	_ = builder.Services.AddKafkaTransport("kafka", kafka =>
	{
		_ = kafka.BootstrapServers(kafkaServers)
			.ConfigureConsumer(consumer =>
			{
				_ = consumer.GroupId(kafkaConfig["GroupId"] ?? "processor-group");
			})
			.MapTopic<object>(kafkaConfig["Topic"] ?? "events");
	});
}

// RabbitMQ
var rabbitConfig = messagingConfig.GetSection("Providers:rabbitmq");
var rabbitConnectionString = rabbitConfig["ConnectionString"];
if (!string.IsNullOrEmpty(rabbitConnectionString))
{
	_ = builder.Services.AddRabbitMQTransport("rabbitmq", rmq =>
	{
		_ = rmq.ConnectionString(rabbitConnectionString)
			.ConfigureQueue(queue =>
			{
				_ = queue.Name(rabbitConfig["QueueName"] ?? "dispatch-events")
					.PrefetchCount((ushort)rabbitConfig.GetValue("PrefetchCount", 10));
			});
	});
}

// =============================================================================
// 4. Additional Transports (ADR-098 Single Entry Points)
// =============================================================================

// AWS SQS
// Note: AWS region is configured via AWS SDK defaults (environment, credentials file, or IAM role)
var awsConfig = messagingConfig.GetSection("Providers:aws-sqs");
var awsQueueUrl = awsConfig["QueueUrl"];
if (!string.IsNullOrEmpty(awsQueueUrl))
{
	_ = builder.Services.AddAwsSqsTransport("aws-sqs", sqs =>
	{
		_ = sqs.ConfigureQueue(queue =>
			{
				_ = queue.ReceiveWaitTimeSeconds(awsConfig.GetValue("WaitTimeSeconds", 20))
					.VisibilityTimeout(TimeSpan.FromSeconds(awsConfig.GetValue("VisibilityTimeout", 30)));
			})
			.ConfigureBatch(batch =>
			{
				_ = batch.ReceiveMaxMessages(awsConfig.GetValue("MaxNumberOfMessages", 10));
			})
			.MapQueue<object>(awsQueueUrl);
	});
}

// Google Pub/Sub
var pubsubConfig = messagingConfig.GetSection("Providers:google-pubsub");
if (!string.IsNullOrEmpty(pubsubConfig["ProjectId"]))
{
	_ = builder.Services.AddGooglePubSubTransport("google-pubsub", pubsub =>
	{
		_ = pubsub.ProjectId(pubsubConfig["ProjectId"])
			.TopicId(pubsubConfig["TopicId"] ?? "dispatch-events")
			.SubscriptionId(pubsubConfig["SubscriptionId"] ?? "dispatch-processor");
	});
}

// =============================================================================
// 5. Configure Background Services
// =============================================================================

// Outbox processor (publishes events from SQL Server outbox to transports)
builder.Services.AddHostedService<OutboxProcessorService>();

// =============================================================================
// 6. Build and Run
// =============================================================================
var host = builder.Build();

// Initialize database schema on startup (development only)
if (builder.Environment.IsDevelopment())
{
	await host.Services.InitializeDatabaseAsync();
}

await host.RunAsync();

// =============================================================================
// Supporting Types
// =============================================================================

namespace MultiProviderQueueProcessor
{
	/// <summary>
	/// Transport configuration options.
	/// </summary>
	public class TransportOptions
	{
		public string DefaultTransport { get; set; } = "azure-servicebus";
	}
}
