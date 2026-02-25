// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="InboxMessage"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InboxMessageShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void DefaultConstructor_CreateInstanceWithDefaultValues()
	{
		// Arrange & Act
		var message = new InboxMessage
		{
			ExternalMessageId = "ext-1",
			MessageType = "TestType",
			MessageMetadata = "{}",
			MessageBody = "{}",
			ReceivedAt = DateTimeOffset.UtcNow
		};

		// Assert
		_ = message.ShouldNotBeNull();
		message.ExternalMessageId.ShouldBe("ext-1");
		message.Attempts.ShouldBe(0);
		message.DispatcherId.ShouldBeNull();
		message.DispatcherTimeout.ShouldBeNull();
		message.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public void RequiredPropertiesConstructor_SetAllRequiredProperties()
	{
		// Arrange
		var externalMessageId = "ext-123";
		var messageType = "OrderCreated";
		var messageMetadata = "{\"correlationId\": \"abc\"}";
		var messageBody = "{\"orderId\": 123}";
		var receivedAt = DateTimeOffset.UtcNow;

		// Act
		var message = new InboxMessage(externalMessageId, messageType, messageMetadata, messageBody, receivedAt);

		// Assert
		message.ExternalMessageId.ShouldBe(externalMessageId);
		message.MessageType.ShouldBe(messageType);
		message.MessageMetadata.ShouldBe(messageMetadata);
		message.MessageBody.ShouldBe(messageBody);
		message.ReceivedAt.ShouldBe(receivedAt);
		message.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public void ExpirationConstructor_SetExpiresAt()
	{
		// Arrange
		var externalMessageId = "ext-456";
		var messageType = "OrderShipped";
		var messageMetadata = "{}";
		var messageBody = "{}";
		var receivedAt = DateTimeOffset.UtcNow;
		var expiresAt = receivedAt.AddHours(24);

		// Act
		var message = new InboxMessage(externalMessageId, messageType, messageMetadata, messageBody, receivedAt, expiresAt);

		// Assert
		message.ExternalMessageId.ShouldBe(externalMessageId);
		message.ExpiresAt.ShouldBe(expiresAt);
	}

	[Fact]
	public void ExpirationConstructor_AllowNullExpiresAt()
	{
		// Arrange
		var receivedAt = DateTimeOffset.UtcNow;

		// Act
		var message = new InboxMessage("ext-1", "Type", "{}", "{}", receivedAt, null);

		// Assert
		message.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public void ProcessingTrackingConstructor_SetAllTrackingProperties()
	{
		// Arrange
		var externalMessageId = "ext-789";
		var messageType = "PaymentProcessed";
		var messageMetadata = "{}";
		var messageBody = "{}";
		var receivedAt = DateTimeOffset.UtcNow;
		var attempts = 3;
		var dispatcherId = "processor-1";
		var dispatcherTimeout = receivedAt.AddMinutes(5);

		// Act
		var message = new InboxMessage(
			externalMessageId, messageType, messageMetadata, messageBody,
			receivedAt, attempts, dispatcherId, dispatcherTimeout);

		// Assert
		message.ExternalMessageId.ShouldBe(externalMessageId);
		message.Attempts.ShouldBe(attempts);
		message.DispatcherId.ShouldBe(dispatcherId);
		message.DispatcherTimeout.ShouldBe(dispatcherTimeout);
	}

	[Fact]
	public void ProcessingTrackingConstructor_AllowNullProcessorFields()
	{
		// Arrange
		var receivedAt = DateTimeOffset.UtcNow;

		// Act
		var message = new InboxMessage("ext-1", "Type", "{}", "{}", receivedAt, 0, null, null);

		// Assert
		message.DispatcherId.ShouldBeNull();
		message.DispatcherTimeout.ShouldBeNull();
	}

	#endregion

	#region Property Mutation Tests

	[Fact]
	public void Attempts_CanBeModified()
	{
		// Arrange
		var message = new InboxMessage("ext-1", "Type", "{}", "{}", DateTimeOffset.UtcNow);

		// Act
		message.Attempts = 5;

		// Assert
		message.Attempts.ShouldBe(5);
	}

	[Fact]
	public void DispatcherId_CanBeModified()
	{
		// Arrange
		var message = new InboxMessage("ext-1", "Type", "{}", "{}", DateTimeOffset.UtcNow);

		// Act
		message.DispatcherId = "new-processor";

		// Assert
		message.DispatcherId.ShouldBe("new-processor");
	}

	[Fact]
	public void DispatcherTimeout_CanBeModified()
	{
		// Arrange
		var message = new InboxMessage("ext-1", "Type", "{}", "{}", DateTimeOffset.UtcNow);
		var timeout = DateTimeOffset.UtcNow.AddMinutes(10);

		// Act
		message.DispatcherTimeout = timeout;

		// Assert
		message.DispatcherTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void ExpiresAt_CanBeModified()
	{
		// Arrange
		var message = new InboxMessage("ext-1", "Type", "{}", "{}", DateTimeOffset.UtcNow);
		var expiresAt = DateTimeOffset.UtcNow.AddDays(1);

		// Act
		message.ExpiresAt = expiresAt;

		// Assert
		message.ExpiresAt.ShouldBe(expiresAt);
	}

	#endregion

	#region Record Equality Tests

	[Fact]
	public void RecordEquality_TwoMessagesWithSameValues_AreEqual()
	{
		// Arrange
		var receivedAt = DateTimeOffset.UtcNow;
		var message1 = new InboxMessage("ext-1", "Type", "{}", "{}", receivedAt);
		var message2 = new InboxMessage("ext-1", "Type", "{}", "{}", receivedAt);

		// Act & Assert
		message1.ShouldBe(message2);
	}

	[Fact]
	public void RecordEquality_TwoMessagesWithDifferentIds_AreNotEqual()
	{
		// Arrange
		var receivedAt = DateTimeOffset.UtcNow;
		var message1 = new InboxMessage("ext-1", "Type", "{}", "{}", receivedAt);
		var message2 = new InboxMessage("ext-2", "Type", "{}", "{}", receivedAt);

		// Act & Assert
		message1.ShouldNotBe(message2);
	}

	[Fact]
	public void RecordEquality_MessageWithDifferentMutableProperties_AreNotEqual()
	{
		// Arrange
		var receivedAt = DateTimeOffset.UtcNow;
		var message1 = new InboxMessage("ext-1", "Type", "{}", "{}", receivedAt) { Attempts = 1 };
		var message2 = new InboxMessage("ext-1", "Type", "{}", "{}", receivedAt) { Attempts = 2 };

		// Act & Assert
		message1.ShouldNotBe(message2);
	}

	[Fact]
	public void WithExpression_CreatesCopyWithModifiedProperty()
	{
		// Arrange
		var original = new InboxMessage("ext-1", "Type", "{}", "{}", DateTimeOffset.UtcNow);

		// Act
		var modified = original with { ExternalMessageId = "ext-2" };

		// Assert
		modified.ExternalMessageId.ShouldBe("ext-2");
		modified.MessageType.ShouldBe(original.MessageType);
		original.ExternalMessageId.ShouldBe("ext-1");
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void Message_WithEmptyStrings_IsValid()
	{
		// Arrange & Act
		var message = new InboxMessage
		{
			ExternalMessageId = "",
			MessageType = "",
			MessageMetadata = "",
			MessageBody = "",
			ReceivedAt = DateTimeOffset.UtcNow
		};

		// Assert
		message.ExternalMessageId.ShouldBe("");
		message.MessageType.ShouldBe("");
	}

	[Fact]
	public void Message_WithLargePayload_IsValid()
	{
		// Arrange
		var largeBody = new string('x', 100000);

		// Act
		var message = new InboxMessage("ext-1", "Type", "{}", largeBody, DateTimeOffset.UtcNow);

		// Assert
		message.MessageBody.Length.ShouldBe(100000);
	}

	[Fact]
	public void Message_WithMaxAttempts_IsValid()
	{
		// Arrange & Act
		var message = new InboxMessage("ext-1", "Type", "{}", "{}", DateTimeOffset.UtcNow)
		{
			Attempts = int.MaxValue
		};

		// Assert
		message.Attempts.ShouldBe(int.MaxValue);
	}

	#endregion
}
