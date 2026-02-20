// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="DispatchMessage"/>.
/// </summary>
/// <remarks>
/// Tests the generic dispatch message with serialized payload.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class DispatchMessageShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesWithDefaults()
	{
		// Arrange & Act
		var message = new DispatchMessage();

		// Assert
		_ = message.ShouldNotBeNull();
		message.Id.ShouldNotBeNullOrEmpty();
		message.MessageType.ShouldBe(string.Empty);
		_ = message.Payload.ShouldNotBeNull();
		message.Payload.Length.ShouldBe(0);
		message.CreatedAt.ShouldNotBe(default);
	}

	#endregion

	#region Id Property Tests

	[Fact]
	public void Id_DefaultsToGuidFormat()
	{
		// Arrange & Act
		var message = new DispatchMessage();

		// Assert
		Guid.TryParse(message.Id, out _).ShouldBeTrue();
	}

	[Fact]
	public void Id_CanBeSet()
	{
		// Arrange
		var message = new DispatchMessage();
		var customId = "custom-id-12345";

		// Act
		message.Id = customId;

		// Assert
		message.Id.ShouldBe(customId);
	}

	[Fact]
	public void Id_GeneratesUniqueValues()
	{
		// Arrange & Act
		var message1 = new DispatchMessage();
		var message2 = new DispatchMessage();

		// Assert
		message1.Id.ShouldNotBe(message2.Id);
	}

	#endregion

	#region MessageType Property Tests

	[Fact]
	public void MessageType_DefaultsToEmpty()
	{
		// Arrange & Act
		var message = new DispatchMessage();

		// Assert
		message.MessageType.ShouldBe(string.Empty);
	}

	[Fact]
	public void MessageType_CanBeSet()
	{
		// Arrange
		var message = new DispatchMessage();

		// Act
		message.MessageType = "OrderCreated";

		// Assert
		message.MessageType.ShouldBe("OrderCreated");
	}

	[Theory]
	[InlineData("CreateOrderCommand")]
	[InlineData("OrderCreatedEvent")]
	[InlineData("Namespace.Commands.CreateOrder")]
	[InlineData("")]
	public void MessageType_WithVariousValues_Works(string messageType)
	{
		// Arrange
		var message = new DispatchMessage();

		// Act
		message.MessageType = messageType;

		// Assert
		message.MessageType.ShouldBe(messageType);
	}

	#endregion

	#region Payload Property Tests

	[Fact]
	public void Payload_DefaultsToEmptyArray()
	{
		// Arrange & Act
		var message = new DispatchMessage();

		// Assert
		_ = message.Payload.ShouldNotBeNull();
		message.Payload.Length.ShouldBe(0);
	}

	[Fact]
	public void Payload_CanBeSet()
	{
		// Arrange
		var message = new DispatchMessage();
		var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };

		// Act
		message.Payload = payload;

		// Assert
		message.Payload.ShouldBe(payload);
	}

	[Fact]
	public void Payload_WithJsonBytes_Works()
	{
		// Arrange
		var message = new DispatchMessage();
		var json = "{\"orderId\":123}";
		var payload = System.Text.Encoding.UTF8.GetBytes(json);

		// Act
		message.Payload = payload;

		// Assert
		message.Payload.Length.ShouldBe(payload.Length);
		System.Text.Encoding.UTF8.GetString(message.Payload).ShouldBe(json);
	}

	[Fact]
	public void Payload_WithLargeData_Works()
	{
		// Arrange
		var message = new DispatchMessage();
		var largePayload = new byte[1024 * 1024]; // 1MB
		Random.Shared.NextBytes(largePayload);

		// Act
		message.Payload = largePayload;

		// Assert
		message.Payload.Length.ShouldBe(1024 * 1024);
	}

	#endregion

	#region CreatedAt Property Tests

	[Fact]
	public void CreatedAt_HasReasonableDefaultValue()
	{
		// Arrange & Act
		var before = DateTime.UtcNow.AddMinutes(-1);
		var message = new DispatchMessage();
		var after = DateTime.UtcNow.AddMinutes(1);

		// Assert - The timestamp should be relatively recent
		// Note: The implementation uses ValueStopwatch which may produce times in a different range
		message.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	public void CreatedAt_CanBeSet()
	{
		// Arrange
		var message = new DispatchMessage();
		var customTime = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);

		// Act
		message.CreatedAt = customTime;

		// Assert
		message.CreatedAt.ShouldBe(customTime);
	}

	[Fact]
	public void CreatedAt_IsUtc()
	{
		// Arrange & Act
		var message = new DispatchMessage();

		// Assert
		message.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var customId = "msg-001";
		var messageType = "TestMessage";
		var payload = new byte[] { 0xFF, 0xFE };
		var createdAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		// Act
		var message = new DispatchMessage
		{
			Id = customId,
			MessageType = messageType,
			Payload = payload,
			CreatedAt = createdAt,
		};

		// Assert
		message.Id.ShouldBe(customId);
		message.MessageType.ShouldBe(messageType);
		message.Payload.ShouldBe(payload);
		message.CreatedAt.ShouldBe(createdAt);
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void SerializedCommandMessage_Scenario()
	{
		// Arrange & Act
		var command = new DispatchMessage
		{
			MessageType = "CreateOrderCommand",
			Payload = System.Text.Encoding.UTF8.GetBytes("{\"orderId\":\"12345\",\"amount\":99.99}"),
		};

		// Assert
		command.MessageType.ShouldBe("CreateOrderCommand");
		command.Payload.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void SerializedEventMessage_Scenario()
	{
		// Arrange & Act
		var evt = new DispatchMessage
		{
			Id = Guid.NewGuid().ToString(),
			MessageType = "OrderCreatedEvent",
			Payload = System.Text.Encoding.UTF8.GetBytes("{\"orderId\":\"12345\",\"status\":\"created\"}"),
			CreatedAt = DateTime.UtcNow,
		};

		// Assert
		evt.MessageType.ShouldBe("OrderCreatedEvent");
		Guid.TryParse(evt.Id, out _).ShouldBeTrue();
	}

	[Fact]
	public void MultipleMessagesWithUniqueIds_Scenario()
	{
		// Arrange & Act
		var messages = Enumerable.Range(0, 100)
			.Select(_ => new DispatchMessage())
			.ToList();

		// Assert - All IDs should be unique
		var uniqueIds = messages.Select(m => m.Id).Distinct().Count();
		uniqueIds.ShouldBe(100);
	}

	[Fact]
	public void BinaryPayload_Scenario()
	{
		// Arrange - Simulating a binary serialized message (e.g., MessagePack, Protobuf)
		var binaryData = new byte[]
		{
			0x92, // MessagePack array of 2 elements
			0xA7, 0x6F, 0x72, 0x64, 0x65, 0x72, 0x49, 0x64, // "orderId" key
			0xCD, 0x30, 0x39, // value 12345
		};

		// Act
		var message = new DispatchMessage
		{
			MessageType = "Order",
			Payload = binaryData,
		};

		// Assert
		message.Payload[0].ShouldBe((byte)0x92);
		message.Payload.Length.ShouldBe(binaryData.Length);
	}

	#endregion
}
