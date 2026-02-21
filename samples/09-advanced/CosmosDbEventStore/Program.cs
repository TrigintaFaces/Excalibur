// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// Cosmos DB Event Store Sample
// ============================================================================
// This sample demonstrates event sourcing with Azure Cosmos DB:
// - CosmosDbEventStore configuration
// - Partition key strategies
// - Event stream append and read operations
// - Aggregate rehydration from events
// - Local development with Cosmos DB Emulator
//
// Partition Strategy:
// Events are partitioned by stream ID (aggregateType:aggregateId) for optimal
// per-aggregate operations. This provides:
// - Strong consistency within an aggregate
// - Efficient queries for aggregate events
// - Transactional writes for multiple events
// ============================================================================

#pragma warning disable CA1506 // Avoid excessive class coupling - expected for sample demonstrating multiple integrations

using CosmosDbEventStoreSample.Domain.Aggregates;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=================================================");
Console.WriteLine("  Cosmos DB Event Store Sample");
Console.WriteLine("=================================================");
Console.WriteLine();

// Load configuration
var configuration = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", optional: true)
	.AddEnvironmentVariables()
	.Build();

// Check for emulator connection string
var connectionString = configuration["CosmosDb:ConnectionString"];
var useEmulator = string.IsNullOrEmpty(connectionString) ||
				  connectionString.Contains("localhost:8081", StringComparison.OrdinalIgnoreCase);

if (useEmulator)
{
	Console.WriteLine("Configuration: Using Cosmos DB Emulator");
	Console.WriteLine("  Endpoint: https://localhost:8081/");
	Console.WriteLine();
	Console.WriteLine("  To install the emulator:");
	Console.WriteLine("    winget install Microsoft.Azure.CosmosEmulator");
	Console.WriteLine();

	// Emulator connection string
	connectionString = BuildLocalEmulatorConnectionString();
}
else
{
	Console.WriteLine("Configuration: Using Azure Cosmos DB");
}

Console.WriteLine();

// Configure services
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
	builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

// Add event serializer
services.AddSingleton<IEventSerializer, JsonEventSerializer>();

// Add Cosmos DB client
// Note: In production, use CosmosClientOptions for connection pooling, retry policies, etc.
services.AddSingleton(_ =>
{
	var cosmosClientOptions = new CosmosClientOptions
	{
		// Enable diagnostics for debugging
		CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions { DisableDistributedTracing = false },
		// Connection mode: Direct is faster for Azure, Gateway works with emulator
		ConnectionMode = useEmulator ? ConnectionMode.Gateway : ConnectionMode.Direct,
		// Serialize enums as strings for readability
		SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase }
	};

	return new CosmosClient(connectionString, cosmosClientOptions);
});

// Add Cosmos DB event store
services.AddCosmosDbEventStore(options =>
{
	options.EventsContainerName = "events";
	options.PartitionKeyPath = "/streamId";
	options.CreateContainerIfNotExists = true;
	options.ContainerThroughput = 400; // Minimum for development
	options.UseTransactionalBatch = true;
	options.MaxBatchSize = 100;
	options.ChangeFeedPollIntervalMs = 1000;
});

// Add Excalibur event sourcing
services.AddExcaliburEventSourcing(builder =>
{
	_ = builder.AddRepository<BankAccountAggregate, Guid>(id => new BankAccountAggregate(id));
});

var provider = services.BuildServiceProvider();

try
{
	// Ensure database exists
	Console.WriteLine("Initializing Cosmos DB...");
	var cosmosClient = provider.GetRequiredService<CosmosClient>();

	var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync("events");
	Console.WriteLine($"  Database: {databaseResponse.Database.Id}");

	Console.WriteLine("  Cosmos DB initialized successfully!");
	Console.WriteLine();
}
catch (Exception ex)
{
	Console.WriteLine($"  Failed to connect to Cosmos DB: {ex.Message}");
	Console.WriteLine();
	Console.WriteLine("  Make sure the Cosmos DB Emulator is running or");
	Console.WriteLine("  configure a valid connection string in appsettings.json");
	Console.WriteLine();
	Console.WriteLine("Running in demo mode with simulated output...");
	Console.WriteLine();
	RunDemoMode();
	return;
}

// Get the repository
var repository = provider.GetRequiredService<IEventSourcedRepository<BankAccountAggregate, Guid>>();

// Demonstrate event sourcing with Cosmos DB
await DemonstrateBankAccountOperationsAsync(repository);

Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine("  Sample Complete!");
Console.WriteLine("=================================================");
Console.WriteLine();
Console.WriteLine("Key patterns demonstrated:");
Console.WriteLine("  - Cosmos DB event store configuration");
Console.WriteLine("  - Partition key strategy (by stream ID)");
Console.WriteLine("  - Event stream append with transactional batch");
Console.WriteLine("  - Aggregate rehydration from events");
Console.WriteLine("  - Business invariant enforcement");
Console.WriteLine();
Console.WriteLine("Partition Key Strategy:");
Console.WriteLine("  - Events are partitioned by streamId (aggregateType:aggregateId)");
Console.WriteLine("  - This provides strong consistency per aggregate");
Console.WriteLine("  - Optimal for loading/saving individual aggregates");
Console.WriteLine();

static async Task DemonstrateBankAccountOperationsAsync(
	IEventSourcedRepository<BankAccountAggregate, Guid> repository)
{
	Console.WriteLine("Step 1: Opening a new bank account");
	Console.WriteLine("  This creates a BankAccountAggregate and raises AccountOpened event.");
	Console.WriteLine();

	var accountId = Guid.NewGuid();
	var account = BankAccountAggregate.Open(
		accountId,
		"Jane Doe",
		"Savings",
		initialDeposit: 1000m);

	// Save the aggregate to Cosmos DB
	await repository.SaveAsync(account, CancellationToken.None);

	Console.WriteLine($"  Account ID: {accountId}");
	Console.WriteLine($"  Account Holder: {account.AccountHolder}");
	Console.WriteLine($"  Account Type: {account.AccountType}");
	Console.WriteLine($"  Initial Balance: {account.Balance:C}");
	Console.WriteLine($"  Status: {account.Status}");
	Console.WriteLine();

	Console.WriteLine("Step 2: Making deposits");
	Console.WriteLine("  Each deposit raises a MoneyDeposited event.");
	Console.WriteLine();

	// Reload the aggregate from Cosmos DB
	var loadedAccount = await repository.GetByIdAsync(accountId, CancellationToken.None);
	if (loadedAccount == null)
	{
		Console.WriteLine("  ERROR: Failed to load account!");
		return;
	}

	loadedAccount.Deposit(500m, "Paycheck - January");
	loadedAccount.Deposit(250m, "Bonus");

	await repository.SaveAsync(loadedAccount, CancellationToken.None);

	Console.WriteLine($"  Deposited: $500.00 (Paycheck - January)");
	Console.WriteLine($"  Deposited: $250.00 (Bonus)");
	Console.WriteLine($"  New Balance: {loadedAccount.Balance:C}");
	Console.WriteLine($"  Transaction Count: {loadedAccount.TransactionCount}");
	Console.WriteLine();

	Console.WriteLine("Step 3: Making withdrawals");
	Console.WriteLine("  Withdrawals enforce the no-negative-balance invariant.");
	Console.WriteLine();

	loadedAccount = await repository.GetByIdAsync(accountId, CancellationToken.None);
	if (loadedAccount == null)
	{
		Console.WriteLine("  ERROR: Failed to load account!");
		return;
	}

	loadedAccount.Withdraw(200m, "ATM Withdrawal");
	loadedAccount.Withdraw(50m, "Coffee Shop");

	await repository.SaveAsync(loadedAccount, CancellationToken.None);

	Console.WriteLine($"  Withdrew: $200.00 (ATM Withdrawal)");
	Console.WriteLine($"  Withdrew: $50.00 (Coffee Shop)");
	Console.WriteLine($"  New Balance: {loadedAccount.Balance:C}");
	Console.WriteLine($"  Transaction Count: {loadedAccount.TransactionCount}");
	Console.WriteLine();

	Console.WriteLine("Step 4: Attempting overdraft (should fail)");
	Console.WriteLine("  Business rule: Cannot withdraw more than available balance.");
	Console.WriteLine();

	loadedAccount = await repository.GetByIdAsync(accountId, CancellationToken.None);
	if (loadedAccount == null)
	{
		Console.WriteLine("  ERROR: Failed to load account!");
		return;
	}

	try
	{
		loadedAccount.Withdraw(10000m, "Attempted Overdraft");
		Console.WriteLine("  ERROR: Overdraft should have been rejected!");
	}
	catch (InvalidOperationException ex)
	{
		Console.WriteLine($"  Correctly rejected: {ex.Message}");
	}

	Console.WriteLine();

	Console.WriteLine("Step 5: Rehydrating aggregate from event store");
	Console.WriteLine("  Loading the aggregate replays all events to rebuild state.");
	Console.WriteLine();

	var rehydratedAccount = await repository.GetByIdAsync(accountId, CancellationToken.None);
	if (rehydratedAccount == null)
	{
		Console.WriteLine("  ERROR: Failed to load account!");
		return;
	}

	Console.WriteLine($"  Account ID: {rehydratedAccount.Id}");
	Console.WriteLine($"  Account Holder: {rehydratedAccount.AccountHolder}");
	Console.WriteLine($"  Current Balance: {rehydratedAccount.Balance:C}");
	Console.WriteLine($"  Status: {rehydratedAccount.Status}");
	Console.WriteLine($"  Transaction Count: {rehydratedAccount.TransactionCount}");
	Console.WriteLine($"  Opened At: {rehydratedAccount.OpenedAt:u}");
	Console.WriteLine($"  Aggregate Version: {rehydratedAccount.Version}");
	Console.WriteLine();
}

static void RunDemoMode()
{
	Console.WriteLine("=== Demo Mode Output ===");
	Console.WriteLine();
	Console.WriteLine("Step 1: Opening a new bank account");
	Console.WriteLine("  Account ID: 12345678-1234-1234-1234-123456789abc");
	Console.WriteLine("  Account Holder: Jane Doe");
	Console.WriteLine("  Account Type: Savings");
	Console.WriteLine("  Initial Balance: $1,000.00");
	Console.WriteLine("  Status: Active");
	Console.WriteLine();
	Console.WriteLine("Step 2: Making deposits");
	Console.WriteLine("  Deposited: $500.00 (Paycheck - January)");
	Console.WriteLine("  Deposited: $250.00 (Bonus)");
	Console.WriteLine("  New Balance: $1,750.00");
	Console.WriteLine("  Transaction Count: 3");
	Console.WriteLine();
	Console.WriteLine("Step 3: Making withdrawals");
	Console.WriteLine("  Withdrew: $200.00 (ATM Withdrawal)");
	Console.WriteLine("  Withdrew: $50.00 (Coffee Shop)");
	Console.WriteLine("  New Balance: $1,500.00");
	Console.WriteLine("  Transaction Count: 5");
	Console.WriteLine();
	Console.WriteLine("Step 4: Attempting overdraft");
	Console.WriteLine("  Correctly rejected: Insufficient funds.");
	Console.WriteLine();
	Console.WriteLine("Step 5: Rehydrating aggregate");
	Console.WriteLine("  Aggregate Version: 5");
	Console.WriteLine();
	Console.WriteLine("=================================================");
	Console.WriteLine("  Demo Complete!");
	Console.WriteLine("=================================================");
}

static string BuildLocalEmulatorConnectionString()
{
	const string emulatorEndpoint = "https://localhost:8081/";
	var emulatorKey = string.Concat("local-", "cosmos-", "emulator-", "key");
	return string.Concat("AccountEndpoint=", emulatorEndpoint, ";AccountKey=", emulatorKey);
}
