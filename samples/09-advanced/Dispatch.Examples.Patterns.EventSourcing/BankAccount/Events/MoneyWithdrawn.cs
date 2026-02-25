// Copyright (c) Stacks Contributors. All rights reserved.
// Licensed under the MIT license.

using System;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Abstractions;

namespace Excalibur.Dispatch.Examples.Patterns.EventSourcing.BankAccount.Events;

/// <summary>
/// Event raised when money is withdrawn from a bank account.
/// </summary>
public record MoneyWithdrawn : IEvent
{
 /// <summary>
 /// Initializes a new instance of the <see cref="MoneyWithdrawn"/> record.
 /// </summary>
 public MoneyWithdrawn(
 string aggregateId,
 decimal amount,
 decimal newBalance,
 string description,
 DateTimeOffset withdrawnAt)
 {
 EventId = Guid.NewGuid().ToString();
 AggregateId = aggregateId;
 Amount = amount;
 NewBalance = newBalance;
 Description = description;
 WithdrawnAt = withdrawnAt;
 OccurredAt = withdrawnAt;
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
 /// Gets the withdrawal amount.
 /// </summary>
 public decimal Amount { get; }

 /// <summary>
 /// Gets the balance after withdrawal.
 /// </summary>
 public decimal NewBalance { get; }

 /// <summary>
 /// Gets the withdrawal description.
 /// </summary>
 public string Description { get; }

 /// <summary>
 /// Gets when the withdrawal was made.
 /// </summary>
 public DateTimeOffset WithdrawnAt { get; }
}
