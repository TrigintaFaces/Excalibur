// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// S2: Event Sourcing Domain Events
// ============================================================================
// These domain events implement IDomainEvent for event store compatibility.
// The aggregate sets EventId, AggregateId, Version, and OccurredAt via RaiseEvent().
// ============================================================================

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Aot.Sample.EventSourcing;

/// <summary>
/// Raised when a new bank account is opened.
/// </summary>
public sealed record AccountOpenedEvent : IDomainEvent
{
	/// <inheritdoc />
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public string AggregateId { get; set; } = string.Empty;

	/// <inheritdoc />
	public long Version { get; set; }

	/// <inheritdoc />
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public string EventType => nameof(AccountOpenedEvent);

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; set; }

	/// <summary>
	/// Gets or sets the account holder name.
	/// </summary>
	public required string HolderName { get; init; }

	/// <summary>
	/// Gets or sets the initial deposit amount.
	/// </summary>
	public required decimal InitialDeposit { get; init; }
}

/// <summary>
/// Raised when funds are deposited into an account.
/// </summary>
public sealed record FundsDepositedEvent : IDomainEvent
{
	/// <inheritdoc />
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public string AggregateId { get; set; } = string.Empty;

	/// <inheritdoc />
	public long Version { get; set; }

	/// <inheritdoc />
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public string EventType => nameof(FundsDepositedEvent);

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; set; }

	/// <summary>
	/// Gets or sets the deposit amount.
	/// </summary>
	public required decimal Amount { get; init; }
}

/// <summary>
/// Raised when funds are withdrawn from an account.
/// </summary>
public sealed record FundsWithdrawnEvent : IDomainEvent
{
	/// <inheritdoc />
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public string AggregateId { get; set; } = string.Empty;

	/// <inheritdoc />
	public long Version { get; set; }

	/// <inheritdoc />
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public string EventType => nameof(FundsWithdrawnEvent);

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; set; }

	/// <summary>
	/// Gets or sets the withdrawal amount.
	/// </summary>
	public required decimal Amount { get; init; }
}
