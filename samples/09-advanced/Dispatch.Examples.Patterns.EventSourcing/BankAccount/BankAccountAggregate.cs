// Copyright (c) Stacks Contributors. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Abstractions;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Implementation;
using Excalibur.Dispatch.Examples.Patterns.EventSourcing.BankAccount.Events;

namespace Excalibur.Dispatch.Examples.Patterns.EventSourcing.BankAccount;

/// <summary>
/// Bank account aggregate demonstrating event sourcing with snapshots.
/// </summary>
public class BankAccountAggregate : AggregateBase
{
 private decimal _balance;
 private string _accountHolder = string.Empty;
 private AccountStatus _status;
 private DateTimeOffset _openedAt;
 private readonly List<Transaction> _transactions = new();

 /// <summary>
 /// Gets the current balance.
 /// </summary>
 public decimal Balance => _balance;

 /// <summary>
 /// Gets the account holder name.
 /// </summary>
 public string AccountHolder => _accountHolder;

 /// <summary>
 /// Gets the account status.
 /// </summary>
 public AccountStatus Status => _status;

 /// <summary>
 /// Gets when the account was opened.
 /// </summary>
 public DateTimeOffset OpenedAt => _openedAt;

 /// <summary>
 /// Gets the transaction history.
 /// </summary>
 public IReadOnlyList<Transaction> Transactions => _transactions.AsReadOnly();

 /// <summary>
 /// Opens a new bank account.
 /// </summary>
 public static BankAccountAggregate Open(string accountId, string accountHolder, decimal initialDeposit)
 {
 if (string.IsNullOrEmpty(accountId))
 throw new ArgumentException("Account ID is required", nameof(accountId));
 if (string.IsNullOrEmpty(accountHolder))
 throw new ArgumentException("Account holder is required", nameof(accountHolder));
 if (initialDeposit < 0)
 throw new ArgumentException("Initial deposit cannot be negative", nameof(initialDeposit));

 var account = new BankAccountAggregate { Id = accountId };
 account.ApplyChange(new AccountOpened(accountId, accountHolder, initialDeposit, DateTimeOffset.UtcNow));

 return account;
 }

 /// <summary>
 /// Deposits money into the account.
 /// </summary>
 public void Deposit(decimal amount, string description = "")
 {
 if (_status != AccountStatus.Active)
 throw new InvalidOperationException("Cannot deposit to inactive account");
 if (amount <= 0)
 throw new ArgumentException("Deposit amount must be positive", nameof(amount));

 ApplyChange(new MoneyDeposited(Id, amount, _balance + amount, description, DateTimeOffset.UtcNow));
 }

 /// <summary>
 /// Withdraws money from the account.
 /// </summary>
 public void Withdraw(decimal amount, string description = "")
 {
 if (_status != AccountStatus.Active)
 throw new InvalidOperationException("Cannot withdraw from inactive account");
 if (amount <= 0)
 throw new ArgumentException("Withdrawal amount must be positive", nameof(amount));
 if (_balance < amount)
 throw new InvalidOperationException("Insufficient funds");

 ApplyChange(new MoneyWithdrawn(Id, amount, _balance - amount, description, DateTimeOffset.UtcNow));
 }

 /// <summary>
 /// Closes the account.
 /// </summary>
 public void Close(string reason)
 {
 if (_status == AccountStatus.Closed)
 throw new InvalidOperationException("Account is already closed");
 if (_balance > 0)
 throw new InvalidOperationException("Cannot close account with positive balance");

 ApplyChange(new AccountClosed(Id, reason, DateTimeOffset.UtcNow));
 }

 /// <inheritdoc/>
 protected override void RegisterHandlers()
 {
 Handles<AccountOpened>(When);
 Handles<MoneyDeposited>(When);
 Handles<MoneyWithdrawn>(When);
 Handles<AccountClosed>(When);
 }

 private void When(AccountOpened e)
 {
 _accountHolder = e.AccountHolder;
 _balance = e.InitialDeposit;
 _status = AccountStatus.Active;
 _openedAt = e.OpenedAt;

 if (e.InitialDeposit > 0)
 {
 _transactions.Add(new Transaction(
 TransactionType.Deposit,
 e.InitialDeposit,
 e.InitialDeposit,
 "Initial deposit",
 e.OpenedAt));
 }
 }

 private void When(MoneyDeposited e)
 {
 _balance = e.NewBalance;
 _transactions.Add(new Transaction(
 TransactionType.Deposit,
 e.Amount,
 e.NewBalance,
 e.Description,
 e.DepositedAt));
 }

 private void When(MoneyWithdrawn e)
 {
 _balance = e.NewBalance;
 _transactions.Add(new Transaction(
 TransactionType.Withdrawal,
 e.Amount,
 e.NewBalance,
 e.Description,
 e.WithdrawnAt));
 }

 private void When(AccountClosed e)
 {
 _status = AccountStatus.Closed;
 }

 /// <inheritdoc/>
 public override ISnapshot CreateSnapshot()
 {
 return new Snapshot
 {
 AggregateId = Id,
 Version = Version,
 AggregateType = GetType().AssemblyQualifiedName!,
 Data = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new BankAccountSnapshot
 {
 Balance = _balance,
 AccountHolder = _accountHolder,
 Status = _status,
 OpenedAt = _openedAt,
 Transactions = _transactions
 })
 };
 }

 /// <inheritdoc/>
 public override void LoadFromSnapshot(ISnapshot snapshot)
 {
 if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

 var data = System.Text.Json.JsonSerializer.Deserialize<BankAccountSnapshot>(snapshot.Data)
 ?? throw new InvalidOperationException("Failed to deserialize snapshot");

 Id = snapshot.AggregateId;
 Version = snapshot.Version;
 _balance = data.Balance;
 _accountHolder = data.AccountHolder;
 _status = data.Status;
 _openedAt = data.OpenedAt;
 _transactions.Clear();
 _transactions.AddRange(data.Transactions);
 }

 private record BankAccountSnapshot
 {
 public decimal Balance { get; init; }
 public string AccountHolder { get; init; } = string.Empty;
 public AccountStatus Status { get; init; }
 public DateTimeOffset OpenedAt { get; init; }
 public List<Transaction> Transactions { get; init; } = new();
 }
}

public enum AccountStatus
{
 Active,
 Frozen,
 Closed
}

public record Transaction(
 TransactionType Type,
 decimal Amount,
 decimal BalanceAfter,
 string Description,
 DateTimeOffset Timestamp);

public enum TransactionType
{
 Deposit,
 Withdrawal
}
