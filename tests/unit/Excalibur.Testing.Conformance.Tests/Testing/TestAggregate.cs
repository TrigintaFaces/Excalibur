// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Testing;

/// <summary>
/// Test domain event representing a counter increment.
/// </summary>
internal sealed record CounterIncremented : DomainEvent
{
	public override string AggregateId { get; init; } = string.Empty;
}

/// <summary>
/// Test domain event representing a counter increment by a specific amount.
/// </summary>
internal sealed record CounterIncrementedBy : DomainEvent
{
	public int Amount { get; init; }
	public override string AggregateId { get; init; } = string.Empty;
}

/// <summary>
/// Test domain event representing aggregate initialization.
/// </summary>
internal sealed record AggregateInitialized : DomainEvent
{
	public string Name { get; init; } = string.Empty;
	public override string AggregateId { get; init; } = string.Empty;
}

/// <summary>
/// Test aggregate for unit testing AggregateTestFixture.
/// </summary>
internal sealed class TestAggregate : AggregateRoot
{
	public TestAggregate()
	{ }

	public TestAggregate(string id) : base(id)
	{
	}

	public int Counter { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public bool IsInitialized { get; private set; }

	/// <summary>
	/// Increments the counter by 1.
	/// </summary>
	public void Increment()
	{
		RaiseEvent(new CounterIncremented { AggregateId = Id, Version = Version });
	}

	/// <summary>
	/// Increments the counter by a specified amount.
	/// </summary>
	/// <param name="amount">The amount to increment by.</param>
	/// <exception cref="ArgumentException">Thrown when amount is not positive.</exception>
	public void IncrementBy(int amount)
	{
		if (amount <= 0)
		{
			throw new ArgumentException("Amount must be positive", nameof(amount));
		}

		RaiseEvent(new CounterIncrementedBy { AggregateId = Id, Version = Version, Amount = amount });
	}

	/// <summary>
	/// Initializes the aggregate with a name.
	/// </summary>
	/// <param name="name">The name to set.</param>
	/// <exception cref="InvalidOperationException">Thrown when already initialized.</exception>
	public void Initialize(string name)
	{
		if (IsInitialized)
		{
			throw new InvalidOperationException("Aggregate is already initialized");
		}

		RaiseEvent(new AggregateInitialized { AggregateId = Id, Version = Version, Name = name });
	}

	/// <summary>
	/// Does nothing - used for testing no-event scenarios.
	/// </summary>
	public void DoNothing()
	{
		// Intentionally does nothing - no events raised
	}

	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case CounterIncremented:
				Counter++;
				break;

			case CounterIncrementedBy e:
				Counter += e.Amount;
				break;

			case AggregateInitialized e:
				Name = e.Name;
				IsInitialized = true;
				break;
		}
	}
}
