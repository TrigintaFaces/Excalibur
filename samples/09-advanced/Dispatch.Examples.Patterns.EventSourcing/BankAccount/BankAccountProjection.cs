// Copyright (c) Stacks Contributors. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Abstractions;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Projections;
using Excalibur.Dispatch.Examples.Patterns.EventSourcing.BankAccount.Events;

namespace Excalibur.Dispatch.Examples.Patterns.EventSourcing.BankAccount;

/// <summary>
/// Projection that builds account summary read models from events.
/// </summary>
public class BankAccountProjection : ProjectionBase<AccountSummary>
{
 private readonly ConcurrentDictionary<string, AccountSummary> _accounts;
 private readonly IMemoryCache _cache;

 /// <summary>
 /// Initializes a new instance of the <see cref="BankAccountProjection"/> class.
 /// </summary>
 public BankAccountProjection(
 IProjectionStore projectionStore,
 IMemoryCache cache,
 ILogger<BankAccountProjection> logger)
 : base("bank-account-summary", projectionStore, logger)
 {
 _accounts = new ConcurrentDictionary<string, AccountSummary>();
 _cache = cache ?? throw new ArgumentNullException(nameof(cache));
 }

 /// <inheritdoc/>
 protected override void RegisterHandlers()
 {
 When<AccountOpened>(async (e, ct) =>
 {
 var summary = new AccountSummary
 {
 AccountId = e.AggregateId,
 AccountHolder = e.AccountHolder,
 Balance = e.InitialDeposit,
 Status = "Active",
 OpenedAt = e.OpenedAt,
 LastActivity = e.OpenedAt,
 TotalDeposits = e.InitialDeposit,
 TotalWithdrawals = 0,
 TransactionCount = e.InitialDeposit > 0 ? 1 : 0
 };

 await SaveAsync(e.AggregateId, summary, ct);
 });

 When<MoneyDeposited>(async (e, ct) =>
 {
 var summary = await GetAsync(e.AggregateId, ct);
 if (summary != null)
 {
 summary.Balance = e.NewBalance;
 summary.LastActivity = e.DepositedAt;
 summary.TotalDeposits += e.Amount;
 summary.TransactionCount++;

 await SaveAsync(e.AggregateId, summary, ct);
 }
 });

 When<MoneyWithdrawn>(async (e, ct) =>
 {
 var summary = await GetAsync(e.AggregateId, ct);
 if (summary != null)
 {
 summary.Balance = e.NewBalance;
 summary.LastActivity = e.WithdrawnAt;
 summary.TotalWithdrawals += e.Amount;
 summary.TransactionCount++;

 await SaveAsync(e.AggregateId, summary, ct);
 }
 });

 When<AccountClosed>(async (e, ct) =>
 {
 var summary = await GetAsync(e.AggregateId, ct);
 if (summary != null)
 {
 summary.Status = "Closed";
 summary.ClosedAt = e.ClosedAt;
 summary.LastActivity = e.ClosedAt;

 await SaveAsync(e.AggregateId, summary, ct);
 }
 });
 }

 /// <inheritdoc/>
 public override async Task<AccountSummary?> GetAsync(string id, CancellationToken cancellationToken = default)
 {
 // Try cache first
 if (_cache.TryGetValue<AccountSummary>($"account:{id}", out var cached))
 {
 return cached;
 }

 // Try in-memory store
 return _accounts.GetValueOrDefault(id);
 }

 /// <inheritdoc/>
 public override async Task SaveAsync(string id, AccountSummary readModel, CancellationToken cancellationToken = default)
 {
 _accounts[id] = readModel;

 // Cache with sliding expiration
 _cache.Set($"account:{id}", readModel, TimeSpan.FromMinutes(5));

 await Task.CompletedTask;
 }

 /// <inheritdoc/>
 protected override Task OnResetAsync(CancellationToken cancellationToken = default)
 {
 _accounts.Clear();
 return Task.CompletedTask;
 }
}

/// <summary>
/// Read model for account summary information.
/// </summary>
public class AccountSummary {
 /// <summary>
 /// Gets or sets the account identifier.
 /// </summary>
 public string AccountId { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the account holder name.
 /// </summary>
 public string AccountHolder { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the current balance.
 /// </summary>
 public decimal Balance { get; set; }

 /// <summary>
 /// Gets or sets the account status.
 /// </summary>
 public string Status { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets when the account was opened.
 /// </summary>
 public DateTimeOffset OpenedAt { get; set; }

 /// <summary>
 /// Gets or sets when the account was closed.
 /// </summary>
 public DateTimeOffset? ClosedAt { get; set; }

 /// <summary>
 /// Gets or sets the last activity timestamp.
 /// </summary>
 public DateTimeOffset LastActivity { get; set; }

 /// <summary>
 /// Gets or sets the total deposits.
 /// </summary>
 public decimal TotalDeposits { get; set; }

 /// <summary>
 /// Gets or sets the total withdrawals.
 /// </summary>
 public decimal TotalWithdrawals { get; set; }

 /// <summary>
 /// Gets or sets the transaction count.
 /// </summary>
 public int TransactionCount { get; set; }
}
