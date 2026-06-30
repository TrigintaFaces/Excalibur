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
//   +---------------------------------------------------------------------+
//   |                     WRITE SIDE (Event Sourcing)                     |
//   +---------------------------------------------------------------------+
//
//   SQL Server #1 (Legacy DB)         SQL Server #2 (Event Store)
//   Port 1433                          Port 1434
//   +-------------------+             +---------------------------+
//   |  LegacyCustomers  |             |  dbo.EventStoreEvents     |
//   |  (CDC enabled)    |             |  dbo.EventStoreSnapshots  |
//   +---------+---------+             +-------------+-------------+
//             |                                     |
//             | CdcJob (Quartz.NET)                 | Domain Events
//             | (Cron-scheduled)                    ^
//             v                                     |
//   +---------------------------------------------------------------------+
//   |                    Anti-Corruption Layer (ACL)                       |
//   |  +-----------------+    +-----------------+    +-----------------+  |
//   |  | LegacyCustomer  |--->|  CdcChangeHandler|--->| CustomerAggregate| |
//   |  |    Adapter      |    | (translate CDC   |    | (domain logic)   | |
//   |  | (schema compat) |    |  to commands)    |    |                  | |
//   |  +-----------------+    +-----------------+    +-----------------+  |
//   +---------------------------------------------------------------------+
//
//   +---------------------------------------------------------------------+
//   |                     READ SIDE (Projections)                         |
//   +---------------------------------------------------------------------+
//
//                    Domain Events
//                         |
//                         v
//             +-----------------------------+
//             | Projection Job (Quartz.NET)  |
//             | (Cron-scheduled)             |
//             +--------------+--------------+
//                            |
//                            v
//   +---------------------------------------------------------------------+
//   |                      Elasticsearch Cluster                          |
//   |                          Port 9200                                  |
//   |  +-----------------------+    +--------------------------------+   |
//   |  | CustomerSearchProjection|   | CustomerTierSummaryProjection |   |
//   |  | (full-text search)     |   | (analytics/materialized view) |   |
//   |  +-----------------------+    +--------------------------------+   |
//   +---------------------------------------------------------------------+
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
using CdcJobQuartz.Domain;
using CdcJobQuartz.Infrastructure;
using CdcJobQuartz.Projections;

using Excalibur.EventSourcing.SqlServer;
using Excalibur.Jobs.Cdc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
// Step 2: Configure Infrastructure Connections
// ============================================================================

var eventStoreConnectionString = builder.Configuration.GetConnectionString("EventStore")
								 ??
								 "Server=localhost,1434;Database=EventStore;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

Console.WriteLine("[Infrastructure] SQL Server #2 (Event Store): localhost:1434");

var cdcSourceConnectionString = builder.Configuration.GetConnectionString("CdcSource")
								??
								"Server=localhost,1433;Database=LegacyDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

Console.WriteLine("[Infrastructure] SQL Server #1 (CDC Source): localhost:1433");

var elasticsearchUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
Console.WriteLine($"[Infrastructure] Elasticsearch: {elasticsearchUri}");

// Register SQL Server services (IDataAccessPolicyFactory, Dapper type handlers)
builder.Services.AddExcaliburSqlServices();

// Register everything CdcJob needs: binds "Jobs:CdcJob" options and registers
// IDataChangeEventProcessorFactory (+ its SQL Server policy factory). Schedule the
// job itself below via CdcJob.ConfigureJob.
builder.Services.AddSqlServerCdcJob(builder.Configuration);

// CdcJob resolves connections by identifier (DatabaseConnectionIdentifier, StateConnectionIdentifier)
// These identifiers map to connection strings in appsettings.json's ConnectionStrings section
// Example: "LegacyDbCdc" identifier -> ConnectionStrings:LegacyDbCdc

// Anti-Corruption Layer components
builder.Services.AddSingleton<LegacyCustomerAdapter>();
builder.Services.AddSingleton<ICustomerLookupService, InMemoryCustomerLookupService>();

// Register Elasticsearch projection stores using per-projection named options.
// Each projection type gets its own isolated options (fix).
builder.Services.AddElasticSearchProjections(elasticsearchUri, projections =>
{
	projections.Add<CustomerSearchProjection>(options =>
	{
		options.IndexPrefix = "customers";
		options.CreateIndexOnInitialize = true;
		options.NumberOfShards = 1;
		options.NumberOfReplicas = 0; // Dev mode - use 1+ in production
		options.RefreshInterval = "1s";
	});

	projections.Add<CustomerTierSummaryProjection>(options =>
	{
		options.IndexPrefix = "customers";
		options.CreateIndexOnInitialize = true;
		options.NumberOfShards = 1;
		options.NumberOfReplicas = 0;
		options.RefreshInterval = "1s";
	});
});

// ============================================================================
// Step 3: Configure Excalibur (Single Composition Root)
// ============================================================================
// All Excalibur subsystems are composed under a single AddExcalibur root:
// - Dispatch messaging (handler discovery via ScanAssemblies)
// - Quartz.NET job scheduling (CdcJob)
// - Event sourcing (SQL Server + inline projections)
// Canonical composition path per / (A13).

builder.Services.AddSingleton<IEventSerializer, JsonEventSerializer>();

// c6wd6f: register event types so the FRAMEWORK's event-sourcing serializer (used by
// IEventSourcedRepository load/replay) resolves them securely without the scan. The AddSingleton above
// is this sample's OWN CDC IEventSerializer (CdcJobQuartz.Infrastructure) — a distinct type; both coexist.
builder.Services.AddEventTypesFromAssembly(typeof(Program).Assembly);

builder.Services.AddExcalibur(excalibur => excalibur
	// Discover handlers and validators from the application assembly
	.ScanAssemblies(typeof(Program).Assembly)

	// Quartz.NET job host: scheduler, job store, health checks
	.AddJobs(
		configureQuartz: q =>
		{
			// Configure CdcJob using framework's static configuration method
			// This reads from appsettings.json "Jobs:CdcJob" section
			CdcJob.ConfigureJob(q, builder.Configuration);

			// Optionally configure additional jobs here
			// ProjectionJob.ConfigureJob(q, builder.Configuration);
		},
		typeof(Program).Assembly)

	// Event sourcing: SQL Server store + inline projection handlers
	.AddEventSourcing(es =>
	{
		// SQL Server event store, snapshot store, outbox store + health checks
		es.UseSqlServer(sql =>
		{
			sql.ConnectionString(eventStoreConnectionString);
		});

		// CustomerSearchProjection: uses IProjectionEventHandler<T, TEvent> classes
		es.AddProjection<CustomerSearchProjection>(p => p
			.Inline()
			.WhenHandledBy<CustomerCreated, CustomerCreatedHandler>()
			.WhenHandledBy<CustomerInfoUpdated, CustomerInfoUpdatedHandler>()
			.WhenHandledBy<CustomerOrderPlaced, CustomerOrderPlacedHandler>()
			.When<CustomerDeactivated>((proj, _) =>
			{
				proj.IsActive = false;
				if (!proj.Tags.Contains("deactivated"))
				{
					proj.Tags.Add("deactivated");
				}
			}));

		// CustomerTierSummaryProjection: uses OverrideProjectionId for per-tier routing
		es.AddProjection<CustomerTierSummaryProjection>(p => p
			.Inline()
			.WhenHandledBy<CustomerCreated, CustomerTierSummaryCreatedHandler>());
	}));

// ============================================================================
// Step 4: Build and Start Host
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
