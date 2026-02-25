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
//             │ (Background Service or Quartz Job)  │
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
// - CDC processing: Background Service OR Quartz Job (configurable)
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

using Excalibur.Data.SqlServer.Cdc;

using Quartz;

Console.WriteLine("=================================================");
Console.WriteLine("  CDC + Event Store + Elasticsearch Sample");
Console.WriteLine("  Production Configuration with Web API");
Console.WriteLine("=================================================");
Console.WriteLine();

// ============================================================================
// Build WebApplication
// ============================================================================

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

Console.WriteLine("[Infrastructure] SQL Server #1 (CDC Source): localhost:1433");
Console.WriteLine("[Infrastructure] SQL Server #2 (Event Store): localhost:1434");
Console.WriteLine($"[Infrastructure] Elasticsearch: {elasticsearchUri}");

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
	options.RegisterHealthChecks = true;
});

// ============================================================================
// CDC Processing (Fluent Builder Pattern)
// ============================================================================

// Configure CDC with fluent builder - per ADR-098 P1 (Single Entry Point)
builder.Services.AddCdcProcessor(cdc =>
{
	cdc.UseSqlServer(cdcSourceConnectionString, sql =>
		{
			sql.PollingInterval(TimeSpan.FromSeconds(5))
				.BatchSize(100);
		})
		.WithRecovery(recovery =>
		{
			recovery.Strategy(Excalibur.Cdc.StalePositionRecoveryStrategy.FallbackToEarliest)
				.MaxAttempts(3)
				.AttemptDelay(TimeSpan.FromSeconds(5))
				.OnPositionReset((args, _) =>
				{
					Console.WriteLine(
						$"[CDC Recovery] Position reset: Stale={FormatLsn(args.StalePosition)}, " +
						$"New={FormatLsn(args.NewPosition)}, Reason={args.ReasonCode}, " +
						$"CaptureInstance={args.CaptureInstance}");
					return Task.CompletedTask;

					static string FormatLsn(byte[]? lsn) =>
						lsn != null ? BitConverter.ToString(lsn) : "null";
				})
				.EnableStructuredLogging();
		});
	// Note: Don't call EnableBackgroundProcessing() - we use custom service/Quartz toggle below
});

// Register IDataChangeHandler implementations from this assembly
builder.Services
	.AddExcaliburSqlServices()
	.AddCdcProcessor(typeof(Program).Assembly);

// CDC polling options (bound from configuration with defaults)
builder.Services.Configure<CdcPollingOptions>(builder.Configuration.GetSection(CdcPollingOptions.SectionName));
builder.Services.PostConfigure<CdcPollingOptions>(options =>
{
	options.CdcSourceConnectionString ??= cdcSourceConnectionString;
	options.StateStoreConnectionString ??= eventStoreConnectionString;
});

// CDC Quartz job options (bound from configuration with defaults)
builder.Services.Configure<CdcSampleJobConfig>(builder.Configuration.GetSection(CdcSampleJobConfig.SectionName));
builder.Services.PostConfigure<CdcSampleJobConfig>(options =>
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

// CDC Database Configuration (required by IDataChangeEventProcessorFactory)
builder.Services.AddSingleton<IDatabaseConfig>(_ => new SampleCdcDatabaseConfig
{
	DatabaseName = "LegacyDb",
	DatabaseConnectionIdentifier = "cdc-LegacyDb",
	StateConnectionIdentifier = "state-LegacyDb",
	CaptureInstances = ["dbo_LegacyCustomers", "dbo_LegacyOrders", "dbo_LegacyOrderItems"],
	StopOnMissingTableHandler = false // Production: skip unknown tables
});

// ============================================================================
// CDC Processing Mode (Background Service or Quartz)
// ============================================================================

var useQuartzScheduler = builder.Configuration.GetValue<bool>("Jobs:UseQuartzScheduler");

if (useQuartzScheduler)
{
	Console.WriteLine("[CDC] Using Quartz Scheduler mode");

	builder.Services.AddQuartz(q => CdcSampleJob.ConfigureJob(q, builder.Configuration));
	builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
}
else
{
	Console.WriteLine("[CDC] Using Background Service mode");

	builder.Services.AddHostedService<CdcPollingBackgroundService>();
}

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

// Projection handlers
builder.Services
	.AddSingleton<CustomerSearchProjectionHandler>()
	.AddSingleton<CustomerTierSummaryProjectionHandler>()
	.AddSingleton<OrderSearchProjectionHandler>()
	.AddSingleton<OrderAnalyticsProjectionHandler>();

// Projection processing options
builder.Services.Configure<ProjectionOptions>(builder.Configuration.GetSection("Projections"));

// Projection background service
builder.Services.AddHostedService<ProjectionBackgroundService>();

// ============================================================================
// Build and Configure Application
// ============================================================================

Console.WriteLine();
Console.WriteLine("[Startup] Building application...");

var app = builder.Build();

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

Console.WriteLine("[Startup] Starting background services and Web API...");
Console.WriteLine();

Console.WriteLine("=================================================");
Console.WriteLine("  Application Running");
Console.WriteLine("=================================================");
Console.WriteLine();
Console.WriteLine($"  CDC Processing:    {(useQuartzScheduler ? "Quartz Scheduler" : "Background Service")}");
Console.WriteLine($"  Web API:           http://localhost:{builder.Configuration["Api:Port"] ?? "5000"}");
Console.WriteLine($"  Swagger UI:        http://localhost:{builder.Configuration["Api:Port"] ?? "5000"}/swagger");
Console.WriteLine();
Console.WriteLine("API Endpoints:");
Console.WriteLine("  GET /api/customers              - Search customers");
Console.WriteLine("  GET /api/orders                 - Search orders");
Console.WriteLine("  GET /api/analytics/dashboard    - Combined dashboard");
Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine("  Ready - Press Ctrl+C to Stop");
Console.WriteLine("=================================================");
Console.WriteLine();

await app.RunAsync().ConfigureAwait(false);
