// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using CosmosDbEventStoreSample.Domain.Events;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace CosmosDbEventStoreSample.Domain.Aggregates;

/// <summary>
/// Represents the account status in its lifecycle.
/// </summary>
public enum AccountStatus
{
	/// <summary>Account is active and can accept transactions.</summary>
	Active,

	/// <summary>Account has been closed.</summary>
	Closed
}

/// <summary>
/// Bank account aggregate demonstrating event sourcing with Cosmos DB.
/// </summary>
/// <remarks>
/// <para>
/// This aggregate demonstrates:
/// <list type="bullet">
/// <item>Event sourcing with RaiseEvent for state changes</item>
/// <item>Pattern matching in ApplyEventInternal using switch expressions</item>
/// <item>Business invariant enforcement (e.g., no negative balance)</item>
/// <item>Static factory method for creation</item>
/// </list>
/// </para>
/// </remarks>
public class BankAccountAggregate : AggregateRoot<Guid>
{
	/// <summary>
	/// Initializes a new instance for rehydration from events.
	/// </summary>
	public BankAccountAggregate()
	{
	}

	/// <summary>
	/// Initializes a new instance with an identifier.
	/// </summary>
	/// <param name="id">The account identifier.</param>
	public BankAccountAggregate(Guid id) : base(id)
	{
	}

	/// <summary>Gets the account holder name.</summary>
	public string AccountHolder { get; private set; } = string.Empty;

	/// <summary>Gets the account type (e.g., Savings, Checking).</summary>
	public string AccountType { get; private set; } = string.Empty;

	/// <summary>Gets the current balance.</summary>
	public decimal Balance { get; private set; }

	/// <summary>Gets the account status.</summary>
	public AccountStatus Status { get; private set; }

	/// <summary>Gets when the account was opened.</summary>
	public DateTimeOffset OpenedAt { get; private set; }

	/// <summary>Gets when the account was closed (if closed).</summary>
	public DateTimeOffset? ClosedAt { get; private set; }

	/// <summary>Gets the total number of transactions.</summary>
	public int TransactionCount { get; private set; }

	/// <summary>
	/// Opens a new bank account.
	/// </summary>
	/// <param name="id">The account identifier.</param>
	/// <param name="accountHolder">The account holder's name.</param>
	/// <param name="accountType">The type of account.</param>
	/// <param name="initialDeposit">The initial deposit amount.</param>
	/// <returns>A new bank account aggregate.</returns>
	public static BankAccountAggregate Open(
		Guid id,
		string accountHolder,
		string accountType,
		decimal initialDeposit)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(accountHolder);
		ArgumentException.ThrowIfNullOrWhiteSpace(accountType);
		ArgumentOutOfRangeException.ThrowIfNegative(initialDeposit);

		var account = new BankAccountAggregate(id);
		account.RaiseEvent(new AccountOpened(id, accountHolder, accountType, initialDeposit, account.Version));
		return account;
	}

	/// <summary>
	/// Deposits money into the account.
	/// </summary>
	/// <param name="amount">The amount to deposit.</param>
	/// <param name="reference">A reference for the transaction.</param>
	public void Deposit(decimal amount, string reference)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
		ArgumentException.ThrowIfNullOrWhiteSpace(reference);

		EnsureAccountIsActive();

		RaiseEvent(new MoneyDeposited(Id, amount, reference, Version));
	}

	/// <summary>
	/// Withdraws money from the account.
	/// </summary>
	/// <param name="amount">The amount to withdraw.</param>
	/// <param name="reference">A reference for the transaction.</param>
	/// <exception cref="InvalidOperationException">Thrown if insufficient funds.</exception>
	public void Withdraw(decimal amount, string reference)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
		ArgumentException.ThrowIfNullOrWhiteSpace(reference);

		EnsureAccountIsActive();

		if (Balance < amount)
		{
			throw new InvalidOperationException(
				$"Insufficient funds. Available: {Balance:C}, Requested: {amount:C}");
		}

		RaiseEvent(new MoneyWithdrawn(Id, amount, reference, Version));
	}

	/// <summary>
	/// Transfers money to another account.
	/// </summary>
	/// <param name="targetAccountId">The target account identifier.</param>
	/// <param name="amount">The amount to transfer.</param>
	/// <param name="reference">A reference for the transaction.</param>
	/// <exception cref="InvalidOperationException">Thrown if insufficient funds.</exception>
	public void Transfer(Guid targetAccountId, decimal amount, string reference)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
		ArgumentException.ThrowIfNullOrWhiteSpace(reference);

		EnsureAccountIsActive();

		if (Balance < amount)
		{
			throw new InvalidOperationException(
				$"Insufficient funds for transfer. Available: {Balance:C}, Requested: {amount:C}");
		}

		RaiseEvent(new MoneyTransferred(Id, targetAccountId, amount, reference, Version));
	}

	/// <summary>
	/// Closes the account.
	/// </summary>
	/// <param name="reason">The reason for closing the account.</param>
	/// <exception cref="InvalidOperationException">Thrown if account has non-zero balance.</exception>
	public void Close(string reason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		EnsureAccountIsActive();

		if (Balance != 0)
		{
			throw new InvalidOperationException(
				$"Cannot close account with balance {Balance:C}. Please withdraw or transfer all funds first.");
		}

		RaiseEvent(new AccountClosed(Id, reason, Version));
	}

	/// <inheritdoc/>
	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		AccountOpened e => ApplyAccountOpened(e),
		MoneyDeposited e => ApplyMoneyDeposited(e),
		MoneyWithdrawn e => ApplyMoneyWithdrawn(e),
		MoneyTransferred e => ApplyMoneyTransferred(e),
		AccountClosed e => ApplyAccountClosed(e),
		_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
	};

	private bool ApplyAccountOpened(AccountOpened e)
	{
		Id = e.AccountId;
		AccountHolder = e.AccountHolder;
		AccountType = e.AccountType;
		Balance = e.InitialDeposit;
		Status = AccountStatus.Active;
		OpenedAt = e.OccurredAt;
		TransactionCount = e.InitialDeposit > 0 ? 1 : 0;
		return true;
	}

	private bool ApplyMoneyDeposited(MoneyDeposited e)
	{
		Balance += e.Amount;
		TransactionCount++;
		return true;
	}

	private bool ApplyMoneyWithdrawn(MoneyWithdrawn e)
	{
		Balance -= e.Amount;
		TransactionCount++;
		return true;
	}

	private bool ApplyMoneyTransferred(MoneyTransferred e)
	{
		Balance -= e.Amount;
		TransactionCount++;
		return true;
	}

	private bool ApplyAccountClosed(AccountClosed e)
	{
		Status = AccountStatus.Closed;
		ClosedAt = e.OccurredAt;
		return true;
	}

	private void EnsureAccountIsActive()
	{
		if (Status != AccountStatus.Active)
		{
			throw new InvalidOperationException(
				$"Cannot perform operation on account with status '{Status}'.");
		}
	}
}
