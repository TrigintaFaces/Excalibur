// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Abstractions;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Extensions;
using examples.Excalibur.Dispatch.Examples.Patterns.EventSourcing.BankAccount;

namespace examples.Excalibur.Dispatch.Examples.Patterns.EventSourcing;

/// <summary>
/// Example program demonstrating event sourcing with bank accounts.
/// </summary>
public class Program {
 public static async Task Main(string[] args)
 {
 var host = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Configure event sourcing
 services.AddEventSourcing(options =>
 {
 options.SnapshotInterval = 5; // Snapshot every 5 events
 options.EnableAutomaticSnapshots = true;
 });

 // Use in-memory event store for demo
 services.AddInMemoryEventStore();

 // Add caching layer
 services.AddCachedEventStore(options =>
 {
 options.StreamCacheDuration = TimeSpan.FromMinutes(10);
 options.SnapshotCacheDuration = TimeSpan.FromMinutes(30);
 });

 // Register the example service
 services.AddHostedService<BankAccountExampleService>();
 })
 .Build();

 await host.RunAsync();
 }
}

/// <summary>
/// Example service demonstrating event sourcing operations.
/// </summary>
public class BankAccountExampleService : BackgroundService
{
 private readonly IEventSourcedRepository<BankAccountAggregate> _repository;
 private readonly ILogger<BankAccountExampleService> _logger;

 public BankAccountExampleService(IEventSourcedRepository<BankAccountAggregate> repository,
 ILogger<BankAccountExampleService> logger)
 {
 _repository = repository;
 _logger = logger;
 }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 _logger.LogInformation("Starting bank account event sourcing example...");

 try
 {
 // Create a new account
 var accountId = $"ACC-{Guid.NewGuid():N}";
 await CreateAndUseAccount(accountId);

 // Demonstrate loading from events
 await LoadAndDisplayAccount(accountId);

 // Demonstrate snapshot functionality
 await DemonstrateSnapshots(accountId);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error in example service");
 }

 _logger.LogInformation("Example completed. Press Ctrl+C to exit.");
 }

 private async Task CreateAndUseAccount(string accountId)
 {
 _logger.LogInformation("Creating new bank account {AccountId}", accountId);

 // Create new account
 var account = new BankAccountAggregate();
 account.Open(accountId, "John Doe", 1000m);

 // Perform some operations
 account.Deposit(500m);
 account.Withdraw(200m);
 account.Deposit(300m);

 // Save the account
 await _repository.SaveAsync(account);

 _logger.LogInformation(
 "Account {AccountId} created with balance: ${Balance}",
 accountId, account.Balance);
 }
 private async Task LoadAndDisplayAccount(string accountId)
 {
 _logger.LogInformation("Loading account {AccountId} from event store", accountId);

 // Load the account
 var account = await _repository.GetByIdAsync(accountId);

 if (account != null)
 {
 _logger.LogInformation(
 "Loaded account {AccountId}: Holder={Holder}, Balance=${Balance}, Version={Version}",
 account.Id, account.AccountHolder, account.Balance, account.Version);
 }
 }

 private async Task DemonstrateSnapshots(string accountId)
 {
 _logger.LogInformation("Demonstrating snapshot functionality");

 // Load account and make more transactions to trigger snapshot
 var account = await _repository.GetByIdAsync(accountId);
 if (account == null) return;

 // Add more transactions
 for (int i = 0; i < 10; i++)
 {
 account.Deposit(100m);
 await _repository.SaveAsync(account);
 }

 _logger.LogInformation(
 "Account {AccountId} now at version {Version} with balance ${Balance}",
 account.Id, account.Version, account.Balance);

 // Now when we load again, it should use the snapshot
 var accountFromSnapshot = await _repository.GetByIdAsync(accountId);

 _logger.LogInformation(
 "Account loaded (potentially from snapshot): Version={Version}, Balance=${Balance}",
 accountFromSnapshot!.Version, accountFromSnapshot.Balance);
 }
}