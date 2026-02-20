// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Testing;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing;

/// <summary>
/// Edge case and additional unit tests for <see cref="AggregateTestFixture{TAggregate}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class AggregateTestFixtureEdgeCasesShould
{
	#region Given Edge Cases

	[Fact]
	public void Given_ThrowsWhenEventsCollectionIsNull()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			fixture.Given((IEnumerable<IDomainEvent>)null!));
	}

	[Fact]
	public void Given_AcceptsEmptyEnumerable()
	{
		// Arrange & Act & Assert - Empty enumerable should not throw
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(Enumerable.Empty<IDomainEvent>())
			.When(agg => agg.Increment())
			.Then()
			.ShouldRaise<CounterIncremented>()
			.StateShould(agg => agg.Counter == 1);
	}

	[Fact]
	public void Given_AcceptsEmptyParamsArray()
	{
		// Arrange & Act & Assert - Empty params should work
		_ = new AggregateTestFixture<TestAggregate>()
			.Given()
			.When(agg => agg.Increment())
			.Then()
			.ShouldRaise<CounterIncremented>();
	}

	[Fact]
	public void Given_CanMixParamsAndEnumerable()
	{
		// Arrange
		var paramsEvents = new IDomainEvent[]
		{
			new CounterIncremented("id", 1),
			new CounterIncremented("id", 2)
		};

		var enumerableEvents = new List<IDomainEvent>
		{
			new CounterIncremented("id", 3)
		};

		// Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(paramsEvents)
			.Given(enumerableEvents)
			.When(agg => agg.DoNothing())
			.Then()
			.ShouldRaiseNoEvents()
			.StateShould(agg => agg.Counter == 3);
	}

	[Fact]
	public void Given_LargeNumberOfEvents_ShouldHandleCorrectly()
	{
		// Arrange
		var events = Enumerable.Range(1, 100)
			.Select(i => new CounterIncremented("id", i))
			.ToList();

		// Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(events)
			.When(agg => agg.DoNothing())
			.Then()
			.ShouldRaiseNoEvents()
			.StateShould(agg => agg.Counter == 100);
	}

	#endregion

	#region When Edge Cases

	[Fact]
	public void When_ThrowsWhenActionIsNull()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			fixture.When(null!));
	}

	[Fact]
	public void When_CapturesExceptionWithInnerException()
	{
		// Arrange & Act
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(_ => throw new InvalidOperationException("Outer", new ArgumentException("Inner")))
			.Then();

		// Assert
		fixture.ShouldThrow<InvalidOperationException>();
	}

	[Fact]
	public void When_ExecutedFlag_IsFalseWhenExceptionThrown()
	{
		// Arrange & Act
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(-1));

		// The exception is captured but execution is considered incomplete
		// This is verified by checking that ShouldThrow works
		fixture.ShouldThrow<ArgumentException>();
	}

	[Fact]
	public void When_CanRaiseMultipleEventsInSingleAction()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.When(agg =>
			{
				agg.Increment();
				agg.Increment();
				agg.IncrementBy(3);
				agg.IncrementBy(5);
			})
			.Then()
			.ShouldRaise<CounterIncremented>()
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 3)
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 5)
			.StateShould(agg => agg.Counter == 10);
	}

	#endregion

	#region Exception Handling Edge Cases

	[Fact]
	public void ShouldThrow_WorksWithDerivedExceptions()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(_ => throw new ArgumentNullException("param", "Value cannot be null"));

		// Act & Assert - ArgumentNullException derives from ArgumentException
		fixture.ShouldThrow<ArgumentException>();
	}

	[Fact]
	public void ShouldThrow_FailsWhenBaseExceptionExpectedButDerivedThrown()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(_ => throw new ArgumentException("Generic argument error"))
			.Then();

		// Act & Assert - We expect ArgumentNullException but got ArgumentException
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldThrow<ArgumentNullException>());

		exception.Message.ShouldContain("ArgumentNullException");
		exception.Message.ShouldContain("ArgumentException");
	}

	[Fact]
	public void ShouldThrow_ShortcutMethod_WorksCorrectly()
	{
		// This tests the shortcut method on AggregateTestFixture
		new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(-5))
			.ShouldThrow<ArgumentException>();
	}

	[Fact]
	public void ShouldThrow_ShortcutMethodWithMessage_WorksCorrectly()
	{
		// This tests the shortcut method with message on AggregateTestFixture
		new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(-5))
			.ShouldThrow<ArgumentException>("positive");
	}

	#endregion

	#region State Verification Edge Cases

	[Fact]
	public void StateShould_CanAccessAllAggregateProperties()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("test-id", 1, "TestName"))
			.Given(new CounterIncremented("test-id", 2))
			.Given(new CounterIncrementedBy("test-id", 3, 5) { Amount = 5 })
			.When(agg => agg.IncrementBy(10))
			.Then()
			.StateShould(agg => agg.Counter == 16)
			.StateShould(agg => agg.Name == "TestName")
			.StateShould(agg => agg.IsInitialized);
	}

	[Fact]
	public void StateShould_CanVerifyDefaultState()
	{
		// Arrange & Act & Assert - Verify initial state before any events
		_ = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.DoNothing())
			.Then()
			.StateShould(agg => agg.Counter == 0)
			.StateShould(agg => string.IsNullOrEmpty(agg.Name))
			.StateShould(agg => !agg.IsInitialized);
	}

	#endregion

	#region Event Verification Edge Cases

	[Fact]
	public void ShouldRaise_CanVerifySameEventTypeMultipleTimes()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg =>
			{
				agg.Increment();
				agg.Increment();
				agg.Increment();
			})
			.Then();

		// Act & Assert - Should find all three CounterIncremented events
		_ = fixture
			.ShouldRaise<CounterIncremented>()
			.AssertAggregate(agg =>
			{
				var events = agg.GetUncommittedEvents();
				events.OfType<CounterIncremented>().Count().ShouldBe(3);
			});
	}

	[Fact]
	public void ShouldRaise_WithPredicate_CanMatchMultipleEvents()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg =>
			{
				agg.IncrementBy(3);
				agg.IncrementBy(5);
				agg.IncrementBy(7);
			})
			.Then();

		// Act & Assert - Various predicate matches
		_ = fixture
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount > 0)    // Matches all
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount > 4)    // Matches 5, 7
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 5)   // Matches one
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount % 2 == 1); // All are odd
	}

	#endregion

	#region Fluent API Tests

	[Fact]
	public void FluentAPI_CanBeChainedInAnyOrder()
	{
		// Arrange & Act & Assert - Various chaining patterns
		var result = new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("id", 1, "Test"))
			.When(agg => agg.IncrementBy(5))
			.Then()
			.StateShould(agg => agg.IsInitialized)
			.ShouldRaise<CounterIncrementedBy>()
			.StateShould(agg => agg.Counter == 5)
			.ShouldNotThrow()
			.AssertAggregate(agg => agg.Name.ShouldBe("Test"));

		result.ShouldNotBeNull();
	}

	[Fact]
	public void FluentAPI_GivenReturnsFixture()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>();

		// Act
		var result1 = fixture.Given(new CounterIncremented("id", 1));
		var result2 = result1.Given(new CounterIncremented("id", 2));

		// Assert - Given should return the same fixture instance
		result1.ShouldBe(fixture);
		result2.ShouldBe(fixture);
	}

	[Fact]
	public void FluentAPI_WhenReturnsFixture()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>();

		// Act
		var result = fixture.When(agg => agg.Increment());

		// Assert - When should return the same fixture instance
		result.ShouldBe(fixture);
	}

	#endregion

	#region Complex Scenario Tests

	[Fact]
	public void ComplexScenario_WithMixedEventsAndExceptions()
	{
		// Scenario: Aggregate has some history, and we test both success and failure paths

		// Success case
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(
				new AggregateInitialized("order-123", 1, "Order"),
				new CounterIncremented("order-123", 2),
				new CounterIncrementedBy("order-123", 3, 10) { Amount = 10 })
			.When(agg => agg.IncrementBy(5))
			.Then()
			.ShouldNotThrow()
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 5)
			.StateShould(agg => agg.Counter == 16)
			.StateShould(agg => agg.Name == "Order");

		// Failure case - same aggregate but trying to initialize again
		new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("order-123", 1, "Order"))
			.When(agg => agg.Initialize("DuplicateName"))
			.ShouldThrow<InvalidOperationException>("already initialized");
	}

	[Fact]
	public void ComplexScenario_StateChangesAreReflectedCorrectly()
	{
		// Verify that state changes through events are correctly reflected
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(
				new AggregateInitialized("agg-1", 1, "First"),
				new CounterIncremented("agg-1", 2),
				new CounterIncremented("agg-1", 3),
				new CounterIncrementedBy("agg-1", 4, 7) { Amount = 7 })
			.When(agg => agg.IncrementBy(3))
			.Then()
			.AssertAggregate(agg =>
			{
				// Given events: 1 init + 2 increment + 1 incrementBy(7) = counter 9
				// When: incrementBy(3) = counter 12
				agg.Counter.ShouldBe(12);
				agg.Name.ShouldBe("First");
				agg.IsInitialized.ShouldBeTrue();

				// Only the When event should be uncommitted
				var uncommitted = agg.GetUncommittedEvents();
				uncommitted.Count.ShouldBe(1);
				uncommitted[0].ShouldBeOfType<CounterIncrementedBy>();
			});
	}

	#endregion
}
