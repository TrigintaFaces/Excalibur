// Database Initialization Helper

using Elastic.Clients.Elasticsearch;

namespace MultiProviderQueueProcessor.Infrastructure;

/// <summary>
/// Extension methods for database initialization.
/// </summary>
public static class DatabaseInitializer
{
	/// <summary>
	/// Initializes database schemas on startup (development only).
	/// </summary>
	public static async Task InitializeDatabaseAsync(this IServiceProvider services)
	{
		var logger = services.GetRequiredService<ILogger<Program>>();

		// Initialize SQL Server tables (event store, snapshots, outbox)
		await InitializeSqlServerAsync(services, logger);

		// Initialize ElasticSearch indices
		await InitializeElasticSearchAsync(services, logger);
	}

	private static Task InitializeSqlServerAsync(IServiceProvider services, ILogger logger)
	{
		// SQL Server schema is typically created via migrations or scripts.
		// This is a placeholder for development-time initialization.
		//
		// In production, use proper database migrations:
		// - FluentMigrator
		// - DbUp
		// - EF Core Migrations (for non-event-store tables)
		// - SQL scripts with deployment pipelines

		logger.LogInformation("SQL Server schema initialization would happen here");
		logger.LogInformation("Run the following SQL to create tables:");
		logger.LogInformation(@"
-- Events table (default: dbo.EventStoreEvents, configurable via SqlServerEventSourcingOptions)
CREATE TABLE [dbo].[EventStoreEvents] (
    [Position]       BIGINT IDENTITY(1,1)  NOT NULL,
    [EventId]        NVARCHAR(256)         NOT NULL,
    [AggregateId]    NVARCHAR(256)         NOT NULL,
    [AggregateType]  NVARCHAR(256)         NOT NULL,
    [EventType]      NVARCHAR(256)         NOT NULL,
    [EventData]      VARBINARY(MAX)        NOT NULL,
    [Metadata]       VARBINARY(MAX)        NULL,
    [Version]        BIGINT                NOT NULL,
    [Timestamp]      DATETIMEOFFSET        NOT NULL,

    CONSTRAINT [PK_EventStoreEvents] PRIMARY KEY CLUSTERED ([Position]),
    CONSTRAINT [UQ_EventStoreEvents_Stream] UNIQUE ([AggregateId], [AggregateType], [Version])
);
CREATE INDEX [IX_EventStoreEvents_AggregateId] ON [dbo].[EventStoreEvents]([AggregateId], [AggregateType]);
CREATE INDEX [IX_EventStoreEvents_EventType] ON [dbo].[EventStoreEvents]([EventType]);

-- Snapshots table (default: dbo.EventStoreSnapshots, configurable via SqlServerEventSourcingOptions)
CREATE TABLE [dbo].[EventStoreSnapshots] (
    [SnapshotId]     NVARCHAR(256)         NOT NULL,
    [AggregateId]    NVARCHAR(256)         NOT NULL,
    [AggregateType]  NVARCHAR(256)         NOT NULL,
    [Version]        BIGINT                NOT NULL,
    [Data]           VARBINARY(MAX)        NOT NULL,
    [CreatedAt]      DATETIMEOFFSET        NOT NULL,
    [Metadata]       VARBINARY(MAX)        NULL,

    CONSTRAINT [PK_EventStoreSnapshots] PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType])
);

-- Outbox table (default: dbo.OutboxMessages, configurable via ISqlServerOutboxBuilder)
-- Note: The outbox is managed separately via services.AddExcalibur(x => x.AddOutbox(...)).
CREATE TABLE [dbo].[OutboxMessages] (
    [Id] NVARCHAR(256) NOT NULL PRIMARY KEY,
    [MessageType] NVARCHAR(512) NOT NULL,
    [Payload] VARBINARY(MAX) NOT NULL,
    [Headers] NVARCHAR(MAX) NULL,
    [Destination] NVARCHAR(512) NULL,
    [CreatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    [ScheduledAt] DATETIMEOFFSET NULL,
    [Status] INT NOT NULL DEFAULT 0,
    [RetryCount] INT NOT NULL DEFAULT 0,
    [CorrelationId] NVARCHAR(256) NULL,
    [CausationId] NVARCHAR(256) NULL,
    [TenantId] NVARCHAR(256) NULL,
    [Priority] INT NOT NULL DEFAULT 0,
    [TargetTransports] NVARCHAR(MAX) NULL,
    [IsMultiTransport] BIT NOT NULL DEFAULT 0
);
CREATE INDEX [IX_OutboxMessages_Status] ON [dbo].[OutboxMessages]([Status]) WHERE [Status] = 0;
");

		return Task.CompletedTask;
	}

	private static async Task InitializeElasticSearchAsync(IServiceProvider services, ILogger logger)
	{
		try
		{
			var client = services.GetService<ElasticsearchClient>();
			if (client == null)
			{
				logger.LogWarning("ElasticSearch client not configured, skipping index initialization");
				return;
			}

			// Check if orders index exists
			var existsResponse = await client.Indices.ExistsAsync("orders");

			if (!existsResponse.Exists)
			{
				logger.LogInformation("Creating 'orders' ElasticSearch index");

				// Create index with basic mappings
				// Note: In production, configure nested object mappings for Items
				var createResponse = await client.Indices.CreateAsync("orders", c => c
					.Mappings(m => m
						.Properties<Projections.OrderProjection>(p => p
							.Keyword(k => k!.CustomerId!)
							.Keyword(k => k!.Status!)
							.Keyword(k => k!.Currency!)
							.Keyword(k => k!.TrackingNumber!)
							.Keyword(k => k!.Carrier!)
							.Text(t => t!.CancellationReason!)
							.Date(d => d!.CreatedAt)
							.Date(d => d!.ShippedAt)
							.Date(d => d!.CancelledAt)
							.Date(d => d!.LastModified))));

				if (!createResponse.IsValidResponse)
				{
					logger.LogError("Failed to create orders index: {Error}", createResponse.DebugInformation);
				}
				else
				{
					logger.LogInformation("Created 'orders' ElasticSearch index");
				}
			}
			else
			{
				logger.LogDebug("'orders' ElasticSearch index already exists");
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "ElasticSearch initialization failed (service may not be running)");
		}
	}
}
