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

		// Initialize SQL Server tables (event store, snapshots)
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
    [EventId]        NVARCHAR(255)         NOT NULL,
    [AggregateId]    NVARCHAR(255)         NOT NULL,
    [AggregateType]  NVARCHAR(255)         NOT NULL,
    [EventType]      NVARCHAR(255)         NOT NULL,
    -- Nullable: GDPR erasure tombstones an event by setting EventData to NULL
    -- while preserving its position. NOT NULL makes every erasure fail.
    [EventData]      VARBINARY(MAX)        NULL,
    [Metadata]       VARBINARY(MAX)        NULL,
    [Version]        BIGINT                NOT NULL,
    [Timestamp]      DATETIMEOFFSET        NOT NULL,
    [TenantId]       NVARCHAR(64)  COLLATE Latin1_General_BIN2         NOT NULL
        CONSTRAINT [DF_EventStoreEvents_TenantId] DEFAULT '__untenanted__',

    CONSTRAINT [PK_EventStoreEvents] PRIMARY KEY CLUSTERED ([Position]),
    CONSTRAINT [UQ_EventStoreEvents_Stream] UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId])
);
CREATE INDEX [IX_EventStoreEvents_AggregateId] ON [dbo].[EventStoreEvents]([AggregateId], [AggregateType]);
CREATE INDEX [IX_EventStoreEvents_EventType] ON [dbo].[EventStoreEvents]([EventType]);

-- Snapshots table (default: dbo.EventStoreSnapshots, configurable via SqlServerEventSourcingOptions)
CREATE TABLE [dbo].[EventStoreSnapshots] (
    [SnapshotId]     NVARCHAR(255)         NOT NULL,
    [AggregateId]    NVARCHAR(255)         NOT NULL,
    [AggregateType]  NVARCHAR(255)         NOT NULL,
    [Version]        BIGINT                NOT NULL,
    [Data]           VARBINARY(MAX)        NOT NULL,
    [CreatedAt]      DATETIMEOFFSET        NOT NULL,
    [Metadata]       VARBINARY(MAX)        NULL,
    -- Part of the primary key so two tenants holding the same aggregate identifier occupy
    -- separate rows instead of overwriting one another. NOT NULL and no default:
    -- SQL Server forbids a nullable column in a PRIMARY KEY, and the reserved '__untenanted__' sentinel is the single-tenant value the
    -- store writes explicitly — omitting it must fail the INSERT, not silently land in that partition.
    [TenantId]       NVARCHAR(64)  COLLATE Latin1_General_BIN2         NOT NULL,

    CONSTRAINT [PK_EventStoreSnapshots] PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType], [TenantId])
);

-- NO OUTBOX TABLES ARE CREATED HERE, DELIBERATELY. This sample does not use the outbox: it
-- consumes from transports and projects to ElasticSearch. Provisioning an outbox table it never
-- reads would leave you with a partial schema and no error to tell you so.
--
-- If you add the outbox to your own host, do NOT hand-copy its DDL. Run the provisioning script
-- shipped in the Excalibur.Outbox.SqlServer package (Scripts/001_CreateOutboxSchema.sql), which
-- creates ALL FOUR tables the store requires -- OutboxMessages, OutboxFence,
-- OutboxMessageTransports and DeadLetterQueue -- and is guarded so it is safe to re-run.
--
-- OutboxFence in particular is required even for a single instance that never elects a leader:
-- the drain statement names that table unconditionally, and SQL Server resolves object names when
-- it compiles the statement rather than when a predicate evaluates. Create only OutboxMessages and
-- every drain fails with 'Msg 208, Invalid object name', so nothing is ever delivered and the
-- messages accumulate silently.
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
