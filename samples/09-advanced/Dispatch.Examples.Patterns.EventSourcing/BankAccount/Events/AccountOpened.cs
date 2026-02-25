// Copyright (c) Stacks Contributors. All rights reserved.
// Licensed under the MIT license.

using System;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Abstractions;

namespace Excalibur.Dispatch.Examples.Patterns.EventSourcing.BankAccount.Events;

/// <summary>
/// Event raised when a bank account is opened.
/// </summary>
public record AccountOpened : IEvent
{
 /// <summary>
 /// Initializes a new instance of the <see cref="AccountOpened"/> record.
 /// </summary>
 public AccountOpened(
 string aggregateId,
 string accountHolder,
 decimal initialDeposit,
 DateTimeOffset openedAt)
 {
 EventId = Guid.NewGuid().ToString();
 AggregateId = aggregateId;
 AccountHolder = accountHolder;
 InitialDeposit = initialDeposit;
 OpenedAt = openedAt;
 OccurredAt = openedAt;
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
 /// Gets the account holder name.
 /// </summary>
 public string AccountHolder { get; }

 /// <summary>
 /// Gets the initial deposit amount.
 /// </summary>
 public decimal InitialDeposit { get; }

 /// <summary>
 /// Gets when the account was opened.
 /// </summary>
 public DateTimeOffset OpenedAt { get; }
}
