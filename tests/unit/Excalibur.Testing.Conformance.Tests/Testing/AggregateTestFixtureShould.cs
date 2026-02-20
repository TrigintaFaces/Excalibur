// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Testing;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing;

/// <summary>
/// Unit tests for <see cref="AggregateTestFixture{TAggregate}"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AggregateTestFixtureShould
{
	#region Given Tests

	[Fact]
	public void Apply_Single_Given_Event()
	{
		// Arrange & Act
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("test-id", 1, "Test"))
			.When(agg => agg.Increment())
			.Then()
			.ShouldRaise<CounterIncremented>()
			.StateShould(agg => agg.IsInitialized)
			.StateShould(agg => agg.Name == "Test");
	}

	[Fact]
	public void Apply_Multiple_Given_Events()
	{
		// Arrange & Act
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(
				new AggregateInitialized("test-id", 1, "Test"),
				new CounterIncremented("test-id", 2),
				new CounterIncremented("test-id", 3))
			.When(agg => agg.Increment())
			.Then()
			.StateShould(agg => agg.Counter == 3, "Counter should be 3 after 2 Given + 1 When");
	}

	[Fact]
	public void Chain_Multiple_Given_Calls()
	{
		// Arrange & Act
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("test-id", 1, "Test"))
			.Given(new CounterIncremented("test-id", 2))
			.Given(new CounterIncremented("test-id", 3))
			.When(agg => agg.Increment())
			.Then()
			.StateShould(agg => agg.Counter == 3);
	}

	[Fact]
	public void Handle_Empty_Given()
	{
		// Arrange & Act
		_ = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then()
			.ShouldRaise<CounterIncremented>()
			.StateShould(agg => agg.Counter == 1);
	}

	[Fact]
	public void Accept_Given_With_IEnumerable()
	{
		// Arrange
		var events = new List<CounterIncremented>
		{
			new("test-id", 1),
			new("test-id", 2),
			new("test-id", 3)
		};

		// Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(events)
			.When(agg => agg.DoNothing())
			.Then()
			.ShouldRaiseNoEvents()
			.StateShould(agg => agg.Counter == 3);
	}

	#endregion Given Tests

	#region When Tests

	[Fact]
	public void Execute_When_Action_On_Aggregate()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(5))
			.Then()
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 5)
			.StateShould(agg => agg.Counter == 5);
	}

	[Fact]
	public void Catch_Exception_In_When()
	{
		// Arrange & Act & Assert
		new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(-1))
			.ShouldThrow<ArgumentException>();
	}

	[Fact]
	public void Only_Capture_Events_From_When_Action()
	{
		// Given events should NOT appear in uncommitted events
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(
				new CounterIncremented("test-id", 1),
				new CounterIncremented("test-id", 2))
			.When(agg => agg.Increment())
			.Then()
			.AssertAggregate(agg =>
			{
				var events = agg.GetUncommittedEvents();
				events.Count.ShouldBe(1, "Only the When event should be uncommitted");
			});
	}

	#endregion When Tests

	#region ShouldRaise Tests

	[Fact]
	public void Pass_When_Event_Was_Raised()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then()
			.ShouldRaise<CounterIncremented>();
	}

	[Fact]
	public void Fail_When_Event_Was_Not_Raised()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldRaise<AggregateInitialized>());

		exception.Message.ShouldContain("AggregateInitialized");
		exception.Message.ShouldContain("was not raised");
	}

	[Fact]
	public void Pass_When_Event_Matches_Predicate()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(10))
			.Then()
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 10);
	}

	[Fact]
	public void Fail_When_Event_Does_Not_Match_Predicate()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(5))
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 100));

		exception.Message.ShouldContain("predicate");
	}

	[Fact]
	public void Allow_Chaining_Multiple_ShouldRaise()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.When(agg =>
			{
				agg.Increment();
				agg.IncrementBy(5);
			})
			.Then()
			.ShouldRaise<CounterIncremented>()
			.ShouldRaise<CounterIncrementedBy>();
	}

	#endregion ShouldRaise Tests

	#region ShouldRaiseNoEvents Tests

	[Fact]
	public void Pass_When_No_Events_Raised()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.DoNothing())
			.Then()
			.ShouldRaiseNoEvents();
	}

	[Fact]
	public void Fail_When_Events_Were_Raised()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldRaiseNoEvents());

		exception.Message.ShouldContain("no events");
	}

	#endregion ShouldRaiseNoEvents Tests

	#region StateShould Tests

	[Fact]
	public void Pass_When_State_Matches()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(42))
			.Then()
			.StateShould(agg => agg.Counter == 42);
	}

	[Fact]
	public void Fail_When_State_Does_Not_Match()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(10))
			.Then();

		// Act & Assert
		_ = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.StateShould(agg => agg.Counter == 999));
	}

	[Fact]
	public void Include_Custom_Message_In_Failure()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.StateShould(agg => agg.Counter == 100, "Counter should be 100 for this test"));

		exception.Message.ShouldBe("Counter should be 100 for this test");
	}

	[Fact]
	public void Allow_Chaining_Multiple_StateShould()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("test-id", 1, "MyAggregate"))
			.When(agg => agg.IncrementBy(7))
			.Then()
			.StateShould(agg => agg.Counter == 7)
			.StateShould(agg => agg.Name == "MyAggregate")
			.StateShould(agg => agg.IsInitialized);
	}

	#endregion StateShould Tests

	#region AssertAggregate Tests

	[Fact]
	public void Allow_Custom_Assertions_Via_AssertAggregate()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("test-id", 1, "CustomTest"))
			.When(agg => agg.IncrementBy(25))
			.Then()
			.AssertAggregate(agg =>
			{
				agg.Counter.ShouldBe(25);
				agg.Name.ShouldBe("CustomTest");
				agg.IsInitialized.ShouldBeTrue();
			});
	}

	#endregion AssertAggregate Tests

	#region ShouldThrow Tests

	[Fact]
	public void Pass_When_Expected_Exception_Thrown()
	{
		// Arrange & Act & Assert
		new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(-5))
			.ShouldThrow<ArgumentException>();
	}

	[Fact]
	public void Fail_When_No_Exception_Thrown()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(5))
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldThrow<ArgumentException>());

		exception.Message.ShouldContain("no exception was thrown");
	}

	[Fact]
	public void Fail_When_Different_Exception_Thrown()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("test-id", 1, "Test"))
			.When(agg => agg.Initialize("Duplicate")) // Throws InvalidOperationException
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldThrow<ArgumentException>());

		exception.Message.ShouldContain("ArgumentException");
		exception.Message.ShouldContain("InvalidOperationException");
	}

	[Fact]
	public void Pass_When_Exception_Message_Contains_Expected_Text()
	{
		// Arrange & Act & Assert
		new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(-1))
			.ShouldThrow<ArgumentException>("positive");
	}

	[Fact]
	public void Fail_When_Exception_Message_Does_Not_Contain_Expected_Text()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(-1))
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldThrow<ArgumentException>("nonexistent text"));

		exception.Message.ShouldContain("nonexistent text");
	}

	#endregion ShouldThrow Tests

	#region ShouldNotThrow Tests

	[Fact]
	public void Pass_When_No_Exception_For_ShouldNotThrow()
	{
		// Arrange & Act & Assert
		_ = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.Increment())
			.Then()
			.ShouldNotThrow()
			.ShouldRaise<CounterIncremented>();
	}

	[Fact]
	public void Fail_When_Exception_Thrown_For_ShouldNotThrow()
	{
		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.IncrementBy(-1))
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldNotThrow());

		exception.Message.ShouldContain("no exception");
		exception.Message.ShouldContain("ArgumentException");
	}

	#endregion ShouldNotThrow Tests

	#region Integration Tests (End-to-End Scenarios)

	[Fact]
	public void Support_Complete_GivenWhenThen_Workflow()
	{
		// Arrange & Act & Assert - Complete workflow demonstrating BDD-style testing
		_ = new AggregateTestFixture<TestAggregate>()
			// Given: Aggregate was initialized and had some activity
			.Given(new AggregateInitialized("order-123", 1, "Order"))
			.Given(new CounterIncremented("order-123", 2))
			.Given(new CounterIncremented("order-123", 3))
			// When: User increments by 5
			.When(agg => agg.IncrementBy(5))
			// Then: Proper event raised and state updated
			.Then()
			.ShouldRaise<CounterIncrementedBy>(e => e.Amount == 5)
			.StateShould(agg => agg.Counter == 7, "2 previous + 5 new = 7")
			.StateShould(agg => agg.Name == "Order")
			.StateShould(agg => agg.IsInitialized);
	}

	[Fact]
	public void Support_Exception_Workflow()
	{
		// Arrange & Act & Assert - Exception scenario
		new AggregateTestFixture<TestAggregate>()
			.Given(new AggregateInitialized("order-123", 1, "Order"))
			.When(agg => agg.Initialize("Duplicate")) // Should throw - already initialized
			.ShouldThrow<InvalidOperationException>("already initialized");
	}

	[Fact]
	public void Support_NoOp_Workflow()
	{
		// Arrange & Act & Assert - No-op scenario
		_ = new AggregateTestFixture<TestAggregate>()
			.Given(
				new AggregateInitialized("order-123", 1, "Order"),
				new CounterIncremented("order-123", 2))
			.When(agg => agg.DoNothing())
			.Then()
			.ShouldRaiseNoEvents()
			.ShouldNotThrow()
			.StateShould(agg => agg.Counter == 1)
			.StateShould(agg => agg.IsInitialized);
	}

	#endregion Integration Tests (End-to-End Scenarios)
}
