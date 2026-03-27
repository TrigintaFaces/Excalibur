// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// CDC + Event Store + Elasticsearch Sample
// ============================================================================
//
// This advanced sample demonstrates a production-like CQRS/Event Sourcing architecture:
//
//   ┌─────────────────────────────────────────────────────────────────────┐
//   │                     WRITE SIDE (Event Sourcing)                     │
//   └─────────────────────────────────────────────────────────────────────┘
//
//   SQL Server #1 (Legacy DB)         SQL Server #2 (Event Store)
//   Port 1433                          Port 1434
//   ┌───────────────────────┐         ┌───────────────────────────┐
//   │  LegacyCustomers      │         │  eventsourcing.Events     │
//   │  LegacyOrders         │         │  eventsourcing.Snapshots  │
//   │  LegacyOrderItems     │         └─────────────┬─────────────┘
//   │  (CDC enabled)        │                       │
//   └─────────┬─────────────┘                       │ Domain Events
//             │                                     ▲
//             │ CDC Polling Service                 │
//             │ (Background Service)               │
//             ▼                                     │
//   ┌─────────────────────────────────────────────────────────────────────┐
//   │                    Anti-Corruption Layer (ACL)                       │
//   │  ┌──────────────────┐  ┌───────────────────┐  ┌──────────────────┐  │
//   │  │  LegacyAdapters  │─▶│  ChangeHandlers   │─▶│    Aggregates    │  │
//   │  │ (schema compat)  │  │ (translate CDC    │  │  (domain logic)  │  │
//   │  │                  │  │  to commands)     │  │                  │  │
//   │  └──────────────────┘  └───────────────────┘  └──────────────────┘  │
//   └─────────────────────────────────────────────────────────────────────┘
//
//   ┌─────────────────────────────────────────────────────────────────────┐
//   │                     READ SIDE (Projections + API)                   │
//   └─────────────────────────────────────────────────────────────────────┘
//
//                    Domain Events
//                         │
//                         ▼
//             ┌─────────────────────────────┐
//             │ Projection Background Service│
//             │ (Async Event Processing)     │
//             └──────────────┬──────────────┘
//                            │
//                            ▼
//   ┌─────────────────────────────────────────────────────────────────────┐
//   │                      Elasticsearch Cluster                          │
//   │                          Port 9200                                  │
//   │  ┌─────────────────────┐  ┌───────────────────┐  ┌───────────────┐  │
//   │  │CustomerSearchProj   │  │OrderSearchProj    │  │AnalyticsProj  │  │
//   │  │TierSummaryProj      │  │DailySummaryProj   │  │               │  │
//   │  └─────────────────────┘  └───────────────────┘  └───────────────┘  │
//   └─────────────────────────────────────────────────────────────────────┘
//                            │
//                            ▼
//   ┌─────────────────────────────────────────────────────────────────────┐
//   │                         ASP.NET Core API                            │
//   │                          Port 5000                                  │
//   │  ┌─────────────────────┐  ┌───────────────────┐  ┌───────────────┐  │
//   │  │ /api/customers      │  │ /api/orders       │  │/api/analytics │  │
//   │  │ Search, filter, get │  │ Search, filter    │  │Dashboards,    │  │
//   │  │ by tier             │  │ by status         │  │tier summaries │  │
//   │  └─────────────────────┘  └───────────────────┘  └───────────────┘  │
//   └─────────────────────────────────────────────────────────────────────┘
//
// Key Features:
// - CDC processing via Background Service
// - Multiple tables: LegacyCustomers, LegacyOrders, LegacyOrderItems
// - Production-grade stale position recovery
// - Real SQL Server event store with snapshots
// - Real Elasticsearch projections
// - ASP.NET Core Web API with Swagger
//
// Prerequisites:
// 1. Start infrastructure: docker-compose up -d
// 2. Run database scripts: scripts/01-*.sql through scripts/04-*.sql
// 3. Run the sample: dotnet run
//
// ============================================================================

using CdcEventStoreElasticsearch.AntiCorruption;
using CdcEventStoreElasticsearch.Infrastructure;
using CdcEventStoreElasticsearch.Projections;
using CdcEventStoreElasticsearch.Repositories;

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Excalibur.Cdc.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// Connection Strings
// ============================================================================

var cdcSourceConnectionString = builder.Configuration.GetConnectionString("CdcSource")
								??
								"Server=localhost,1433;Database=LegacyDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

var eventStoreConnectionString = builder.Configuration.GetConnectionString("EventStore")
								 ??
								 "Server=localhost,1434;Database=EventStore;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

var elasticsearchUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";

// Use the host's built-in bootstrap logger for startup messages
using var startupLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
var startupLogger = startupLoggerFactory.CreateLogger("Startup");
startupLogger.LogInformation("CDC + Event Store + Elasticsearch Sample - Production Configuration with Web API");
startupLogger.LogInformation("SQL Server #1 (CDC Source): localhost:1433");
startupLogger.LogInformation("SQL Server #2 (Event Store): localhost:1434");
startupLogger.LogInformation("Elasticsearch: {ElasticsearchUri}", elasticsearchUri);

// ============================================================================
// ASP.NET Core Web API
// ============================================================================

builder.Services
	.AddControllers();

builder.Services
	.AddEndpointsApiExplorer()
	.AddSwaggerGen(options =>
	{
		options.SwaggerDoc("v1",
			new Microsoft.OpenApi.Models.OpenApiInfo
			{
				Title = "CDC Event Store Elasticsearch Sample API",
				Version = "v1",
				Description = "Sample API demonstrating CQRS/Event Sourcing with CDC, SQL Server, and Elasticsearch"
			});
	});

// ============================================================================
// Dispatch Messaging
// ============================================================================

builder.Services
	.AddDispatch(typeof(Program).Assembly)
	.AddSingleton<IEventSerializer, JsonEventSerializer>();

// ============================================================================
// SQL Server Event Store
// ============================================================================

builder.Services.AddSqlServerEventSourcing(options =>
{
	options.ConnectionString = eventStoreConnectionString;
	options.HealthChecks.RegisterHealthChecks = true;
});

// ============================================================================
// CDC Processing (Fluent Builder Pattern)
// ============================================================================

// Logger for CDC recovery callback (assigned after app.Build())
ILogger? cdcRecoveryLogger = null;

// Configure CDC with fluent builder - per ADR-098 P1 (Single Entry Point)
builder.Services.AddCdcProcessor(cdc =>
{
	cdc.UseSqlServer(sql =>
		{
			sql.ConnectionString(cdcSourceConnectionString)
				.DatabaseName("LegacyDb")
				.DatabaseConnectionIdentifier("cdc-LegacyDb")
				.StateConnectionIdentifier("state-LegacyDb")
				.CaptureInstances("dbo_LegacyCustomers", "dbo_LegacyOrders", "dbo_LegacyOrderItems")
				.StopOnMissingTableHandler(false)
				.PollingInterval(TimeSpan.FromSeconds(5))
				.BatchSize(100);
		})
		.WithRecovery(recovery =>
		{
			recovery.Strategy(Excalibur.Cdc.StalePositionRecoveryStrategy.FallbackToEarliest)
				.MaxAttempts(3)
				.AttemptDelay(TimeSpan.FromSeconds(5))
				.OnPositionReset((args, _) =>
				{
					cdcRecoveryLogger?.LogWarning(
						"CDC position reset: Stale={StalePosition}, New={NewPosition}, Reason={ReasonCode}, CaptureInstance={CaptureInstance}",
						FormatLsn(args.StalePosition),
						FormatLsn(args.NewPosition),
						args.ReasonCode,
						args.CaptureInstance);
					return Task.CompletedTask;

					static string FormatLsn(byte[]? lsn) =>
						lsn != null ? BitConverter.ToString(lsn) : "null";
				})
				.EnableStructuredLogging();
		});
	// Note: Don't call EnableBackgroundProcessing() - we use CdcPollingBackgroundService below
});

// Register SQL services (CDC processor already registered above via AddCdcProcessor builder)
builder.Services
	.AddExcaliburSqlServices();

// CDC polling options (bound from configuration with defaults)
builder.Services.Configure<CdcPollingOptions>(builder.Configuration.GetSection(CdcPollingOptions.SectionName));
builder.Services.PostConfigure<CdcPollingOptions>(options =>
{
	options.CdcSourceConnectionString ??= cdcSourceConnectionString;
	options.StateStoreConnectionString ??= eventStoreConnectionString;
});

// ============================================================================
// Anti-Corruption Layer
// ============================================================================

// Customer ACL adapters and lookup services
builder.Services
	.AddSingleton<LegacyCustomerAdapter>()
	.AddSingleton<ICustomerLookupService, InMemoryCustomerLookupService>();

// Order ACL adapters and lookup services
builder.Services
	.AddSingleton<LegacyOrderAdapter>()
	.AddSingleton<LegacyOrderItemAdapter>()
	.AddSingleton<IOrderLookupService, InMemoryOrderLookupService>()
	.AddSingleton<IOrderItemLookupService, InMemoryOrderItemLookupService>();

// ============================================================================
// CDC Background Processing
// ============================================================================
// For Quartz-based scheduling, see the CdcJobQuartz sample in samples/13-jobs.

builder.Services.AddHostedService<CdcPollingBackgroundService>();

// ============================================================================
// Elasticsearch Projections
// ============================================================================

// Customer projections
builder.Services
	.AddElasticSearchProjectionStore<CustomerSearchProjection>(elasticsearchUri, options =>
	{
		options.IndexPrefix = "customers";
		options.CreateIndexOnInitialize = true;
		options.NumberOfShards = 1;
		options.NumberOfReplicas = 0;
	})
	.AddElasticSearchProjectionStore<CustomerTierSummaryProjection>(elasticsearchUri, options =>
	{
		options.IndexPrefix = "customers";
		options.CreateIndexOnInitialize = true;
	});

// Order projections
builder.Services
	.AddElasticSearchProjectionStore<OrderSearchProjection>(elasticsearchUri, options =>
	{
		options.IndexPrefix = "orders";
		options.CreateIndexOnInitialize = true;
		options.NumberOfShards = 1;
		options.NumberOfReplicas = 0;
	});

// Analytics projections
builder.Services
	.AddElasticSearchProjectionStore<OrderAnalyticsProjection>(elasticsearchUri, options =>
	{
		options.IndexPrefix = "analytics";
		options.CreateIndexOnInitialize = true;
	})
	.AddElasticSearchProjectionStore<DailyOrderSummaryProjection>(elasticsearchUri, options =>
	{
		options.IndexPrefix = "analytics";
		options.CreateIndexOnInitialize = true;
	});

// ============================================================================
// Custom Elasticsearch Repository (Native Query Features)
// ============================================================================
// The OrderFullTextSearchRepository extends ElasticRepositoryBase<T> for native
// ES queries (full-text search, aggregations, fuzzy matching) while targeting
// the SAME index as IProjectionStore<OrderSearchProjection> above.
//
// ElasticSearchProjectionIndexConvention resolves the index name from the same
// options, so if you change the IndexPrefix both paths stay in sync.

builder.Services.AddSingleton<ElasticsearchClient>(_ =>
{
	var settings = new ElasticsearchClientSettings(new Uri(elasticsearchUri));
	return new ElasticsearchClient(settings);
});

builder.Services.AddScoped<OrderFullTextSearchRepository>();

// Projection handlers (auto-discovered from assembly via IProjectionHandler marker)
builder.Services.AddProjectionHandlersFromAssembly(typeof(Program).Assembly);

// Projection processing options
builder.Services.Configure<ProjectionOptions>(builder.Configuration.GetSection("Projections"));

// Projection background service
builder.Services.AddHostedService<ProjectionBackgroundService>();

// ============================================================================
// Build and Configure Application
// ============================================================================

startupLogger.LogInformation("Building application...");

var app = builder.Build();

// Switch to the host's logger now that the service provider is available
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

// Assign the CDC recovery logger now that the service provider is available
cdcRecoveryLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CdcRecovery");

// Configure HTTP pipeline
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Api:EnableSwagger"))
{
	_ = app.UseSwagger();
	_ = app.UseSwaggerUI(options =>
	{
		options.SwaggerEndpoint("/swagger/v1/swagger.json", "CDC Event Store Elasticsearch Sample API v1");
		options.RoutePrefix = "swagger";
	});
}

app.UseRouting();
app.MapControllers();

// ============================================================================
// Start Application
// ============================================================================

var apiPort = builder.Configuration["Api:Port"] ?? "5000";
logger.LogInformation(
	"Application running. API=http://localhost:{Port}, Swagger=http://localhost:{Port}/swagger",
	apiPort,
	apiPort);
logger.LogInformation("Endpoints: GET /api/customers, GET /api/orders, GET /api/orders/search?q=..., GET /api/orders/statistics, GET /api/analytics/dashboard");

await app.RunAsync().ConfigureAwait(false);
