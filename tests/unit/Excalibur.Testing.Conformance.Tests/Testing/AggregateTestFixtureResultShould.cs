// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Testing;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing;

/// <summary>
/// Unit tests for <see cref="AggregateTestFixtureResult{TAggregate}"/>.
/// These tests exercise the result object assertions directly.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class AggregateTestFixtureResultShould
{
	#region ShouldRaise Tests

	[Fact]
	public void ShouldRaise_ThrowsWhenPredicateIsNull()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(5))
			.Then();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			fixture.ShouldRaise<CounterIncrementedBy>(null!));
	}

	[Fact]
	public void ShouldRaise_WithPredicate_ThrowsWhenTypeMatchesButPredicateFails()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(5))
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 999));

		exception.Message.ShouldContain("predicate");
		exception.Message.ShouldContain("1 event(s)");
	}

	[Fact]
	public void ShouldRaise_WithPredicate_ThrowsWhenNoEventsOfTypeExist()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 5));

		exception.Message.ShouldContain("predicate");
		exception.Message.ShouldContain("0 event(s)");
	}

	[Fact]
	public void ShouldRaise_ChainedCalls_ShouldReturnSameInstance()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg =>
			{
				agg.Increment();
				agg.IncrementBy(10);
			})
			.Then();

		// Act
		var result1 = fixture.ShouldRaise<CounterIncremented>();
		var result2 = result1.ShouldRaise<CounterIncrementedBy>();

		// Assert - method chaining should return same instance
		result2.ShouldBe(result1);
	}

	#endregion

	#region ShouldRaiseNoEvents Tests

	[Fact]
	public void ShouldRaiseNoEvents_ThrowsWhenMultipleEventsRaised()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg =>
			{
				agg.Increment();
				agg.Increment();
				agg.IncrementBy(5);
			})
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldRaiseNoEvents());

		exception.Message.ShouldContain("no events");
		exception.Message.ShouldContain("CounterIncremented");
		exception.Message.ShouldContain("CounterIncrementedBy");
	}

	#endregion

	#region StateShould Tests

	[Fact]
	public void StateShould_ThrowsWhenPredicateIsNull()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			fixture.StateShould(null!));
	}

	[Fact]
	public void StateShould_ChainedCalls_ShouldReturnSameInstance()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("id", 1, "Test"))
			.When(agg => agg.IncrementBy(5))
			.Then();

		// Act
		var result1 = fixture.StateShould(agg => agg.Counter == 5);
		var result2 = result1.StateShould(agg => agg.IsInitialized);
		var result3 = result2.StateShould(agg => agg.Name == "Test");

		// Assert - method chaining should return same instance
		result3.ShouldBe(result1);
	}

	[Fact]
	public void StateShould_UsesDefaultMessageWhenCustomMessageIsNull()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.StateShould(agg => agg.Counter == 999, null));

		exception.Message.ShouldBe("Aggregate state did not match the expected predicate.");
	}

	#endregion

	#region AssertAggregate Tests

	[Fact]
	public void AssertAggregate_ThrowsWhenAssertionIsNull()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			fixture.AssertAggregate(null!));
	}

	[Fact]
	public void AssertAggregate_ChainedCalls_ShouldReturnSameInstance()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(7))
			.Then();

		// Act
		var capturedCounter = 0;
		var capturedName = string.Empty;
		var result1 = fixture.AssertAggregate(agg => capturedCounter = agg.Counter);
		var result2 = result1.AssertAggregate(agg => capturedName = agg.Name);

		// Assert - method chaining should return same instance
		result2.ShouldBe(result1);
		capturedCounter.ShouldBe(7);
		capturedName.ShouldBeEmpty();
	}

	[Fact]
	public void AssertAggregate_PropagatesExceptionFromAssertion()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			fixture.AssertAggregate(_ => throw new InvalidOperationException("Custom assertion failed")));
	}

	#endregion

	#region ShouldThrow Tests

	[Fact]
	public void ShouldThrow_WithMessageContains_CaseInsensitive()
	{
		// Arrange & Act & Assert - Should match case-insensitively
		new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(-1))
			.ShouldThrow<ArgumentException>("POSITIVE");
	}

	[Fact]
	public void ShouldThrow_WithMessageContains_FailsWhenMessageNotFound()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(-1))
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldThrow<ArgumentException>("completely unrelated text"));

		exception.Message.ShouldContain("completely unrelated text");
		exception.Message.ShouldContain("Amount must be positive");
	}

	#endregion

	#region ShouldNotThrow Tests

	[Fact]
	public void ShouldNotThrow_ChainedCalls_ShouldReturnSameInstance()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then();

		// Act
		var result1 = fixture.ShouldNotThrow();
		var result2 = result1.ShouldRaise<CounterIncremented>();

		// Assert - method chaining should work
		result2.ShouldBe(result1);
	}

	[Fact]
	public void ShouldNotThrow_IncludesExceptionDetailsInFailureMessage()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("id", 1, "Test"))
			.When(agg => agg.Initialize("Duplicate"))
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldNotThrow());

		exception.Message.ShouldContain("no exception");
		exception.Message.ShouldContain("InvalidOperationException");
		exception.Message.ShouldContain("already initialized");
	}

	#endregion

	#region Combined Workflow Tests

	[Fact]
	public void CanChainMultipleAssertionTypes()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("order-1", 1, "Order"))
			.When(agg => agg.IncrementBy(42))
			.Then()
			.ShouldNotThrow()
			.ShouldRaise<CounterIncrementedBy>()
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 42)
			.StateShould(agg => agg.Counter == 42)
			.StateShould(agg => agg.Name == "Order", "Name should be Order")
			.StateShould(agg => agg.IsInitialized)
			.AssertAggregate(agg =>
			{
				agg.Counter.ShouldBe(42);
				agg.Name.ShouldBe("Order");
			});
	}

	#endregion
}
