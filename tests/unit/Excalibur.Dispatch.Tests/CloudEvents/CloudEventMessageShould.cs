// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventMessageShould
{
	[Fact]
	public void DefaultConstructor_SetsDefaults()
	{
		// Act
		var message = new CloudEventMessage();

		// Assert
		message.MessageId.ShouldNotBeNullOrEmpty();
		message.Type.ShouldBe(string.Empty);
		message.Data.ShouldBeNull();
		message.Timestamp.ShouldNotBe(default);
		message.Headers.ShouldNotBeNull();
		message.Headers.Count.ShouldBe(0);
		message.Features.ShouldNotBeNull();
		message.Kind.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void Body_ReturnsDataWhenSet()
	{
		// Arrange
		var message = new CloudEventMessage { Data = "test-data" };

		// Act & Assert
		message.Body.ShouldBe("test-data");
	}

	[Fact]
	public void Body_ReturnsNewObjectWhenDataIsNull()
	{
		// Arrange
		var message = new CloudEventMessage { Data = null };

		// Act & Assert
		message.Body.ShouldNotBeNull();
	}

	[Fact]
	public void MessageType_ReturnsType()
	{
		// Arrange
		var message = new CloudEventMessage { Type = "com.example.event" };

		// Act & Assert
		message.MessageType.ShouldBe("com.example.event");
	}

	[Fact]
	public void Id_ParsesValidGuid()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var message = new CloudEventMessage { MessageId = guid.ToString() };

		// Act & Assert
		message.Id.ShouldBe(guid);
	}

	[Fact]
	public void Id_ReturnsEmptyGuidForInvalidMessageId()
	{
		// Arrange
		var message = new CloudEventMessage { MessageId = "not-a-guid" };

		// Act & Assert
		message.Id.ShouldBe(Guid.Empty);
	}

	[Fact]
	public void ImplementsIDispatchMessage()
	{
		// Arrange & Act
		IDispatchMessage message = new CloudEventMessage();

		// Assert
		message.ShouldNotBeNull();
	}

	[Fact]
	public void AllProperties_AreSettable()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow.AddHours(-1);

		// Act
		var message = new CloudEventMessage
		{
			MessageId = "custom-id",
			Type = "my.event.type",
			Data = new { Value = 42 },
			Timestamp = timestamp,
		};

		// Assert
		message.MessageId.ShouldBe("custom-id");
		message.Type.ShouldBe("my.event.type");
		message.Timestamp.ShouldBe(timestamp);
	}
}
