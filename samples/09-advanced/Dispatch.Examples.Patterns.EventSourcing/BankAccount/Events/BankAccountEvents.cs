// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Abstractions;

namespace Excalibur.Dispatch.Examples.Patterns.EventSourcing.BankAccount.Events;

/// <summary>
/// Base class for bank account events.
/// </summary>
public abstract class BankAccountEvent : IEvent
{
 /// <summary>
 /// Initializes a new instance of the <see cref="BankAccountEvent"/> class.
 /// </summary>
 protected BankAccountEvent(string aggregateId, DateTimeOffset occurredAt)
 {
 EventId = Guid.NewGuid().ToString();
 AggregateId = aggregateId;
 OccurredAt = occurredAt;
 EventType = GetType().Name;
 }

 /// <inheritdoc/>
 public string EventId { get; }

 /// <inheritdoc/>
 public DateTimeOffset OccurredAt { get; }

 /// <inheritdoc/>
 public string AggregateId { get; }

 /// <inheritdoc/>
 public long Version { get; set; }

 /// <inheritdoc/>
 public string EventType { get; }

 /// <inheritdoc/>
 public string? CorrelationId { get; set; }

 /// <inheritdoc/>
 public string? CausationId { get; set; }

 /// <inheritdoc/>
 public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Event raised when a bank account is opened.
/// </summary>
public class AccountOpened : BankAccountEvent
{
 /// <summary>
 /// Initializes a new instance of the <see cref="AccountOpened"/> class.
 /// </summary>
 public AccountOpened(string accountId, string accountHolder, decimal initialDeposit, DateTimeOffset occurredAt)
 : base(accountId, occurredAt)
 {
 AccountHolder = accountHolder;
 InitialDeposit = initialDeposit;
 }

 /// <summary>
 /// Gets the account holder name.
 /// </summary>
 public string AccountHolder { get; }

 /// <summary>
 /// Gets the initial deposit amount.
 /// </summary>
 public decimal InitialDeposit { get; }
}

/// <summary>
/// Event raised when money is deposited.
/// </summary>
public class MoneyDeposited : BankAccountEvent
{
 /// <summary>
 /// Initializes a new instance of the <see cref="MoneyDeposited"/> class.
 /// </summary>
 public MoneyDeposited(string accountId, decimal amount, decimal newBalance, DateTimeOffset occurredAt)
 : base(accountId, occurredAt)
 {
 Amount = amount;
 NewBalance = newBalance;
 }

 /// <summary>
 /// Gets the deposit amount.
 /// </summary>
 public decimal Amount { get; }

 /// <summary>
 /// Gets the new balance after deposit.
 /// </summary>
 public decimal NewBalance { get; }
}

/// <summary>
/// Event raised when money is withdrawn.
/// </summary>
public class MoneyWithdrawn : BankAccountEvent
{
 /// <summary>
 /// Initializes a new instance of the <see cref="MoneyWithdrawn"/> class.
 /// </summary>
 public MoneyWithdrawn(string accountId, decimal amount, decimal newBalance, DateTimeOffset occurredAt)
 : base(accountId, occurredAt)
 {
 Amount = amount;
 NewBalance = newBalance;
 }

 /// <summary>
 /// Gets the withdrawal amount.
 /// </summary>
 public decimal Amount { get; }

 /// <summary>
 /// Gets the new balance after withdrawal.
 /// </summary>
 public decimal NewBalance { get; }
}
/// <summary>
/// Event raised when an account is closed.
/// </summary>
public class AccountClosed : BankAccountEvent
{
 /// <summary>
 /// Initializes a new instance of the <see cref="AccountClosed"/> class.
 /// </summary>
 public AccountClosed(string accountId, DateTimeOffset occurredAt)
 : base(accountId, occurredAt)
 {
 }
}