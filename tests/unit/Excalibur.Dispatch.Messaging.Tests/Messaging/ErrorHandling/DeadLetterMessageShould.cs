// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

/// <summary>
/// Unit tests for <see cref="DeadLetterMessage"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
[Trait("Priority", "0")]
public sealed class DeadLetterMessageShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Id_IsNotEmpty()
	{
		// Arrange & Act
		var message = CreateMinimalDeadLetterMessage();

		// Assert
		message.Id.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Default_ProcessingAttempts_IsZero()
	{
		// Arrange & Act
		var message = CreateMinimalDeadLetterMessage();

		// Assert
		message.ProcessingAttempts.ShouldBe(0);
	}

	[Fact]
	public void Default_IsReplayed_IsFalse()
	{
		// Arrange & Act
		var message = CreateMinimalDeadLetterMessage();

		// Assert
		message.IsReplayed.ShouldBeFalse();
	}

	[Fact]
	public void Default_Properties_IsEmptyDictionary()
	{
		// Arrange & Act
		var message = CreateMinimalDeadLetterMessage();

		// Assert
		_ = message.Properties.ShouldNotBeNull();
		message.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void Default_OptionalProperties_AreNull()
	{
		// Arrange & Act
		var message = CreateMinimalDeadLetterMessage();

		// Assert
		message.ExceptionDetails.ShouldBeNull();
		message.FirstAttemptAt.ShouldBeNull();
		message.LastAttemptAt.ShouldBeNull();
		message.ReplayedAt.ShouldBeNull();
		message.SourceSystem.ShouldBeNull();
		message.CorrelationId.ShouldBeNull();
	}

	#endregion

	#region Required Property Tests

	[Fact]
	public void RequiredProperties_CanBeSet()
	{
		// Act
		var message = new DeadLetterMessage
		{
			MessageId = "msg-123",
			MessageType = "OrderCreated",
			MessageBody = "{\"orderId\": 1}",
			MessageMetadata = "{\"tenant\": \"acme\"}",
			Reason = "Max retries exceeded",
		};

		// Assert
		message.MessageId.ShouldBe("msg-123");
		message.MessageType.ShouldBe("OrderCreated");
		message.MessageBody.ShouldBe("{\"orderId\": 1}");
		message.MessageMetadata.ShouldBe("{\"tenant\": \"acme\"}");
		message.Reason.ShouldBe("Max retries exceeded");
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Id_CanBeSet()
	{
		// Arrange
		var message = CreateMinimalDeadLetterMessage();

		// Act
		message.Id = "custom-id";

		// Assert
		message.Id.ShouldBe("custom-id");
	}

	[Fact]
	public void ProcessingAttempts_CanBeSet()
	{
		// Arrange
		var message = CreateMinimalDeadLetterMessage();

		// Act
		message.ProcessingAttempts = 5;

		// Assert
		message.ProcessingAttempts.ShouldBe(5);
	}

	[Fact]
	public void ExceptionDetails_CanBeSet()
	{
		// Arrange
		var message = CreateMinimalDeadLetterMessage();

		// Act
		message.ExceptionDetails = "System.InvalidOperationException: Something went wrong";

		// Assert
		message.ExceptionDetails.ShouldBe("System.InvalidOperationException: Something went wrong");
	}

	[Fact]
	public void IsReplayed_CanBeSet()
	{
		// Arrange
		var message = CreateMinimalDeadLetterMessage();

		// Act
		message.IsReplayed = true;
		message.ReplayedAt = DateTimeOffset.UtcNow;

		// Assert
		message.IsReplayed.ShouldBeTrue();
		_ = message.ReplayedAt.ShouldNotBeNull();
	}

	[Fact]
	public void Properties_CanAddItems()
	{
		// Arrange
		var message = CreateMinimalDeadLetterMessage();

		// Act
		message.Properties["custom-key"] = "custom-value";

		// Assert
		message.Properties.Count.ShouldBe(1);
		message.Properties["custom-key"].ShouldBe("custom-value");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Message_ForFailedOrder_HasCorrectDetails()
	{
		// Act
		var message = new DeadLetterMessage
		{
			MessageId = "order-123",
			MessageType = "OrderCreated",
			MessageBody = "{\"orderId\": 123, \"amount\": 99.99}",
			MessageMetadata = "{\"tenantId\": \"tenant-1\"}",
			Reason = "Payment validation failed",
			ProcessingAttempts = 3,
			ExceptionDetails = "Payment gateway timeout",
			FirstAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-5),
			LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1),
			CorrelationId = "workflow-456",
		};

		// Assert
		message.ProcessingAttempts.ShouldBe(3);
		message.Reason.ShouldContain("Payment");
		_ = message.CorrelationId.ShouldNotBeNull();
	}

	[Fact]
	public void Message_ForReplay_TracksReplayTime()
	{
		// Arrange
		var message = CreateMinimalDeadLetterMessage();
		message.ProcessingAttempts = 5;

		// Act
		message.IsReplayed = true;
		message.ReplayedAt = DateTimeOffset.UtcNow;

		// Assert
		message.IsReplayed.ShouldBeTrue();
		_ = message.ReplayedAt.ShouldNotBeNull();
		message.ReplayedAt.Value.ShouldBeGreaterThanOrEqualTo(message.MovedToDeadLetterAt);
	}

	[Fact]
	public void Message_WithSourceSystem_TracksOrigin()
	{
		// Act
		var message = new DeadLetterMessage
		{
			MessageId = "msg-123",
			MessageType = "IntegrationEvent",
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "Handler not found",
			SourceSystem = "ExternalAPI",
		};

		// Assert
		message.SourceSystem.ShouldBe("ExternalAPI");
	}

	#endregion

	#region Helper Methods

	private static DeadLetterMessage CreateMinimalDeadLetterMessage() =>
		new()
		{
			MessageId = "test-id",
			MessageType = "TestMessage",
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "Test reason",
		};

	#endregion
}
