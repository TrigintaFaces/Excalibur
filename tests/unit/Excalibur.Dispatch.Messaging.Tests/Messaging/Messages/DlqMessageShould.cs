// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messages;

namespace Excalibur.Dispatch.Tests.Messaging.Messages;

/// <summary>
/// Unit tests for <see cref="DlqMessage"/>.
/// </summary>
/// <remarks>
/// Tests the dead letter queue message data class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messages")]
[Trait("Priority", "0")]
public sealed class DlqMessageShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesWithDefaults()
	{
		// Arrange & Act
		var message = new DlqMessage();

		// Assert
		_ = message.ShouldNotBeNull();
		message.MessageId.ShouldBe(string.Empty);
		message.Body.ShouldBe(string.Empty);
		message.Reason.ShouldBe(string.Empty);
		message.ExceptionDetails.ShouldBeNull();
		message.ProcessingAttempts.ShouldBe(0);
		message.SourceQueue.ShouldBe(string.Empty);
		_ = message.Headers.ShouldNotBeNull();
		message.Headers.ShouldBeEmpty();
		_ = message.Metadata.ShouldNotBeNull();
		message.Metadata.ShouldBeEmpty();
		message.CanRetry.ShouldBeTrue();
		message.Priority.ShouldBeNull();
	}

	#endregion

	#region MessageId Property Tests

	[Fact]
	public void MessageId_CanBeSet()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.MessageId = "msg-12345";

		// Assert
		message.MessageId.ShouldBe("msg-12345");
	}

	[Fact]
	public void MessageId_CanBeGuid()
	{
		// Arrange
		var message = new DlqMessage();
		var guid = Guid.NewGuid().ToString();

		// Act
		message.MessageId = guid;

		// Assert
		message.MessageId.ShouldBe(guid);
	}

	#endregion

	#region Body Property Tests

	[Fact]
	public void Body_CanBeSet()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.Body = "{\"key\": \"value\"}";

		// Assert
		message.Body.ShouldBe("{\"key\": \"value\"}");
	}

	[Fact]
	public void Body_CanBeLargeText()
	{
		// Arrange
		var message = new DlqMessage();
		var largeBody = new string('x', 100000);

		// Act
		message.Body = largeBody;

		// Assert
		message.Body.Length.ShouldBe(100000);
	}

	#endregion

	#region Reason Property Tests

	[Fact]
	public void Reason_CanBeSet()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.Reason = "Processing timeout exceeded";

		// Assert
		message.Reason.ShouldBe("Processing timeout exceeded");
	}

	[Theory]
	[InlineData("Max retry count exceeded")]
	[InlineData("Handler threw exception")]
	[InlineData("Message expired")]
	[InlineData("Validation failed")]
	public void Reason_WithVariousReasons_Works(string reason)
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.Reason = reason;

		// Assert
		message.Reason.ShouldBe(reason);
	}

	#endregion

	#region ExceptionDetails Property Tests

	[Fact]
	public void ExceptionDetails_CanBeSet()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.ExceptionDetails = "System.InvalidOperationException: Operation failed\n at Handler.Process()";

		// Assert
		message.ExceptionDetails.ShouldBe("System.InvalidOperationException: Operation failed\n at Handler.Process()");
	}

	[Fact]
	public void ExceptionDetails_CanBeNull()
	{
		// Arrange
		var message = new DlqMessage();
		message.ExceptionDetails = "Some error";

		// Act
		message.ExceptionDetails = null;

		// Assert
		message.ExceptionDetails.ShouldBeNull();
	}

	#endregion

	#region ProcessingAttempts Property Tests

	[Fact]
	public void ProcessingAttempts_CanBeSet()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.ProcessingAttempts = 5;

		// Assert
		message.ProcessingAttempts.ShouldBe(5);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(10)]
	public void ProcessingAttempts_WithVariousCounts_Works(int attempts)
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.ProcessingAttempts = attempts;

		// Assert
		message.ProcessingAttempts.ShouldBe(attempts);
	}

	#endregion

	#region FirstReceivedAt Property Tests

	[Fact]
	public void FirstReceivedAt_CanBeSet()
	{
		// Arrange
		var message = new DlqMessage();
		var received = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);

		// Act
		message.FirstReceivedAt = received;

		// Assert
		message.FirstReceivedAt.ShouldBe(received);
	}

	#endregion

	#region MovedToDlqAt Property Tests

	[Fact]
	public void MovedToDlqAt_CanBeSet()
	{
		// Arrange
		var message = new DlqMessage();
		var movedAt = new DateTime(2025, 1, 15, 11, 0, 0, DateTimeKind.Utc);

		// Act
		message.MovedToDlqAt = movedAt;

		// Assert
		message.MovedToDlqAt.ShouldBe(movedAt);
	}

	#endregion

	#region SourceQueue Property Tests

	[Fact]
	public void SourceQueue_CanBeSet()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.SourceQueue = "orders-queue";

		// Assert
		message.SourceQueue.ShouldBe("orders-queue");
	}

	[Theory]
	[InlineData("orders")]
	[InlineData("payments-topic")]
	[InlineData("notifications.email")]
	public void SourceQueue_WithVariousNames_Works(string queueName)
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.SourceQueue = queueName;

		// Assert
		message.SourceQueue.ShouldBe(queueName);
	}

	#endregion

	#region Headers Property Tests

	[Fact]
	public void Headers_CanAddItems()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.Headers.Add("Content-Type", "application/json");
		message.Headers.Add("X-Correlation-Id", "abc-123");

		// Assert
		message.Headers.Count.ShouldBe(2);
		message.Headers["Content-Type"].ShouldBe("application/json");
		message.Headers["X-Correlation-Id"].ShouldBe("abc-123");
	}

	[Fact]
	public void Headers_CanBeInitializedWithValues()
	{
		// Arrange & Act
		var message = new DlqMessage
		{
			Headers =
			{
				["Header1"] = "Value1",
				["Header2"] = "Value2",
			},
		};

		// Assert
		message.Headers.Count.ShouldBe(2);
	}

	#endregion

	#region Metadata Property Tests

	[Fact]
	public void Metadata_CanAddItems()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.Metadata.Add("RetryCount", 3);
		message.Metadata.Add("LastError", "Timeout");

		// Assert
		message.Metadata.Count.ShouldBe(2);
		message.Metadata["RetryCount"].ShouldBe(3);
		message.Metadata["LastError"].ShouldBe("Timeout");
	}

	[Fact]
	public void Metadata_CanStoreVariousTypes()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.Metadata["IntValue"] = 42;
		message.Metadata["StringValue"] = "test";
		message.Metadata["BoolValue"] = true;
		message.Metadata["DateValue"] = DateTime.UtcNow;

		// Assert
		message.Metadata.Count.ShouldBe(4);
	}

	#endregion

	#region CanRetry Property Tests

	[Fact]
	public void CanRetry_DefaultsToTrue()
	{
		// Arrange & Act
		var message = new DlqMessage();

		// Assert
		message.CanRetry.ShouldBeTrue();
	}

	[Fact]
	public void CanRetry_CanBeSetToFalse()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.CanRetry = false;

		// Assert
		message.CanRetry.ShouldBeFalse();
	}

	#endregion

	#region Priority Property Tests

	[Fact]
	public void Priority_DefaultsToNull()
	{
		// Arrange & Act
		var message = new DlqMessage();

		// Assert
		message.Priority.ShouldBeNull();
	}

	[Fact]
	public void Priority_CanBeSet()
	{
		// Arrange
		var message = new DlqMessage();

		// Act
		message.Priority = 5;

		// Assert
		message.Priority.ShouldBe(5);
	}

	[Fact]
	public void Priority_CanBeCleared()
	{
		// Arrange
		var message = new DlqMessage();
		message.Priority = 10;

		// Act
		message.Priority = null;

		// Assert
		message.Priority.ShouldBeNull();
	}

	#endregion

	#region Full Object Tests

	[Fact]
	public void AllProperties_CanBeSetViaObjectInitializer()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var message = new DlqMessage
		{
			MessageId = "msg-001",
			Body = "{\"data\": \"test\"}",
			Reason = "Max retries exceeded",
			ExceptionDetails = "Error details",
			ProcessingAttempts = 3,
			FirstReceivedAt = now.AddMinutes(-30),
			MovedToDlqAt = now,
			SourceQueue = "orders",
			CanRetry = false,
			Priority = 1,
		};

		// Assert
		message.MessageId.ShouldBe("msg-001");
		message.Body.ShouldBe("{\"data\": \"test\"}");
		message.Reason.ShouldBe("Max retries exceeded");
		message.ExceptionDetails.ShouldBe("Error details");
		message.ProcessingAttempts.ShouldBe(3);
		message.SourceQueue.ShouldBe("orders");
		message.CanRetry.ShouldBeFalse();
		message.Priority.ShouldBe(1);
	}

	#endregion
}
