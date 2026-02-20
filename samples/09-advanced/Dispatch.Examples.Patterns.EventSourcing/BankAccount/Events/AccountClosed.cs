// Copyright (c) Stacks Contributors. All rights reserved.
// Licensed under the MIT license.

using System;
using Excalibur.Dispatch.CloudNative.Patterns.EventSourcing.Abstractions;

namespace Excalibur.Dispatch.Examples.Patterns.EventSourcing.BankAccount.Events;

/// <summary>
/// Event raised when a bank account is closed.
/// </summary>
public record AccountClosed : IEvent
{
 /// <summary>
 /// Initializes a new instance of the <see cref="AccountClosed"/> record.
 /// </summary>
 public AccountClosed(
 string aggregateId,
 string reason,
 DateTimeOffset closedAt)
 {
 EventId = Guid.NewGuid().ToString();
 AggregateId = aggregateId;
 Reason = reason;
 ClosedAt = closedAt;
 OccurredAt = closedAt;
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
 /// Gets the reason for closing the account.
 /// </summary>
 public string Reason { get; }

 /// <summary>
 /// Gets when the account was closed.
 /// </summary>
 public DateTimeOffset ClosedAt { get; }
}
