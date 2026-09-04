// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// S2: Event Sourcing Domain Events
// ============================================================================
// These domain events implement IDomainEvent for event store compatibility.
// Each event carries EventId, AggregateId, Version, and OccurredAt as its own properties; the
// aggregate sets AggregateId when it raises the event. The authoritative stream position lives in
// the persistence envelope, not on the event payload.
// ============================================================================

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Aot.Sample.EventSourcing;

/// <summary>
/// Raised when a new bank account is opened.
/// </summary>
[MessageName("Contoso.Banking.AccountOpenedEvent")]
public sealed record AccountOpenedEvent : IDomainEvent
{
	/// <inheritdoc />
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the aggregate identifier this event belongs to. A sample-local convenience on the
	/// event; the authoritative stream identity lives in the persistence envelope.
	/// </summary>
	public string AggregateId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the stream position for this event. A sample-local convenience; the authoritative
	/// version lives in the persistence envelope.
	/// </summary>
	public long Version { get; set; }

	/// <inheritdoc />
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;


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
[MessageName("Contoso.Banking.FundsDepositedEvent")]
public sealed record FundsDepositedEvent : IDomainEvent
{
	/// <inheritdoc />
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the aggregate identifier this event belongs to. A sample-local convenience on the
	/// event; the authoritative stream identity lives in the persistence envelope.
	/// </summary>
	public string AggregateId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the stream position for this event. A sample-local convenience; the authoritative
	/// version lives in the persistence envelope.
	/// </summary>
	public long Version { get; set; }

	/// <inheritdoc />
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;


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
[MessageName("Contoso.Banking.FundsWithdrawnEvent")]
public sealed record FundsWithdrawnEvent : IDomainEvent
{
	/// <inheritdoc />
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the aggregate identifier this event belongs to. A sample-local convenience on the
	/// event; the authoritative stream identity lives in the persistence envelope.
	/// </summary>
	public string AggregateId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the stream position for this event. A sample-local convenience; the authoritative
	/// version lives in the persistence envelope.
	/// </summary>
	public long Version { get; set; }

	/// <inheritdoc />
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;


	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; set; }

	/// <summary>
	/// Gets or sets the withdrawal amount.
	/// </summary>
	public required decimal Amount { get; init; }
}
