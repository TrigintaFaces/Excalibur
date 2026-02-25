// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="MessageTimeoutException"/>.
/// </summary>
/// <remarks>
/// Tests the exception thrown when message processing times out.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class MessageTimeoutExceptionShould
{
	#region Constructor Tests - Default

	[Fact]
	public void Constructor_Default_CreatesInstance()
	{
		// Arrange & Act
		var exception = new MessageTimeoutException();

		// Assert
		_ = exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeEmpty();
	}

	[Fact]
	public void Constructor_Default_HasNullMessageId()
	{
		// Arrange & Act
		var exception = new MessageTimeoutException();

		// Assert
		exception.MessageId.ShouldBeNull();
	}

	[Fact]
	public void Constructor_Default_HasNullMessageType()
	{
		// Arrange & Act
		var exception = new MessageTimeoutException();

		// Assert
		exception.MessageType.ShouldBeNull();
	}

	[Fact]
	public void Constructor_Default_HasDefaultTimeoutDuration()
	{
		// Arrange & Act
		var exception = new MessageTimeoutException();

		// Assert
		exception.TimeoutDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void Constructor_Default_HasDefaultElapsedTime()
	{
		// Arrange & Act
		var exception = new MessageTimeoutException();

		// Assert
		exception.ElapsedTime.ShouldBe(TimeSpan.Zero);
	}

	#endregion

	#region Constructor Tests - With Message

	[Fact]
	public void Constructor_WithMessage_SetsMessage()
	{
		// Arrange
		const string message = "Operation timed out after 30 seconds";

		// Act
		var exception = new MessageTimeoutException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Constructor_WithEmptyMessage_AcceptsEmptyString()
	{
		// Arrange & Act
		var exception = new MessageTimeoutException(string.Empty);

		// Assert
		exception.Message.ShouldBe(string.Empty);
	}

	#endregion

	#region Constructor Tests - With Message and InnerException

	[Fact]
	public void Constructor_WithMessageAndInnerException_SetsBoth()
	{
		// Arrange
		const string message = "Message processing timed out";
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new MessageTimeoutException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void Constructor_WithNullInnerException_AcceptsNull()
	{
		// Arrange & Act
		var exception = new MessageTimeoutException("Message", null!);

		// Assert
		exception.InnerException.ShouldBeNull();
	}

	#endregion

	#region MessageId Property Tests

	[Fact]
	public void MessageId_CanBeSet()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");

		// Act
		exception.MessageId = "msg-12345";

		// Assert
		exception.MessageId.ShouldBe("msg-12345");
	}

	[Fact]
	public void MessageId_CanBeSetToNull()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout") { MessageId = "test" };

		// Act
		exception.MessageId = null;

		// Assert
		exception.MessageId.ShouldBeNull();
	}

	[Fact]
	public void MessageId_CanBeSetToEmptyString()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");

		// Act
		exception.MessageId = string.Empty;

		// Assert
		exception.MessageId.ShouldBe(string.Empty);
	}

	#endregion

	#region MessageType Property Tests

	[Fact]
	public void MessageType_CanBeSet()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");

		// Act
		exception.MessageType = "OrderCreatedEvent";

		// Assert
		exception.MessageType.ShouldBe("OrderCreatedEvent");
	}

	[Fact]
	public void MessageType_CanBeSetToNull()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout") { MessageType = "test" };

		// Act
		exception.MessageType = null;

		// Assert
		exception.MessageType.ShouldBeNull();
	}

	[Fact]
	public void MessageType_CanBeSetToFullTypeName()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");

		// Act
		exception.MessageType = "MyNamespace.Events.OrderCreatedEvent, MyAssembly";

		// Assert
		exception.MessageType.ShouldBe("MyNamespace.Events.OrderCreatedEvent, MyAssembly");
	}

	#endregion

	#region TimeoutDuration Property Tests

	[Fact]
	public void TimeoutDuration_CanBeSet()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");

		// Act
		exception.TimeoutDuration = TimeSpan.FromSeconds(30);

		// Assert
		exception.TimeoutDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void TimeoutDuration_CanBeSetToZero()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout") { TimeoutDuration = TimeSpan.FromSeconds(10) };

		// Act
		exception.TimeoutDuration = TimeSpan.Zero;

		// Assert
		exception.TimeoutDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void TimeoutDuration_CanBeSetToLargeValue()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");

		// Act
		exception.TimeoutDuration = TimeSpan.FromHours(24);

		// Assert
		exception.TimeoutDuration.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void TimeoutDuration_CanBeSetToMilliseconds()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");

		// Act
		exception.TimeoutDuration = TimeSpan.FromMilliseconds(500);

		// Assert
		exception.TimeoutDuration.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	#endregion

	#region ElapsedTime Property Tests

	[Fact]
	public void ElapsedTime_CanBeSet()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");

		// Act
		exception.ElapsedTime = TimeSpan.FromSeconds(35);

		// Assert
		exception.ElapsedTime.ShouldBe(TimeSpan.FromSeconds(35));
	}

	[Fact]
	public void ElapsedTime_CanBeSetToZero()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout") { ElapsedTime = TimeSpan.FromSeconds(10) };

		// Act
		exception.ElapsedTime = TimeSpan.Zero;

		// Assert
		exception.ElapsedTime.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ElapsedTime_CanExceedTimeoutDuration()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout")
		{
			TimeoutDuration = TimeSpan.FromSeconds(30),
		};

		// Act
		exception.ElapsedTime = TimeSpan.FromSeconds(45);

		// Assert
		exception.ElapsedTime.ShouldBe(TimeSpan.FromSeconds(45));
		(exception.ElapsedTime > exception.TimeoutDuration).ShouldBeTrue();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void CanBeInitializedWithObjectInitializer()
	{
		// Arrange & Act
		var exception = new MessageTimeoutException("Processing timed out")
		{
			MessageId = "msg-001",
			MessageType = "TestMessage",
			TimeoutDuration = TimeSpan.FromSeconds(30),
			ElapsedTime = TimeSpan.FromSeconds(35),
		};

		// Assert
		exception.Message.ShouldBe("Processing timed out");
		exception.MessageId.ShouldBe("msg-001");
		exception.MessageType.ShouldBe("TestMessage");
		exception.TimeoutDuration.ShouldBe(TimeSpan.FromSeconds(30));
		exception.ElapsedTime.ShouldBe(TimeSpan.FromSeconds(35));
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromException()
	{
		// Arrange & Act
		var exception = new MessageTimeoutException("Timeout");

		// Assert
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void CanBeCaughtAsException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<Exception>(() => throw new MessageTimeoutException("Test timeout"));
	}

	[Fact]
	public void CanBeCaughtAsMessageTimeoutException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<MessageTimeoutException>(() => throw new MessageTimeoutException("Test timeout"));
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanDescribeTimeoutScenario()
	{
		// Arrange
		var exception = new MessageTimeoutException("Message processing exceeded configured timeout")
		{
			MessageId = "order-12345",
			MessageType = "OrderProcessedEvent",
			TimeoutDuration = TimeSpan.FromSeconds(30),
			ElapsedTime = TimeSpan.FromSeconds(45.5),
		};

		// Act
		var timeOverrun = exception.ElapsedTime - exception.TimeoutDuration;

		// Assert
		timeOverrun.ShouldBe(TimeSpan.FromSeconds(15.5));
	}

	[Fact]
	public void PreservesStackTraceWhenRethrown()
	{
		// Arrange
		MessageTimeoutException? caughtException = null;

		// Act
		try
		{
			try
			{
				throw new MessageTimeoutException("Original timeout");
			}
			catch (MessageTimeoutException ex)
			{
				ex.MessageId = "enriched-id";
				throw;
			}
		}
		catch (MessageTimeoutException ex)
		{
			caughtException = ex;
		}

		// Assert
		_ = caughtException.ShouldNotBeNull();
		caughtException.MessageId.ShouldBe("enriched-id");
		_ = caughtException.StackTrace.ShouldNotBeNull();
	}

	#endregion
}
