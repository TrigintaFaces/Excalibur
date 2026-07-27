// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="OutboxMessage"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxMessageShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void DefaultConstructor_CreateInstanceWithDefaultValues()
	{
		// Arrange & Act
		var message = new OutboxMessage
		{
			MessageId = "msg-1",
			MessageType = "TestType",
			MessageMetadata = "{}",
			MessageBody = Encoding.UTF8.GetBytes("{}"),
			CreatedAt = DateTimeOffset.UtcNow
		};

		// Assert
		_ = message.ShouldNotBeNull();
		message.MessageId.ShouldBe("msg-1");
		message.Attempts.ShouldBe(0);
		message.DispatcherId.ShouldBeNull();
		message.DispatcherTimeout.ShouldBeNull();
		message.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public void EqualsAndGetHashCode_WithNullMessageBody_DoNotThrow()
	{
		// MessageBody is declared non-null (required), but Dapper hydration can materialize a null payload
		// column. Equals/GetHashCode must be null-safe (05f5q4) rather than NRE on the byte-array compare.
		var withNullBody = new OutboxMessage
		{
			MessageId = "m", MessageType = "T", MessageMetadata = "{}",
			MessageBody = null!, CreatedAt = DateTimeOffset.UnixEpoch,
		};
		var withBody = withNullBody with { MessageBody = [1, 2, 3] };
		var alsoNullBody = withNullBody with { };

		// GetHashCode never throws with a null body.
		_ = Should.NotThrow(() => withNullBody.GetHashCode());

		// Equals never throws in either direction and yields the correct value.
		withNullBody.Equals(withBody).ShouldBeFalse();   // null vs non-null body
		withBody.Equals(withNullBody).ShouldBeFalse();   // non-null vs null body
		withNullBody.Equals(alsoNullBody).ShouldBeTrue(); // null vs null body -> equal
	}

	[Fact]
	public void RequiredPropertiesConstructor_SetAllRequiredProperties()
	{
		// Arrange
		var messageId = "msg-123";
		var messageType = "OrderCreated";
		var messageMetadata = "{\"correlationId\": \"abc\"}";
		var messageBody = Encoding.UTF8.GetBytes("{\"orderId\": 123}");
		var createdAt = DateTimeOffset.UtcNow;

		// Act
		var message = new OutboxMessage(messageId, messageType, messageMetadata, messageBody, createdAt);

		// Assert
		message.MessageId.ShouldBe(messageId);
		message.MessageType.ShouldBe(messageType);
		message.MessageMetadata.ShouldBe(messageMetadata);
		message.MessageBody.ShouldBe(messageBody);
		message.CreatedAt.ShouldBe(createdAt);
		message.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public void ExpirationConstructor_SetExpiresAt()
	{
		// Arrange
		var messageId = "msg-456";
		var messageType = "OrderShipped";
		var messageMetadata = "{}";
		var messageBody = Encoding.UTF8.GetBytes("{}");
		var createdAt = DateTimeOffset.UtcNow;
		var expiresAt = createdAt.AddHours(24);

		// Act
		var message = new OutboxMessage(messageId, messageType, messageMetadata, messageBody, createdAt, expiresAt);

		// Assert
		message.MessageId.ShouldBe(messageId);
		message.ExpiresAt.ShouldBe(expiresAt);
	}

	[Fact]
	public void ExpirationConstructor_AllowNullExpiresAt()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow;

		// Act
		var message = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), createdAt, null);

		// Assert
		message.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public void DispatchTrackingConstructor_SetAllTrackingProperties()
	{
		// Arrange
		var messageId = "msg-789";
		var messageType = "PaymentProcessed";
		var messageMetadata = "{}";
		var messageBody = Encoding.UTF8.GetBytes("{}");
		var createdAt = DateTimeOffset.UtcNow;
		var attempts = 3;
		var dispatcherId = "worker-1";
		var dispatcherTimeout = createdAt.AddMinutes(5);

		// Act
		var message = new OutboxMessage(
			messageId, messageType, messageMetadata, messageBody,
			createdAt, attempts, dispatcherId, dispatcherTimeout);

		// Assert
		message.MessageId.ShouldBe(messageId);
		message.Attempts.ShouldBe(attempts);
		message.DispatcherId.ShouldBe(dispatcherId);
		message.DispatcherTimeout.ShouldBe(dispatcherTimeout);
	}

	[Fact]
	public void DispatchTrackingConstructor_AllowNullDispatcherFields()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow;

		// Act
		var message = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), createdAt, 0, null, null);

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
		var message = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), DateTimeOffset.UtcNow);

		// Act
		message.Attempts = 5;

		// Assert
		message.Attempts.ShouldBe(5);
	}

	[Fact]
	public void DispatcherId_CanBeModified()
	{
		// Arrange
		var message = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), DateTimeOffset.UtcNow);

		// Act
		message.DispatcherId = "new-worker";

		// Assert
		message.DispatcherId.ShouldBe("new-worker");
	}

	[Fact]
	public void DispatcherTimeout_CanBeModified()
	{
		// Arrange
		var message = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), DateTimeOffset.UtcNow);
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
		var message = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), DateTimeOffset.UtcNow);
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
		var createdAt = DateTimeOffset.UtcNow;
		var message1 = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), createdAt);
		var message2 = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), createdAt);

		// Act & Assert
		message1.ShouldBe(message2);
	}

	[Fact]
	public void RecordEquality_TwoMessagesWithDifferentIds_AreNotEqual()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow;
		var message1 = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), createdAt);
		var message2 = new OutboxMessage("msg-2", "Type", "{}", Encoding.UTF8.GetBytes("{}"), createdAt);

		// Act & Assert
		message1.ShouldNotBe(message2);
	}

	[Fact]
	public void RecordEquality_MessageWithDifferentMutableProperties_AreNotEqual()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow;
		var message1 = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), createdAt) { Attempts = 1 };
		var message2 = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), createdAt) { Attempts = 2 };

		// Act & Assert - mutable properties affect equality in records
		message1.ShouldNotBe(message2);
	}

	[Fact]
	public void WithExpression_CreatesCopyWithModifiedProperty()
	{
		// Arrange
		var original = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), DateTimeOffset.UtcNow);

		// Act
		var modified = original with { MessageId = "msg-2" };

		// Assert
		modified.MessageId.ShouldBe("msg-2");
		modified.MessageType.ShouldBe(original.MessageType);
		original.MessageId.ShouldBe("msg-1"); // Original unchanged
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void Message_WithEmptyStrings_IsValid()
	{
		// Arrange & Act
		var message = new OutboxMessage
		{
			MessageId = "",
			MessageType = "",
			MessageMetadata = "",
			MessageBody = Array.Empty<byte>(),
			CreatedAt = DateTimeOffset.UtcNow
		};

		// Assert
		message.MessageId.ShouldBe("");
		message.MessageType.ShouldBe("");
		message.MessageMetadata.ShouldBe("");
		message.MessageBody.ShouldBe(Array.Empty<byte>());
	}

	[Fact]
	public void Message_WithLargePayload_IsValid()
	{
		// Arrange
		var largeBody = Encoding.UTF8.GetBytes(new string('x', 100000));

		// Act
		var message = new OutboxMessage("msg-1", "Type", "{}", largeBody, DateTimeOffset.UtcNow);

		// Assert
		message.MessageBody.Length.ShouldBe(100000);
	}

	[Fact]
	public void Message_WithMaxAttempts_IsValid()
	{
		// Arrange & Act
		var message = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), DateTimeOffset.UtcNow)
		{
			Attempts = int.MaxValue
		};

		// Assert
		message.Attempts.ShouldBe(int.MaxValue);
	}

	[Fact]
	public void Message_WithMinDateTimeOffset_IsValid()
	{
		// Arrange & Act
		var message = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), DateTimeOffset.MinValue);

		// Assert
		message.CreatedAt.ShouldBe(DateTimeOffset.MinValue);
	}

	[Fact]
	public void Message_WithMaxDateTimeOffset_IsValid()
	{
		// Arrange & Act
		var message = new OutboxMessage("msg-1", "Type", "{}", Encoding.UTF8.GetBytes("{}"), DateTimeOffset.MaxValue);

		// Assert
		message.CreatedAt.ShouldBe(DateTimeOffset.MaxValue);
	}

	#endregion
}
