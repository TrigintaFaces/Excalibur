// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// ============================================================================
// S2: Event Sourcing Domain Events
// ============================================================================
// These domain events derive from DomainEvent, which supplies EventId, OccurredAt and Metadata.
// Each event carries only its own business data -- including AccountId, which is the account's
// identity as a domain field. An event has no aggregate id and no stream version of its own: the
// aggregate id is a parameter to the event store, and the version is assigned by the store at
// append time and read back on StoredEvent.Version.
// ============================================================================

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Aot.Sample.EventSourcing;

/// <summary>
/// Raised when a new bank account is opened.
/// </summary>
[MessageName("Contoso.Banking.AccountOpenedEvent")]
public sealed record AccountOpenedEvent : DomainEvent
{
	/// <summary>Gets the account this event belongs to — the event's own business data.</summary>
	/// <remarks>
	/// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
	/// to the event store; the version is assigned by the store at append time and read back on
	/// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> are
	/// inherited from <c>DomainEvent</c>.
	/// </remarks>
	public required string AccountId { get; init; }

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
public sealed record FundsDepositedEvent : DomainEvent
{
	/// <summary>Gets the account this event belongs to — the event's own business data.</summary>
	/// <remarks>
	/// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
	/// to the event store; the version is assigned by the store at append time and read back on
	/// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> are
	/// inherited from <c>DomainEvent</c>.
	/// </remarks>
	public required string AccountId { get; init; }

	/// <summary>
	/// Gets or sets the deposit amount.
	/// </summary>
	public required decimal Amount { get; init; }
}

/// <summary>
/// Raised when funds are withdrawn from an account.
/// </summary>
[MessageName("Contoso.Banking.FundsWithdrawnEvent")]
public sealed record FundsWithdrawnEvent : DomainEvent
{
	/// <summary>Gets the account this event belongs to — the event's own business data.</summary>
	/// <remarks>
	/// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
	/// to the event store; the version is assigned by the store at append time and read back on
	/// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> are
	/// inherited from <c>DomainEvent</c>.
	/// </remarks>
	public required string AccountId { get; init; }

	/// <summary>
	/// Gets or sets the withdrawal amount.
	/// </summary>
	public required decimal Amount { get; init; }
}
