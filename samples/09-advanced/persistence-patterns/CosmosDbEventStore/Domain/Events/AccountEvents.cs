// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

namespace CosmosDbEventStoreSample.Domain.Events;

/// <summary>
/// Event raised when a bank account is opened.
/// </summary>
[MessageName("Contoso.Banking.AccountOpened")]
public sealed record AccountOpened(
	Guid AccountId,
	string AccountHolder,
	string AccountType,
	decimal InitialDeposit) : DomainEvent;

/// <summary>
/// Event raised when money is deposited into an account.
/// </summary>
[MessageName("Contoso.Banking.MoneyDeposited")]
public sealed record MoneyDeposited(Guid AccountId, decimal Amount, string Reference) : DomainEvent;

/// <summary>
/// Event raised when money is withdrawn from an account.
/// </summary>
[MessageName("Contoso.Banking.MoneyWithdrawn")]
public sealed record MoneyWithdrawn(Guid AccountId, decimal Amount, string Reference) : DomainEvent;

/// <summary>
/// Event raised when money is transferred between accounts.
/// </summary>
[MessageName("Contoso.Banking.MoneyTransferred")]
public sealed record MoneyTransferred(
	Guid AccountId,
	Guid TargetAccountId,
	decimal Amount,
	string Reference) : DomainEvent;

/// <summary>
/// Event raised when an account is closed.
/// </summary>
[MessageName("Contoso.Banking.AccountClosed")]
public sealed record AccountClosed(Guid AccountId, string Reason) : DomainEvent
{
	/// <summary>Gets when the account was closed.</summary>
	public DateTime ClosedAt { get; init; } = DateTime.UtcNow;
}
