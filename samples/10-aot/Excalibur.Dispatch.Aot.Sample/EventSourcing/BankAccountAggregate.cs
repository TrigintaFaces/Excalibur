// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// S2: Event-Sourced Aggregate (AOT-Safe Pattern Matching)
// ============================================================================
// Demonstrates the AggregateRoot base class with:
// - Pattern-matching event application (no reflection, <10ns per event)
// - Immutable event records
// - Uncommitted events collection for persistence
// - LoadFromHistory for aggregate reconstruction
// ============================================================================

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace Excalibur.Dispatch.Aot.Sample.EventSourcing;

/// <summary>
/// A simple bank account aggregate demonstrating event sourcing with AOT compatibility.
/// </summary>
/// <remarks>
/// <para>
/// AOT Key Points:
/// <list type="bullet">
/// <item>ApplyEventInternal uses a switch expression (pattern matching) -- zero reflection</item>
/// <item>All event types are known at compile time -- trimmer-safe</item>
/// <item>No Activator.CreateInstance or MakeGenericType anywhere</item>
/// </list>
/// </para>
/// </remarks>
public sealed class BankAccountAggregate : AggregateRoot
{
	/// <summary>
	/// Gets the account holder name.
	/// </summary>
	public string HolderName { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the current balance.
	/// </summary>
	public decimal Balance { get; private set; }

	/// <summary>
	/// Gets a value indicating whether the account is open.
	/// </summary>
	public bool IsOpen { get; private set; }

	/// <summary>
	/// Opens a new bank account with an initial deposit.
	/// </summary>
	/// <param name="accountId">The account identifier.</param>
	/// <param name="holderName">The account holder name.</param>
	/// <param name="initialDeposit">The initial deposit amount.</param>
	public void Open(string accountId, string holderName, decimal initialDeposit)
	{
		if (IsOpen)
		{
			throw new InvalidOperationException("Account is already open.");
		}

		if (initialDeposit < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(initialDeposit), "Initial deposit cannot be negative.");
		}

		RaiseEvent(new AccountOpenedEvent
		{
			AggregateId = accountId,
			HolderName = holderName,
			InitialDeposit = initialDeposit
		});
	}

	/// <summary>
	/// Deposits funds into the account.
	/// </summary>
	/// <param name="amount">The amount to deposit.</param>
	public void Deposit(decimal amount)
	{
		if (!IsOpen)
		{
			throw new InvalidOperationException("Account is not open.");
		}

		if (amount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be positive.");
		}

		RaiseEvent(new FundsDepositedEvent { Amount = amount });
	}

	/// <summary>
	/// Withdraws funds from the account.
	/// </summary>
	/// <param name="amount">The amount to withdraw.</param>
	public void Withdraw(decimal amount)
	{
		if (!IsOpen)
		{
			throw new InvalidOperationException("Account is not open.");
		}

		if (amount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(amount), "Withdrawal amount must be positive.");
		}

		if (amount > Balance)
		{
			throw new InvalidOperationException($"Insufficient funds. Balance: {Balance}, Requested: {amount}");
		}

		RaiseEvent(new FundsWithdrawnEvent { Amount = amount });
	}

	/// <inheritdoc />
	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		AccountOpenedEvent e => Apply(e),
		FundsDepositedEvent e => Apply(e),
		FundsWithdrawnEvent e => Apply(e),
		_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
	};

	private bool Apply(AccountOpenedEvent e)
	{
		Id = e.AggregateId;
		HolderName = e.HolderName;
		Balance = e.InitialDeposit;
		IsOpen = true;
		return true;
	}

	private bool Apply(FundsDepositedEvent e)
	{
		Balance += e.Amount;
		return true;
	}

	private bool Apply(FundsWithdrawnEvent e)
	{
		Balance -= e.Amount;
		return true;
	}
}
