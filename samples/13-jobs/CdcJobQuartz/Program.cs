// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// CDC Job with Quartz.NET Sample
// ============================================================================
//
// This sample demonstrates using CdcJob from Excalibur.Jobs for scheduled
// CDC processing via Quartz.NET instead of a background service.
//
// Architecture (same as background service sample):
//
//   ┌─────────────────────────────────────────────────────────────────────┐
//   │                     WRITE SIDE (Event Sourcing)                     │
//   └─────────────────────────────────────────────────────────────────────┘
//
//   SQL Server #1 (Legacy DB)         SQL Server #2 (Event Store)
//   Port 1433                          Port 1434
//   ┌───────────────────┐             ┌───────────────────────────┐
//   │  LegacyCustomers  │             │  eventsourcing.Events     │
//   │  (CDC enabled)    │             │  eventsourcing.Snapshots  │
//   └─────────┬─────────┘             └─────────────┬─────────────┘
//             │                                     │
//             │ CdcJob (Quartz.NET)                 │ Domain Events
//             │ (Cron-scheduled)                    ▲
//             ▼                                     │
//   ┌─────────────────────────────────────────────────────────────────────┐
//   │                    Anti-Corruption Layer (ACL)                       │
//   │  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐  │
//   │  │ LegacyCustomer  │───▶│  CdcChangeHandler│───▶│ CustomerAggregate│ │
//   │  │    Adapter      │    │ (translate CDC   │    │ (domain logic)   │ │
//   │  │ (schema compat) │    │  to commands)    │    │                  │ │
//   │  └─────────────────┘    └─────────────────┘    └─────────────────┘  │
//   └─────────────────────────────────────────────────────────────────────┘
//
//   ┌─────────────────────────────────────────────────────────────────────┐
//   │                     READ SIDE (Projections)                         │
//   └─────────────────────────────────────────────────────────────────────┘
//
//                    Domain Events
//                         │
//                         ▼
//             ┌─────────────────────────────┐
//             │ Projection Job (Quartz.NET)  │
//             │ (Cron-scheduled)             │
//             └──────────────┬──────────────┘
//                            │
//                            ▼
//   ┌─────────────────────────────────────────────────────────────────────┐
//   │                      Elasticsearch Cluster                          │
//   │                          Port 9200                                  │
//   │  ┌───────────────────────┐    ┌──────────────────────────────────┐  │
//   │  │ CustomerSearchProjection│   │ CustomerTierSummaryProjection   │  │
//   │  │ (full-text search)     │   │ (analytics/materialized view)   │  │
//   │  └───────────────────────┘    └──────────────────────────────────┘  │
//   └─────────────────────────────────────────────────────────────────────┘
//
// Key Differences from Background Service Sample:
// - Uses Quartz.NET CdcJob instead of custom BackgroundService
// - Job scheduling via cron expressions
// - Built-in health checks and monitoring
// - Supports multiple database configurations
// - Framework-managed job configuration via appsettings.json
//
// Prerequisites:
// 1. Start infrastructure: docker-compose up -d
// 2. Initialize databases: ./scripts/setup-databases.sh
// 3. Run the sample: dotnet run
//
// ============================================================================

using CdcJobQuartz.AntiCorruption;
using CdcJobQuartz.Infrastructure;
using CdcJobQuartz.Projections;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.Jobs.Cdc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

Console.WriteLine("================================================");
Console.WriteLine("  CDC Job with Quartz.NET Sample");
Console.WriteLine("  Cron-Scheduled CDC Processing");
Console.WriteLine("================================================");
Console.WriteLine();

// ============================================================================
// Step 1: Build Host with Quartz.NET Job Configuration
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure logging
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Information);
});

// ============================================================================
// Step 2: Configure Dispatch Messaging
// ============================================================================

builder.Services.AddDispatch(typeof(Program).Assembly);
builder.Services.AddSingleton<IEventSerializer, JsonEventSerializer>();

// ============================================================================
// Step 3: Configure SQL Server Event Store (Port 1434)
// ============================================================================

var eventStoreConnectionString = builder.Configuration.GetConnectionString("EventStore")
								 ??
								 "Server=localhost,1434;Database=EventStore;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

Console.WriteLine("[Infrastructure] SQL Server #2 (Event Store): localhost:1434");

// Register SQL Server Event Sourcing (Event Store + Snapshot Store + Outbox Store)
builder.Services.AddSqlServerEventSourcing(options =>
{
	options.ConnectionString = eventStoreConnectionString;
	options.RegisterHealthChecks = true;
});

// ============================================================================
// Step 4: Configure CDC Processing with Named Connections
// ============================================================================

var cdcSourceConnectionString = builder.Configuration.GetConnectionString("CdcSource")
								??
								"Server=localhost,1433;Database=LegacyDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

Console.WriteLine("[Infrastructure] SQL Server #1 (CDC Source): localhost:1433");

// Register SQL Server services (IDataAccessPolicyFactory, Dapper type handlers)
builder.Services.AddExcaliburSqlServices();

// Register CDC processor factory and scan for IDataChangeHandler implementations
// This makes IDataChangeEventProcessorFactory available for CdcJob
builder.Services.AddCdcProcessor(typeof(Program).Assembly);

// CdcJob resolves connections by identifier (DatabaseConnectionIdentifier, StateConnectionIdentifier)
// These identifiers map to connection strings in appsettings.json's ConnectionStrings section
// Example: "LegacyDbCdc" identifier -> ConnectionStrings:LegacyDbCdc

// Anti-Corruption Layer components
builder.Services.AddSingleton<LegacyCustomerAdapter>();
builder.Services.AddSingleton<ICustomerLookupService, InMemoryCustomerLookupService>();

// ============================================================================
// Step 5: Configure Quartz.NET Job Host with CdcJob
// ============================================================================

// The AddExcaliburJobHost() method:
// - Sets up Quartz.NET scheduler with sensible defaults
// - Configures job store (in-memory or persistent)
// - Registers health checks for job monitoring automatically
// - Allows custom job configuration via callback
builder.Services.AddExcaliburJobHost(
	configureQuartz: q =>
	{
		// Configure CdcJob using framework's static configuration method
		// This reads from appsettings.json "Jobs:CdcJob" section
		CdcJob.ConfigureJob(q, builder.Configuration);

		// Optionally configure additional jobs here
		// ProjectionJob.ConfigureJob(q, builder.Configuration);
	},
	typeof(Program).Assembly);

// ============================================================================
// Step 6: Configure Elasticsearch Projections (Port 9200)
// ============================================================================

var elasticsearchUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
Console.WriteLine($"[Infrastructure] Elasticsearch: {elasticsearchUri}");

builder.Services.Configure<ElasticSearchProjectionStoreOptions>(options =>
{
	options.NodeUri = elasticsearchUri;
	options.IndexPrefix = "customers";
	options.CreateIndexOnInitialize = true;
	options.NumberOfShards = 1;
	options.NumberOfReplicas = 0; // Dev mode - use 1+ in production
	options.RefreshInterval = "1s";
});

// Register Elasticsearch projection stores
builder.Services.AddSingleton<IProjectionStore<CustomerSearchProjection>>(sp =>
{
	var options = sp.GetRequiredService<IOptions<ElasticSearchProjectionStoreOptions>>();
	var logger = sp.GetRequiredService<ILogger<ElasticSearchProjectionStore<CustomerSearchProjection>>>();
	return new ElasticSearchProjectionStore<CustomerSearchProjection>(options, logger);
});

builder.Services.AddSingleton<IProjectionStore<CustomerTierSummaryProjection>>(sp =>
{
	var options = sp.GetRequiredService<IOptions<ElasticSearchProjectionStoreOptions>>();
	var logger = sp.GetRequiredService<ILogger<ElasticSearchProjectionStore<CustomerTierSummaryProjection>>>();
	return new ElasticSearchProjectionStore<CustomerTierSummaryProjection>(options, logger);
});

// Register projection handlers
builder.Services.AddSingleton<CustomerSearchProjectionHandler>();
builder.Services.AddSingleton<CustomerTierSummaryProjectionHandler>();

// Configure projection processing options
builder.Services.Configure<ProjectionOptions>(options =>
{
	options.PollingInterval = TimeSpan.FromSeconds(1);
	options.BatchSize = 100;
	options.RebuildOnStartup = false; // Set to true to rebuild projections from event store
});

// Projection Background Service - Creates materialized views in Elasticsearch
// This processes domain events from the event store and updates:
// - CustomerSearchProjection: Full-text search index
// - CustomerTierSummaryProjection: Analytics aggregation by tier
builder.Services.AddHostedService<ProjectionBackgroundService>();

// ============================================================================
// Step 7: Build and Start Host
// ============================================================================

Console.WriteLine();
Console.WriteLine("[Startup] Building host with Quartz.NET scheduler...");

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

Console.WriteLine("[Startup] Starting Quartz.NET job scheduler...");
Console.WriteLine();

// Start the host
await host.StartAsync().ConfigureAwait(false);

Console.WriteLine("================================================");
Console.WriteLine("  Quartz.NET Job Scheduler Running");
Console.WriteLine("================================================");
Console.WriteLine();
Console.WriteLine("  CdcJob: Processing CDC changes on schedule");
Console.WriteLine($"  Schedule: {builder.Configuration["Jobs:CdcJob:CronSchedule"] ?? "0/5 * * * * ?"}");
Console.WriteLine();
Console.WriteLine("  Projection Service: Creating materialized views");
Console.WriteLine("  - CustomerSearchProjection (full-text search)");
Console.WriteLine("  - CustomerTierSummaryProjection (analytics)");
Console.WriteLine();
Console.WriteLine("Infrastructure Summary:");
Console.WriteLine($"  CDC Source:    {cdcSourceConnectionString.Split(';')[0]}");
Console.WriteLine($"  Event Store:   {eventStoreConnectionString.Split(';')[0]}");
Console.WriteLine($"  Elasticsearch: {elasticsearchUri}");
Console.WriteLine();

// ============================================================================
// Summary
// ============================================================================

Console.WriteLine("================================================");
Console.WriteLine("  Ready - Press Ctrl+C to Stop");
Console.WriteLine("================================================");
Console.WriteLine();
Console.WriteLine("The Quartz.NET scheduler is now:");
Console.WriteLine("  1. Running CdcJob on cron schedule (default: every 5 seconds)");
Console.WriteLine("  2. Processing CDC changes from SQL Server #1 (dbo.LegacyCustomers)");
Console.WriteLine("  3. Storing domain events in SQL Server #2 (Event Store)");
Console.WriteLine("  4. Projecting events to Elasticsearch indices");
Console.WriteLine();
Console.WriteLine("CDC Recovery Features (same as background service sample):");
Console.WriteLine("  - Automatic stale position detection");
Console.WriteLine("  - Recovery from backup restores");
Console.WriteLine("  - Recovery from CDC cleanup job purges");
Console.WriteLine("  - Configurable strategy via appsettings.json");
Console.WriteLine();
Console.WriteLine("Quartz.NET Features:");
Console.WriteLine("  - Cron expression scheduling (Jobs:CdcJob:CronSchedule)");
Console.WriteLine("  - DisallowConcurrentExecution (prevents overlapping runs)");
Console.WriteLine("  - Health check integration (Jobs:CdcJob:DegradedThreshold/UnhealthyThreshold)");
Console.WriteLine("  - Multiple database configurations per job");
Console.WriteLine();

// Wait for shutdown
await host.WaitForShutdownAsync().ConfigureAwait(false);

Console.WriteLine();
Console.WriteLine("Shutting down...");
