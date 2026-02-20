// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace SqlServerEventStore.Domain;

/// <summary>
/// Bank account aggregate demonstrating SQL Server event sourcing.
/// </summary>
/// <remarks>
/// This aggregate demonstrates a banking domain with:
/// - Account creation with initial deposit
/// - Deposits and withdrawals with balance tracking
/// - Business rules (overdraft protection, deposit limits)
/// - Full event sourcing with SQL Server persistence
/// </remarks>
public class BankAccountAggregate : AggregateRoot<Guid>
{
	/// <summary>
	/// Maximum single deposit amount.
	/// </summary>
	public const decimal MaxDepositAmount = 50_000m;

	/// <summary>
	/// Minimum balance (no overdraft).
	/// </summary>
	public const decimal MinBalance = 0m;

	/// <summary>
	/// Initializes a new instance for rehydration from events.
	/// </summary>
	public BankAccountAggregate()
	{
	}

	/// <summary>
	/// Initializes a new instance with an identifier.
	/// </summary>
	public BankAccountAggregate(Guid id) : base(id)
	{
	}

	/// <summary>Gets the account holder name.</summary>
	public string HolderName { get; private set; } = string.Empty;

	/// <summary>Gets the current account balance.</summary>
	public decimal Balance { get; private set; }

	/// <summary>Gets the total deposits made.</summary>
	public decimal TotalDeposits { get; private set; }

	/// <summary>Gets the total withdrawals made.</summary>
	public decimal TotalWithdrawals { get; private set; }

	/// <summary>Gets the number of transactions.</summary>
	public int TransactionCount { get; private set; }

	/// <summary>Gets whether the account is active.</summary>
	public bool IsActive { get; private set; }

	/// <summary>Gets when the account was opened.</summary>
	public DateTimeOffset? OpenedAt { get; private set; }

	/// <summary>Gets when the account was closed (if closed).</summary>
	public DateTimeOffset? ClosedAt { get; private set; }

	/// <summary>
	/// Creates a new bank account with an initial deposit.
	/// </summary>
	public static BankAccountAggregate Open(Guid id, string holderName, decimal initialDeposit)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(holderName);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialDeposit);

		if (initialDeposit > MaxDepositAmount)
		{
			throw new ArgumentException(
				$"Initial deposit cannot exceed {MaxDepositAmount:C}",
				nameof(initialDeposit));
		}

		var account = new BankAccountAggregate(id);
		account.RaiseEvent(new AccountOpened(id, holderName, initialDeposit, account.Version));
		return account;
	}

	/// <summary>
	/// Deposits money into the account.
	/// </summary>
	public void Deposit(decimal amount, string description = "Deposit")
	{
		EnsureAccountActive();
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

		if (amount > MaxDepositAmount)
		{
			throw new InvalidOperationException(
				$"Single deposit cannot exceed {MaxDepositAmount:C}");
		}

		RaiseEvent(new MoneyDeposited(Id, amount, Balance + amount, description, Version));
	}

	/// <summary>
	/// Withdraws money from the account.
	/// </summary>
	public void Withdraw(decimal amount, string description = "Withdrawal")
	{
		EnsureAccountActive();
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

		if (Balance - amount < MinBalance)
		{
			throw new InvalidOperationException(
				$"Insufficient funds. Balance: {Balance:C}, Requested: {amount:C}");
		}

		RaiseEvent(new MoneyWithdrawn(Id, amount, Balance - amount, description, Version));
	}

	/// <summary>
	/// Closes the account. Balance must be zero.
	/// </summary>
	public void Close(string reason)
	{
		EnsureAccountActive();

		if (Balance != 0)
		{
			throw new InvalidOperationException(
				$"Cannot close account with non-zero balance: {Balance:C}");
		}

		RaiseEvent(new AccountClosed(Id, reason, Version));
	}

	/// <inheritdoc/>
	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		AccountOpened e => ApplyAccountOpened(e),
		MoneyDeposited e => ApplyMoneyDeposited(e),
		MoneyWithdrawn e => ApplyMoneyWithdrawn(e),
		AccountClosed e => ApplyAccountClosed(e),
		_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
	};

	private void EnsureAccountActive()
	{
		if (!IsActive)
		{
			throw new InvalidOperationException("Account is not active");
		}
	}

	private bool ApplyAccountOpened(AccountOpened e)
	{
		Id = e.AccountId;
		HolderName = e.HolderName;
		Balance = e.InitialDeposit;
		TotalDeposits = e.InitialDeposit;
		TransactionCount = 1;
		IsActive = true;
		OpenedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyMoneyDeposited(MoneyDeposited e)
	{
		Balance = e.NewBalance;
		TotalDeposits += e.Amount;
		TransactionCount++;
		return true;
	}

	private bool ApplyMoneyWithdrawn(MoneyWithdrawn e)
	{
		Balance = e.NewBalance;
		TotalWithdrawals += e.Amount;
		TransactionCount++;
		return true;
	}

	private bool ApplyAccountClosed(AccountClosed e)
	{
		IsActive = false;
		ClosedAt = e.OccurredAt;
		return true;
	}
}

#region Domain Events

/// <summary>
/// Event raised when a bank account is opened.
/// </summary>
public sealed record AccountOpened : DomainEvent
{
	public AccountOpened(Guid accountId, string holderName, decimal initialDeposit, long version)
		: base(accountId.ToString(), version)
	{
		AccountId = accountId;
		HolderName = holderName;
		InitialDeposit = initialDeposit;
	}

	/// <summary>Gets the account identifier.</summary>
	public Guid AccountId { get; init; }

	/// <summary>Gets the account holder name.</summary>
	public string HolderName { get; init; }

	/// <summary>Gets the initial deposit amount.</summary>
	public decimal InitialDeposit { get; init; }
}

/// <summary>
/// Event raised when money is deposited.
/// </summary>
public sealed record MoneyDeposited : DomainEvent
{
	public MoneyDeposited(Guid accountId, decimal amount, decimal newBalance, string description, long version)
		: base(accountId.ToString(), version)
	{
		AccountId = accountId;
		Amount = amount;
		NewBalance = newBalance;
		Description = description;
	}

	/// <summary>Gets the account identifier.</summary>
	public Guid AccountId { get; init; }

	/// <summary>Gets the deposit amount.</summary>
	public decimal Amount { get; init; }

	/// <summary>Gets the balance after deposit.</summary>
	public decimal NewBalance { get; init; }

	/// <summary>Gets the transaction description.</summary>
	public string Description { get; init; }
}

/// <summary>
/// Event raised when money is withdrawn.
/// </summary>
public sealed record MoneyWithdrawn : DomainEvent
{
	public MoneyWithdrawn(Guid accountId, decimal amount, decimal newBalance, string description, long version)
		: base(accountId.ToString(), version)
	{
		AccountId = accountId;
		Amount = amount;
		NewBalance = newBalance;
		Description = description;
	}

	/// <summary>Gets the account identifier.</summary>
	public Guid AccountId { get; init; }

	/// <summary>Gets the withdrawal amount.</summary>
	public decimal Amount { get; init; }

	/// <summary>Gets the balance after withdrawal.</summary>
	public decimal NewBalance { get; init; }

	/// <summary>Gets the transaction description.</summary>
	public string Description { get; init; }
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
	}

	/// <summary>Gets the account identifier.</summary>
	public Guid AccountId { get; init; }

	/// <summary>Gets the closure reason.</summary>
	public string Reason { get; init; }
}

#endregion
