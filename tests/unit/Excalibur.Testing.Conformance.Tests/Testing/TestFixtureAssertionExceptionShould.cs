// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Testing;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing;

/// <summary>
/// Unit tests for <see cref="TestFixtureAssertionException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class TestFixtureAssertionExceptionShould
{
	[Fact]
	public void Create_With_Message()
	{
		// Arrange
		var message = "Expected event was not raised";

		// Act
		var exception = new TestFixtureAssertionException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Inherit_From_Exception()
	{
		// Arrange & Act
		var exception = new TestFixtureAssertionException("test");

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void Create_With_Message_And_InnerException()
	{
		// Arrange
		var message = "Assertion failed during aggregate test";
		var inner = new InvalidOperationException("Inner operation failed");

		// Act
		var exception = new TestFixtureAssertionException(message, inner);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(inner);
		exception.InnerException.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public void Preserve_InnerException_Details()
	{
		// Arrange
		var innerMessage = "Database connection failed";
		var inner = new TimeoutException(innerMessage);
		var outerMessage = "Test fixture assertion failed";

		// Act
		var exception = new TestFixtureAssertionException(outerMessage, inner);

		// Assert
		exception.InnerException.ShouldNotBeNull();
		exception.InnerException.Message.ShouldBe(innerMessage);
	}

	[Fact]
	public void Be_Throwable_And_Catchable()
	{
		// Arrange
		var message = "Test assertion failure";

		// Act & Assert
		var caughtException = Should.Throw<TestFixtureAssertionException>(() =>
		{
			throw new TestFixtureAssertionException(message);
		});

		caughtException.Message.ShouldBe(message);
	}

	[Fact]
	public void Be_Catchable_As_Exception()
	{
		// Arrange
		var message = "Test assertion failure";

		// Act & Assert
		var caughtException = Should.Throw<Exception>(() =>
		{
			throw new TestFixtureAssertionException(message);
		});

		caughtException.ShouldBeOfType<TestFixtureAssertionException>();
	}

	[Fact]
	public void Support_Empty_Message()
	{
		// Arrange & Act
		var exception = new TestFixtureAssertionException(string.Empty);

		// Assert
		exception.Message.ShouldBeEmpty();
	}

	[Fact]
	public void Support_Multiline_Message()
	{
		// Arrange
		var message = """
			Expected event OrderCreated was not raised.
			Actual events: [CounterIncremented, AggregateInitialized]
			Aggregate state: { Counter: 5, IsInitialized: true }
			""";

		// Act
		var exception = new TestFixtureAssertionException(message);

		// Assert
		exception.Message.ShouldContain("OrderCreated");
		exception.Message.ShouldContain("CounterIncremented");
		exception.Message.ShouldContain("AggregateInitialized");
	}

	[Fact]
	public void Support_Message_With_Special_Characters()
	{
		// Arrange
		var message = "Expected: <OrderCreated> but got: [None] @ position {0}";

		// Act
		var exception = new TestFixtureAssertionException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Have_Null_InnerException_When_Not_Provided()
	{
		// Arrange & Act
		var exception = new TestFixtureAssertionException("test");

		// Assert
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Support_Nested_InnerExceptions()
	{
		// Arrange
		var level3 = new ArgumentException("Level 3");
		var level2 = new InvalidOperationException("Level 2", level3);
		var level1 = new TestFixtureAssertionException("Level 1", level2);

		// Act & Assert
		level1.InnerException.ShouldNotBeNull();
		level1.InnerException.InnerException.ShouldNotBeNull();
		level1.InnerException.InnerException.Message.ShouldBe("Level 3");
	}

	[Fact]
	public void Include_Stack_Trace_When_Thrown()
	{
		// Arrange & Act
		TestFixtureAssertionException? caughtException = null;
		try
		{
			ThrowHelperMethod();
		}
		catch (TestFixtureAssertionException ex)
		{
			caughtException = ex;
		}

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.StackTrace.ShouldNotBeNullOrEmpty();
		caughtException.StackTrace.ShouldContain("ThrowHelperMethod");
	}

	[Fact]
	public void Be_Usable_In_Test_Framework_Assertions()
	{
		// This test demonstrates that TestFixtureAssertionException integrates
		// well with test frameworks like xUnit/Shouldly

		// Arrange
		var fixture = new AggregateTestFixture<TestAggregate>()
			.When(agg => agg.DoNothing())
			.Then();

		// Act & Assert
		var exception = Should.Throw<TestFixtureAssertionException>(() =>
			fixture.ShouldRaise<CounterIncremented>());

		exception.Message.ShouldContain("CounterIncremented");
		exception.Message.ShouldContain("was not raised");
	}

	private static void ThrowHelperMethod()
	{
		throw new TestFixtureAssertionException("Thrown from helper method");
	}
}
