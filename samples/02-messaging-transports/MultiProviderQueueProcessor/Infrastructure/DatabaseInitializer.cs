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
-- Events table
CREATE TABLE [dbo].[Events] (
    [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [AggregateId] NVARCHAR(256) NOT NULL,
    [AggregateType] NVARCHAR(256) NOT NULL,
    [EventType] NVARCHAR(512) NOT NULL,
    [EventId] UNIQUEIDENTIFIER NOT NULL,
    [Version] BIGINT NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    [Metadata] NVARCHAR(MAX) NULL,
    [OccurredAt] DATETIMEOFFSET NOT NULL,
    [CreatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [UQ_Events_AggregateId_Version] UNIQUE ([AggregateId], [AggregateType], [Version])
);
CREATE INDEX [IX_Events_AggregateId] ON [dbo].[Events]([AggregateId], [AggregateType]);

-- Snapshots table
CREATE TABLE [dbo].[Snapshots] (
    [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [AggregateId] NVARCHAR(256) NOT NULL,
    [AggregateType] NVARCHAR(256) NOT NULL,
    [Version] BIGINT NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    [CreatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [UQ_Snapshots_AggregateId] UNIQUE ([AggregateId], [AggregateType])
);

-- Outbox table
CREATE TABLE [dbo].[EventSourcedOutbox] (
    [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [AggregateId] NVARCHAR(256) NOT NULL,
    [AggregateType] NVARCHAR(256) NOT NULL,
    [EventType] NVARCHAR(512) NOT NULL,
    [EventId] UNIQUEIDENTIFIER NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    [Metadata] NVARCHAR(MAX) NULL,
    [PublishedAt] DATETIMEOFFSET NULL,
    [RetryCount] INT NOT NULL DEFAULT 0,
    [CreatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX [IX_Outbox_Unpublished] ON [dbo].[EventSourcedOutbox]([PublishedAt]) WHERE [PublishedAt] IS NULL;
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
