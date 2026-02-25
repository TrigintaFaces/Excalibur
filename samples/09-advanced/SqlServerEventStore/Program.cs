// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// SQL Server Event Store Sample
// ==============================
// This sample demonstrates event sourcing with SQL Server:
// - SQL Server event store configuration
// - Event stream append and read
// - Aggregate rehydration from events
// - Connection factory pattern
//
// Prerequisites:
// 1. Start SQL Server: docker-compose up -d
// 2. Run schema script (see docker-compose.yml comments)
// 3. Run the sample: dotnet run

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SqlServerEventStore.Domain;

// Build configuration
var builder = new HostApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure logging for visibility
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Information);
});

// ============================================================
// Configure Dispatch messaging
// ============================================================
builder.Services.AddDispatch(typeof(Program).Assembly);

// Add event serializer (required for event sourcing)
builder.Services.AddSingleton<IEventSerializer, JsonEventSerializer>();

// ============================================================
// Configure SQL Server Event Sourcing
// ============================================================
var connectionString = builder.Configuration.GetConnectionString("EventStore")
					   ?? throw new InvalidOperationException(
						   "ConnectionString 'EventStore' not found. Ensure appsettings.json is configured.");

// Option 1: Simple connection string registration
builder.Services.AddSqlServerEventSourcing(connectionString, registerHealthChecks: true);

// Option 2: Full configuration (alternative)
// builder.Services.AddSqlServerEventSourcing(options =>
// {
//     options.ConnectionString = connectionString;
//     options.RegisterHealthChecks = true;
//     options.EventStoreHealthCheckName = "eventstore-sqlserver";
//     options.SnapshotStoreHealthCheckName = "snapshotstore-sqlserver";
//     options.OutboxStoreHealthCheckName = "outbox-sqlserver";
// });

// ============================================================
// Configure Event Sourcing Repository
// ============================================================
builder.Services.AddExcaliburEventSourcing(es =>
{
	// Register the BankAccountAggregate repository with factory
	_ = es.AddRepository<BankAccountAggregate, Guid>(id => new BankAccountAggregate(id));
});

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var eventStore = host.Services.GetRequiredService<IEventStore>();
var repository = host.Services.GetRequiredService<IEventSourcedRepository<BankAccountAggregate, Guid>>();

logger.LogInformation("=================================================");
logger.LogInformation("  SQL Server Event Store Sample");
logger.LogInformation("=================================================");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demo 1: Create and Save an Aggregate
// ============================================================
logger.LogInformation("=== Demo 1: Create and Save an Aggregate ===");
logger.LogInformation("");

var accountId = Guid.NewGuid();
logger.LogInformation("Creating new bank account: {AccountId}", accountId);

// Create a new aggregate
var account = BankAccountAggregate.Open(accountId, "John Doe", 1000.00m);
logger.LogInformation("Account opened with initial deposit of {Deposit:C}", 1000.00m);

// Perform some operations
account.Deposit(500.00m, "Paycheck");
logger.LogInformation("Deposited: {Amount:C} (Paycheck)", 500.00m);

account.Deposit(250.00m, "Birthday gift");
logger.LogInformation("Deposited: {Amount:C} (Birthday gift)", 250.00m);

account.Withdraw(200.00m, "Groceries");
logger.LogInformation("Withdrew: {Amount:C} (Groceries)", 200.00m);

// Save to SQL Server
logger.LogInformation("");
logger.LogInformation("Saving aggregate to SQL Server event store...");
await repository.SaveAsync(account, CancellationToken.None).ConfigureAwait(false);
logger.LogInformation("Saved {EventCount} events for aggregate {AccountId}", account.Version, accountId);

logger.LogInformation("");
logger.LogInformation("Current state after operations:");
logger.LogInformation("  Account Holder: {Holder}", account.HolderName);
logger.LogInformation("  Balance: {Balance:C}", account.Balance);
logger.LogInformation("  Total Deposits: {TotalDeposits:C}", account.TotalDeposits);
logger.LogInformation("  Total Withdrawals: {TotalWithdrawals:C}", account.TotalWithdrawals);
logger.LogInformation("  Transaction Count: {TransactionCount}", account.TransactionCount);
logger.LogInformation("  Version: {Version}", account.Version);

// ============================================================
// Demo 2: Load Aggregate from Event Store
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 2: Load Aggregate from Event Store ===");
logger.LogInformation("");

logger.LogInformation("Loading aggregate {AccountId} from SQL Server...", accountId);
var loadedAccount = await repository.GetByIdAsync(accountId, CancellationToken.None).ConfigureAwait(false);

if (loadedAccount != null)
{
	logger.LogInformation("Successfully loaded aggregate!");
	logger.LogInformation("");
	logger.LogInformation("Reconstructed state from {EventCount} events:", loadedAccount.Version);
	logger.LogInformation("  Account Holder: {Holder}", loadedAccount.HolderName);
	logger.LogInformation("  Balance: {Balance:C}", loadedAccount.Balance);
	logger.LogInformation("  Total Deposits: {TotalDeposits:C}", loadedAccount.TotalDeposits);
	logger.LogInformation("  Total Withdrawals: {TotalWithdrawals:C}", loadedAccount.TotalWithdrawals);
	logger.LogInformation("  Transaction Count: {TransactionCount}", loadedAccount.TransactionCount);
	logger.LogInformation("  Is Active: {IsActive}", loadedAccount.IsActive);
}

// ============================================================
// Demo 3: Append More Events to Existing Stream
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 3: Append More Events to Existing Stream ===");
logger.LogInformation("");

if (loadedAccount != null)
{
	logger.LogInformation("Performing more operations on loaded aggregate...");

	loadedAccount.Deposit(100.00m, "Interest");
	logger.LogInformation("Deposited: {Amount:C} (Interest)", 100.00m);

	loadedAccount.Withdraw(50.00m, "Coffee subscription");
	logger.LogInformation("Withdrew: {Amount:C} (Coffee subscription)", 50.00m);

	logger.LogInformation("");
	logger.LogInformation("Saving updated aggregate...");
	await repository.SaveAsync(loadedAccount, CancellationToken.None).ConfigureAwait(false);
	logger.LogInformation("Saved. New version: {Version}", loadedAccount.Version);

	logger.LogInformation("");
	logger.LogInformation("Updated state:");
	logger.LogInformation("  Balance: {Balance:C}", loadedAccount.Balance);
	logger.LogInformation("  Version: {Version}", loadedAccount.Version);
}

// ============================================================
// Demo 4: Direct Event Store Access
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 4: Direct Event Store Access ===");
logger.LogInformation("");

logger.LogInformation("Reading events directly from event store...");
var streamId = accountId.ToString();
var aggregateType = nameof(BankAccountAggregate);
var storedEvents = await eventStore.LoadAsync(streamId, aggregateType, CancellationToken.None).ConfigureAwait(false);

logger.LogInformation("Found {EventCount} events in stream {StreamId}:", storedEvents.Count, streamId);
foreach (var evt in storedEvents)
{
	logger.LogInformation("  [{Version}] {EventType} at {Timestamp}",
		evt.Version,
		evt.EventType,
		evt.Timestamp);
}

// ============================================================
// Demo 5: Close Account (Business Rule)
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 5: Close Account (Business Rule) ===");
logger.LogInformation("");

// Reload to get fresh state
var accountToClose = await repository.GetByIdAsync(accountId, CancellationToken.None).ConfigureAwait(false);
if (accountToClose != null)
{
	// Withdraw remaining balance
	if (accountToClose.Balance > 0)
	{
		logger.LogInformation("Withdrawing remaining balance: {Balance:C}", accountToClose.Balance);
		accountToClose.Withdraw(accountToClose.Balance, "Account closure - final withdrawal");
	}

	// Close the account
	logger.LogInformation("Closing account...");
	accountToClose.Close("Customer request - sample demonstration");

	await repository.SaveAsync(accountToClose, CancellationToken.None).ConfigureAwait(false);
	logger.LogInformation("Account closed successfully.");
	logger.LogInformation("  Is Active: {IsActive}", accountToClose.IsActive);
	logger.LogInformation("  Final Version: {Version}", accountToClose.Version);
}

// ============================================================
// Configuration Reference
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Configuration Reference ===");
logger.LogInformation("");
logger.LogInformation("Connection String Pattern:");
logger.LogInformation("  Server=localhost,1433;Database=EventStore;User Id=sa;Password=...;TrustServerCertificate=True");
logger.LogInformation("");
logger.LogInformation("Registration Methods:");
logger.LogInformation("  | Method                          | Description                    |");
logger.LogInformation("  |---------------------------------|--------------------------------|");
logger.LogInformation("  | AddSqlServerEventStore          | Event store only               |");
logger.LogInformation("  | AddSqlServerSnapshotStore       | Snapshot store only            |");
logger.LogInformation("  | AddSqlServerOutboxStore         | Outbox store only              |");
logger.LogInformation("  | AddSqlServerEventSourcing       | All stores + health checks     |");
logger.LogInformation("");
logger.LogInformation("Key Tables:");
logger.LogInformation("  | Table                           | Purpose                        |");
logger.LogInformation("  |---------------------------------|--------------------------------|");
logger.LogInformation("  | eventsourcing.Events            | Domain events                  |");
logger.LogInformation("  | eventsourcing.Snapshots         | Aggregate snapshots            |");
logger.LogInformation("  | eventsourcing.Outbox            | Transactional outbox           |");
logger.LogInformation("  | eventsourcing.ProjectionCheckpoints | Projection progress       |");

logger.LogInformation("");
logger.LogInformation("Sample completed. Press Ctrl+C to exit...");

await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
