// Copyright (c) Stacks Contributors. All rights reserved.
// Licensed under the MIT license.

using System;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Abstractions;

namespace Excalibur.Dispatch.Examples.Patterns.EventSourcing.BankAccount.Events;

/// <summary>
/// Event raised when money is deposited into a bank account.
/// </summary>
public record MoneyDeposited : IEvent
{
 /// <summary>
 /// Initializes a new instance of the <see cref="MoneyDeposited"/> record.
 /// </summary>
 public MoneyDeposited(
 string aggregateId,
 decimal amount,
 decimal newBalance,
 string description,
 DateTimeOffset depositedAt)
 {
 EventId = Guid.NewGuid().ToString();
 AggregateId = aggregateId;
 Amount = amount;
 NewBalance = newBalance;
 Description = description;
 DepositedAt = depositedAt;
 OccurredAt = depositedAt;
 }

 /// <inheritdoc/>
 public string EventId { get; }

 /// <inheritdoc/>
 public DateTimeOffset OccurredAt { get; }

 /// <inheritdoc/>
 public string AggregateId { get; }

 /// <inheritdoc/>
 public long Version { get; init; }

 /// <summary>
 /// Gets the deposit amount.
 /// </summary>
 public decimal Amount { get; }

 /// <summary>
 /// Gets the balance after deposit.
 /// </summary>
 public decimal NewBalance { get; }

 /// <summary>
 /// Gets the deposit description.
 /// </summary>
 public string Description { get; }

 /// <summary>
 /// Gets when the deposit was made.
 /// </summary>
 public DateTimeOffset DepositedAt { get; }
}
