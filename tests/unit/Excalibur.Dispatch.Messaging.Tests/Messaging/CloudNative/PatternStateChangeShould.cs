// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="PatternStateChange"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PatternStateChangeShould
{
	[Fact]
	public void HaveDefaultTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var stateChange = new PatternStateChange();
		var after = DateTimeOffset.UtcNow;

		// Assert
		stateChange.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		stateChange.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveNullPreviousStateByDefault()
	{
		// Arrange & Act
		var stateChange = new PatternStateChange();

		// Assert
		stateChange.PreviousState.ShouldBeNull();
	}

	[Fact]
	public void HaveNullNewStateByDefault()
	{
		// Arrange & Act
		var stateChange = new PatternStateChange();

		// Assert
		stateChange.NewState.ShouldBeNull();
	}

	[Fact]
	public void HaveEmptyReasonByDefault()
	{
		// Arrange & Act
		var stateChange = new PatternStateChange();

		// Assert
		stateChange.Reason.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyContextByDefault()
	{
		// Arrange & Act
		var stateChange = new PatternStateChange();

		// Assert
		stateChange.Context.ShouldNotBeNull();
		stateChange.Context.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingTimestamp()
	{
		// Arrange
		var stateChange = new PatternStateChange();
		var customTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		stateChange.Timestamp = customTime;

		// Assert
		stateChange.Timestamp.ShouldBe(customTime);
	}

	[Fact]
	public void AllowSettingPreviousState()
	{
		// Arrange
		var stateChange = new PatternStateChange();

		// Act
		stateChange.PreviousState = CircuitState.Closed;

		// Assert
		stateChange.PreviousState.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void AllowSettingNewState()
	{
		// Arrange
		var stateChange = new PatternStateChange();

		// Act
		stateChange.NewState = CircuitState.Open;

		// Assert
		stateChange.NewState.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public void AllowSettingReason()
	{
		// Arrange
		var stateChange = new PatternStateChange();

		// Act
		stateChange.Reason = "Circuit breaker tripped";

		// Assert
		stateChange.Reason.ShouldBe("Circuit breaker tripped");
	}

	[Fact]
	public void AllowAddingContextEntries()
	{
		// Arrange
		var stateChange = new PatternStateChange();

		// Act
		stateChange.Context["failureCount"] = 5;
		stateChange.Context["serviceName"] = "OrderService";

		// Assert
		stateChange.Context.Count.ShouldBe(2);
		stateChange.Context["failureCount"].ShouldBe(5);
		stateChange.Context["serviceName"].ShouldBe("OrderService");
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var stateChange = new PatternStateChange
		{
			Timestamp = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
			PreviousState = CircuitState.Closed,
			NewState = CircuitState.Open,
			Reason = "Failure threshold exceeded",
			Context = new Dictionary<string, object>
			{
				["threshold"] = 5,
				["actual"] = 10,
			},
		};

		// Assert
		stateChange.Timestamp.ShouldBe(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero));
		stateChange.PreviousState.ShouldBe(CircuitState.Closed);
		stateChange.NewState.ShouldBe(CircuitState.Open);
		stateChange.Reason.ShouldBe("Failure threshold exceeded");
		stateChange.Context.Count.ShouldBe(2);
	}

	[Fact]
	public void AllowAnyObjectTypeForStates()
	{
		// Arrange
		var stateChange = new PatternStateChange();

		// Act
		stateChange.PreviousState = "inactive";
		stateChange.NewState = 42;

		// Assert
		stateChange.PreviousState.ShouldBe("inactive");
		stateChange.NewState.ShouldBe(42);
	}

	[Fact]
	public void AllowComplexObjectsInContext()
	{
		// Arrange
		var stateChange = new PatternStateChange();
		var complexData = new { Name = "Test", Value = 123 };

		// Act
		stateChange.Context["data"] = complexData;

		// Assert
		stateChange.Context["data"].ShouldBe(complexData);
	}

	[Theory]
	[InlineData("")]
	[InlineData("Short")]
	[InlineData("A longer reason with more details about the state change")]
	public void AcceptVariousReasonLengths(string reason)
	{
		// Arrange
		var stateChange = new PatternStateChange();

		// Act
		stateChange.Reason = reason;

		// Assert
		stateChange.Reason.ShouldBe(reason);
	}
}
