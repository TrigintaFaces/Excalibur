// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace CosmosDbEventStoreSample.Domain.Events;

/// <summary>
/// Event raised when a bank account is opened.
/// </summary>
public sealed record AccountOpened : DomainEvent
{
	public AccountOpened(Guid accountId, string accountHolder, string accountType, decimal initialDeposit, long version)
		: base(accountId.ToString(), version)
	{
		AccountId = accountId;
		AccountHolder = accountHolder;
		AccountType = accountType;
		InitialDeposit = initialDeposit;
	}

	/// <summary>Gets the account identifier.</summary>
	public Guid AccountId { get; init; }

	/// <summary>Gets the account holder's name.</summary>
	public string AccountHolder { get; init; }

	/// <summary>Gets the type of account.</summary>
	public string AccountType { get; init; }

	/// <summary>Gets the initial deposit amount.</summary>
	public decimal InitialDeposit { get; init; }
}

/// <summary>
/// Event raised when money is deposited into an account.
/// </summary>
public sealed record MoneyDeposited : DomainEvent
{
	public MoneyDeposited(Guid accountId, decimal amount, string reference, long version)
		: base(accountId.ToString(), version)
	{
		AccountId = accountId;
		Amount = amount;
		Reference = reference;
	}

	/// <summary>Gets the account identifier.</summary>
	public Guid AccountId { get; init; }

	/// <summary>Gets the deposit amount.</summary>
	public decimal Amount { get; init; }

	/// <summary>Gets the transaction reference.</summary>
	public string Reference { get; init; }
}

/// <summary>
/// Event raised when money is withdrawn from an account.
/// </summary>
public sealed record MoneyWithdrawn : DomainEvent
{
	public MoneyWithdrawn(Guid accountId, decimal amount, string reference, long version)
		: base(accountId.ToString(), version)
	{
		AccountId = accountId;
		Amount = amount;
		Reference = reference;
	}

	/// <summary>Gets the account identifier.</summary>
	public Guid AccountId { get; init; }

	/// <summary>Gets the withdrawal amount.</summary>
	public decimal Amount { get; init; }

	/// <summary>Gets the transaction reference.</summary>
	public string Reference { get; init; }
}

/// <summary>
/// Event raised when money is transferred between accounts.
/// </summary>
public sealed record MoneyTransferred : DomainEvent
{
	public MoneyTransferred(Guid accountId, Guid targetAccountId, decimal amount, string reference, long version)
		: base(accountId.ToString(), version)
	{
		AccountId = accountId;
		TargetAccountId = targetAccountId;
		Amount = amount;
		Reference = reference;
	}

	/// <summary>Gets the source account identifier.</summary>
	public Guid AccountId { get; init; }

	/// <summary>Gets the target account identifier.</summary>
	public Guid TargetAccountId { get; init; }

	/// <summary>Gets the transfer amount.</summary>
	public decimal Amount { get; init; }

	/// <summary>Gets the transaction reference.</summary>
	public string Reference { get; init; }
}

/// <summary>
/// Event raised when an account is closed.
/// </summary>
public sealed record AccountClosed : DomainEvent
{
	public AccountClosed(Guid accountId, string reason, long version)
		: base(accountId.ToString(), version)
	{
		AccountId = accountId;
		Reason = reason;
		ClosedAt = DateTime.UtcNow;
	}

	/// <summary>Gets the account identifier.</summary>
	public Guid AccountId { get; init; }

	/// <summary>Gets the reason for closing.</summary>
	public string Reason { get; init; }

	/// <summary>Gets when the account was closed.</summary>
	public DateTime ClosedAt { get; init; }
}
