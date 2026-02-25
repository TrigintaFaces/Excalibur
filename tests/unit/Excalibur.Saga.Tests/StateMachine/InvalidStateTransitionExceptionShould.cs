// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.StateMachine;

namespace Excalibur.Saga.Tests.StateMachine;

/// <summary>
/// Unit tests for <see cref="InvalidStateTransitionException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class InvalidStateTransitionExceptionShould
{
	#region Constructor Tests

	[Fact]
	public void CreateWithCurrentStateAndAttemptedTransition()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"Processing",
			"Completed",
			messageType: null);

		// Assert
		exception.CurrentState.ShouldBe("Processing");
		exception.AttemptedTransition.ShouldBe("Completed");
		exception.MessageType.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessageType()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"Created",
			"Running",
			typeof(string));

		// Assert
		exception.CurrentState.ShouldBe("Created");
		exception.AttemptedTransition.ShouldBe("Running");
		exception.MessageType.ShouldBe(typeof(string));
	}

	[Fact]
	public void CreateWithInnerException()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new InvalidStateTransitionException(
			"Pending",
			"Active",
			typeof(int),
			innerException);

		// Assert
		exception.CurrentState.ShouldBe("Pending");
		exception.AttemptedTransition.ShouldBe("Active");
		exception.InnerException.ShouldBe(innerException);
	}

	#endregion Constructor Tests

	#region Message Formatting Tests

	[Fact]
	public void FormatMessageWithoutMessageType()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"StateA",
			"StateB",
			messageType: null);

		// Assert
		exception.Message.ShouldContain("StateA");
		exception.Message.ShouldContain("StateB");
		exception.Message.ShouldContain("Invalid state transition from 'StateA' to 'StateB'");
		exception.Message.ShouldNotContain("triggered by message type");
	}

	[Fact]
	public void FormatMessageWithMessageType()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"Initial",
			"Processing",
			typeof(OrderCreatedEvent));

		// Assert
		exception.Message.ShouldContain("Initial");
		exception.Message.ShouldContain("Processing");
		exception.Message.ShouldContain("triggered by message type 'OrderCreatedEvent'");
	}

	[Fact]
	public void IncludeTargetStateNotDefinedMessage()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"Current",
			"Target",
			messageType: null);

		// Assert
		exception.Message.ShouldContain("The target state 'Target' is not defined or the transition is not allowed");
	}

	#endregion Message Formatting Tests

	#region Property Tests

	[Fact]
	public void ExposeCurrentStateProperty()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"MyCurrentState",
			"MyTargetState",
			messageType: null);

		// Assert
		exception.CurrentState.ShouldBe("MyCurrentState");
	}

	[Fact]
	public void ExposeAttemptedTransitionProperty()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"Source",
			"Destination",
			messageType: null);

		// Assert
		exception.AttemptedTransition.ShouldBe("Destination");
	}

	[Fact]
	public void ExposeMessageTypePropertyWhenProvided()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"A",
			"B",
			typeof(PaymentProcessedEvent));

		// Assert
		exception.MessageType.ShouldBe(typeof(PaymentProcessedEvent));
	}

	[Fact]
	public void ExposeNullMessageTypeWhenNotProvided()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"A",
			"B",
			messageType: null);

		// Assert
		exception.MessageType.ShouldBeNull();
	}

	#endregion Property Tests

	#region Inheritance Tests

	[Fact]
	public void InheritFromInvalidOperationException()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"State1",
			"State2",
			messageType: null);

		// Assert
		exception.ShouldBeAssignableTo<InvalidOperationException>();
	}

	[Fact]
	public void InheritFromException()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"State1",
			"State2",
			messageType: null);

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	#endregion Inheritance Tests

	#region Scenario Tests

	[Fact]
	public void RepresentUndefinedStateTransition()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"Running",
			"InvalidState",
			messageType: null);

		// Assert
		exception.Message.ShouldContain("Invalid state transition from 'Running' to 'InvalidState'");
	}

	[Fact]
	public void RepresentMessageTriggeredTransition()
	{
		// Act
		var exception = new InvalidStateTransitionException(
			"AwaitingPayment",
			"Shipped",
			typeof(ShipOrderCommand));

		// Assert
		exception.Message.ShouldContain("triggered by message type 'ShipOrderCommand'");
	}

	[Fact]
	public void RepresentViolatedConstraint()
	{
		// Arrange
		var innerException = new InvalidOperationException("Constraint violation: Cannot skip states");

		// Act
		var exception = new InvalidStateTransitionException(
			"Created",
			"Completed",
			typeof(CompleteOrderCommand),
			innerException);

		// Assert
		exception.InnerException.Message.ShouldContain("Cannot skip states");
	}

	[Fact]
	public void BeCatchableAsInvalidOperationException()
	{
		// Arrange
		var exception = new InvalidStateTransitionException(
			"A",
			"B",
			messageType: null);

		// Act & Assert
		Should.NotThrow(() =>
		{
			try
			{
				throw exception;
			}
			catch (InvalidOperationException caught)
			{
				caught.ShouldBeOfType<InvalidStateTransitionException>();
			}
		});
	}

	[Fact]
	public void PreserveInnerExceptionStackTrace()
	{
		// Arrange
		InvalidOperationException innerException;
		try
		{
			throw new InvalidOperationException("Original error");
		}
		catch (InvalidOperationException ex)
		{
			innerException = ex;
		}

		// Act
		var exception = new InvalidStateTransitionException(
			"State1",
			"State2",
			typeof(string),
			innerException);

		// Assert
		exception.InnerException.StackTrace.ShouldNotBeNull();
	}

	#endregion Scenario Tests

	#region Test Helper Types

	// ReSharper disable ClassNeverInstantiated.Local
	private sealed record OrderCreatedEvent;
	private sealed record PaymentProcessedEvent;
	private sealed record ShipOrderCommand;
	private sealed record CompleteOrderCommand;
	// ReSharper restore ClassNeverInstantiated.Local

	#endregion Test Helper Types
}
