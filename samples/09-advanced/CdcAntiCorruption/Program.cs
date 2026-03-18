// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcAntiCorruption.Commands;
using CdcAntiCorruption.Configuration;
using CdcAntiCorruption.Backfill;

using Excalibur.Cdc.SqlServer;
using Excalibur.Data.DataProcessing;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ============================================================================
// CDC Anti-Corruption Layer Example
// ============================================================================
//
// This example demonstrates the Anti-Corruption Layer (ACL) pattern for CDC:
//
//   CDC Source (Legacy DB)
//        │
//        ▼
//   DataChangeEvent (raw CDC)
//        │
//        ▼
//   ┌─────────────────────────────────────┐
//   │    Anti-Corruption Layer (ACL)      │
//   │                                     │
//   │  ┌─────────────────────────────┐    │
//   │  │   LegacySchemaAdapter       │    │  ← Handles schema evolution
//   │  │   (column renames, types)   │    │
//   │  └─────────────────────────────┘    │
//   │                │                    │
//   │                ▼                    │
//   │  ┌─────────────────────────────┐    │
//   │  │   CustomerSyncHandler       │    │  ← Translates to commands
//   │  │   (boundary validation)     │    │
//   │  └─────────────────────────────┘    │
//   └─────────────────────────────────────┘
//        │
//        ▼
//   Domain Commands (SyncCustomer, UpdateCustomer, DeactivateCmd)
//        │
//        ▼
//   Dispatcher → Business Logic
//
// ============================================================================

// Build the host with all services
var builder = Host.CreateApplicationBuilder(args);

// Add Excalibur framework
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Add CDC anti-corruption layer
builder.Services.AddCdcAntiCorruptionLayer();

// Add DataProcessing-based backfill/replay path for CDC history gaps.
// In production, use SqlLegacyCustomerSnapshotSource with a connection factory:
//
//   var legacyConnectionString = builder.Configuration.GetConnectionString("LegacyDb")
//       ?? "Server=localhost,1433;Database=LegacyDb;...";
//   builder.Services.AddSingleton<ILegacyCustomerSnapshotSource>(
//       new SqlLegacyCustomerSnapshotSource(() => new SqlConnection(legacyConnectionString)));
//
// Each source gets its own Func<SqlConnection> factory — naturally supports multi-database
// scenarios where processors read from different servers. ADO.NET pools connections per
// connection string automatically. This is the same pattern used by SqlServerSnapshotStore,
// SqlServerProjectionStore, SqlServerInboxStore, and CdcProcessor throughout the framework.
//
// For this demo, use the in-memory source:
builder.Services.AddSingleton<ILegacyCustomerSnapshotSource, InMemoryLegacyCustomerSnapshotSource>();
builder.Services.AddDataProcessor<CustomerHistoryBackfillProcessor>(new DataProcessingConfiguration
{
	QueueSize = 128,
	ProducerBatchSize = 50,
	ConsumerBatchSize = 20,
});
builder.Services.AddRecordHandler<CustomerHistoryRecordHandler, LegacyCustomerSnapshot>();

// Note: Command handlers (ExampleSyncCustomerHandler, etc.) are auto-discovered
// by AddHandlersFromAssembly above — no manual registration needed.

// Configure logging
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Debug);
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CdcAntiCorruption");
logger.LogInformation("CDC Anti-Corruption Layer Example");

// Demonstrate the anti-corruption layer
await DemonstrateAntiCorruptionLayerAsync(host.Services, logger);

logger.LogInformation("Example complete");

// ============================================================================
// Demonstration
// ============================================================================

static async Task DemonstrateAntiCorruptionLayerAsync(IServiceProvider services, ILogger logger)
{
	using var scope = services.CreateScope();
	var handler = scope.ServiceProvider.GetRequiredService<IDataChangeHandler>();

	logger.LogInformation("Simulating CDC events...");

	// Simulate INSERT (legacy schema V1)
	logger.LogInformation("1. INSERT event (Legacy V1 schema: CustomerName, CustId)");
	var insertEvent = CreateMockInsertEvent();
	await handler.HandleAsync(insertEvent, CancellationToken.None);

	// Simulate UPDATE (current schema)
	logger.LogInformation("2. UPDATE event (Current schema: Name, ExternalId)");
	var updateEvent = CreateMockUpdateEvent();
	await handler.HandleAsync(updateEvent, CancellationToken.None);

	// Simulate DELETE
	logger.LogInformation("3. DELETE event (Soft delete -> Deactivate)");
	var deleteEvent = CreateMockDeleteEvent();
	await handler.HandleAsync(deleteEvent, CancellationToken.None);

	logger.LogInformation("4. CDC gap recovery (historical replay via Excalibur.Data.DataProcessing)");
	await DemonstrateCdcHistoryReplayAsync(services, logger);
}

static async Task DemonstrateCdcHistoryReplayAsync(IServiceProvider services, ILogger logger)
{
	using var scope = services.CreateScope();
	var processor = scope.ServiceProvider.GetRequiredService<CustomerHistoryBackfillProcessor>();

	try
	{
		var replayed = await processor.RunAsync(
			completedCount: 0,
			updateCompletedCount: static (_, _) => Task.CompletedTask,
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		logger.LogInformation("Replayed {ReplayedCount} historical records to close missing CDC history", replayed);
	}
	finally
	{
		await processor.DisposeAsync().ConfigureAwait(false);
	}
}

static DataChangeEvent CreateMockInsertEvent()
{
	return new DataChangeEvent
	{
		TableName = "LegacyCustomers",
		ChangeType = DataChangeType.Insert,
		CommitTime = DateTime.UtcNow,
		Lsn = [0x00, 0x00, 0x00, 0x01],
		SeqVal = [0x00, 0x00, 0x00, 0x01],
		Changes =
		[
			// Legacy V1 column names
			new DataChange { ColumnName = "CustId", NewValue = "CUST-001", DataType = typeof(string) },
			new DataChange { ColumnName = "CustomerName", NewValue = "John Smith", DataType = typeof(string) },
			new DataChange { ColumnName = "Email", NewValue = "john@example.com", DataType = typeof(string) },
		],
	};
}

static DataChangeEvent CreateMockUpdateEvent()
{
	return new DataChangeEvent
	{
		TableName = "LegacyCustomers",
		ChangeType = DataChangeType.Update,
		CommitTime = DateTime.UtcNow,
		Lsn = [0x00, 0x00, 0x00, 0x02],
		SeqVal = [0x00, 0x00, 0x00, 0x01],
		Changes =
		[
			// Current schema column names
			new DataChange { ColumnName = "ExternalId", OldValue = "CUST-001", NewValue = "CUST-001", DataType = typeof(string) },
			new DataChange { ColumnName = "Name", OldValue = "John Smith", NewValue = "John D. Smith", DataType = typeof(string) },
			new DataChange
			{
				ColumnName = "Email", OldValue = "john@example.com", NewValue = "john.smith@example.com", DataType = typeof(string)
			},
			new DataChange { ColumnName = "Phone", OldValue = null, NewValue = "+1-555-0123", DataType = typeof(string) },
		],
	};
}

static DataChangeEvent CreateMockDeleteEvent()
{
	return new DataChangeEvent
	{
		TableName = "LegacyCustomers",
		ChangeType = DataChangeType.Delete,
		CommitTime = DateTime.UtcNow,
		Lsn = [0x00, 0x00, 0x00, 0x03],
		SeqVal = [0x00, 0x00, 0x00, 0x01],
		Changes =
		[
			new DataChange { ColumnName = "ExternalId", OldValue = "CUST-001", DataType = typeof(string) },
			new DataChange { ColumnName = "Name", OldValue = "John D. Smith", DataType = typeof(string) },
			new DataChange { ColumnName = "Email", OldValue = "john.smith@example.com", DataType = typeof(string) },
		],
	};
}

// ============================================================================
// Example Command Handlers (for demonstration)
// ============================================================================

public sealed class ExampleSyncCustomerHandler : IActionHandler<SyncCustomerCommand, SyncCustomerResult>
{
	private readonly ILogger<ExampleSyncCustomerHandler> _logger;

	public ExampleSyncCustomerHandler(ILogger<ExampleSyncCustomerHandler> logger) => _logger = logger;

	public Task<SyncCustomerResult> HandleAsync(SyncCustomerCommand action, CancellationToken cancellationToken)
	{
		var customerId = Guid.NewGuid();
		_logger.LogInformation(
			"   → SyncCustomerCommand: Created customer {CustomerId} for external ID {ExternalId}",
			customerId, action.CustomerData.ExternalId);

		return Task.FromResult(new SyncCustomerResult { CustomerId = customerId, WasCreated = true });
	}
}

public sealed class ExampleUpdateCustomerHandler : IActionHandler<UpdateCustomerCommand, UpdateCustomerResult>
{
	private readonly ILogger<ExampleUpdateCustomerHandler> _logger;

	public ExampleUpdateCustomerHandler(ILogger<ExampleUpdateCustomerHandler> logger) => _logger = logger;

	public Task<UpdateCustomerResult> HandleAsync(UpdateCustomerCommand action, CancellationToken cancellationToken)
	{
		var customerId = Guid.NewGuid();
		_logger.LogInformation(
			"   → UpdateCustomerCommand: Updated customer {ExternalId}, Name: {Name}, Email: {Email}",
			action.CustomerData.ExternalId, action.CustomerData.Name, action.CustomerData.Email);

		return Task.FromResult(new UpdateCustomerResult { CustomerId = customerId, WasUpdated = true });
	}
}

public sealed class ExampleDeactivateCustomerHandler : IActionHandler<DeactivateCustomerCommand, DeactivateCustomerResult>
{
	private readonly ILogger<ExampleDeactivateCustomerHandler> _logger;

	public ExampleDeactivateCustomerHandler(ILogger<ExampleDeactivateCustomerHandler> logger) => _logger = logger;

	public Task<DeactivateCustomerResult> HandleAsync(DeactivateCustomerCommand action, CancellationToken cancellationToken)
	{
		var customerId = Guid.NewGuid();
		_logger.LogInformation(
			"   → DeactivateCustomerCommand: Soft-deleted customer {ExternalId} (CDC DELETE → Domain Deactivate)",
			action.CustomerData.ExternalId);

		return Task.FromResult(new DeactivateCustomerResult { CustomerId = customerId, WasDeactivated = true });
	}
}
