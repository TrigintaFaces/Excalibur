// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageEnvelopeBuilderShould
{
	[Fact]
	public void Build_WithDefaults_GenerateMessageIdAndTimestamp()
	{
		// Act
		var envelope = new MessageEnvelopeBuilder().Build();

		// Assert
		envelope.MessageId.ShouldNotBeNullOrWhiteSpace();
		envelope.TimestampTicks.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Build_WithExplicitMessageId_UseProvidedId()
	{
		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithMessageId("test-id-123")
			.Build();

		// Assert
		envelope.MessageId.ShouldBe("test-id-123");
	}

	[Fact]
	public void Build_WithBody_SetBody()
	{
		// Arrange
		var bodyData = new byte[] { 1, 2, 3, 4 };

		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithBody(bodyData)
			.Build();

		// Assert
		envelope.Body.ToArray().ShouldBe(bodyData);
	}

	[Fact]
	public void Build_WithTimestamp_SetTimestamp()
	{
		// Arrange
		var timestamp = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithTimestamp(timestamp)
			.Build();

		// Assert
		envelope.TimestampTicks.ShouldBe(timestamp.Ticks);
	}

	[Fact]
	public void Build_WithTimestampTicks_SetTimestampTicks()
	{
		// Arrange
		var ticks = 638_700_000_000_000_000L;

		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithTimestampTicks(ticks)
			.Build();

		// Assert
		envelope.TimestampTicks.ShouldBe(ticks);
	}

	[Fact]
	public void Build_WithSingleHeader_AddHeader()
	{
		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithHeader("Content-Type", "application/json")
			.Build();

		// Assert
		envelope.Headers.ShouldNotBeNull();
		envelope.Headers["Content-Type"].ShouldBe("application/json");
	}

	[Fact]
	public void Build_WithMultipleHeaders_AddAllHeaders()
	{
		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithHeaders(
				("Content-Type", "application/json"),
				("X-Request-Id", "req-123"))
			.Build();

		// Assert
		envelope.Headers.ShouldNotBeNull();
		envelope.Headers.Count.ShouldBe(2);
		envelope.Headers["Content-Type"].ShouldBe("application/json");
		envelope.Headers["X-Request-Id"].ShouldBe("req-123");
	}

	[Fact]
	public void Build_WithCorrelationId_SetCorrelationId()
	{
		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithCorrelationId("corr-456")
			.Build();

		// Assert
		envelope.CorrelationId.ShouldBe("corr-456");
	}

	[Fact]
	public void Build_WithMessageType_SetMessageType()
	{
		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithMessageType("OrderCreated")
			.Build();

		// Assert
		envelope.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void Build_WithPriority_SetPriority()
	{
		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithPriority(10)
			.Build();

		// Assert
		envelope.Priority.ShouldBe((byte)10);
	}

	[Fact]
	public void Build_WithTimeToLive_SetTimeToLive()
	{
		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithTimeToLive(TimeSpan.FromMinutes(5))
			.Build();

		// Assert
		envelope.TimeToLiveSeconds.ShouldBe(300);
	}

	[Fact]
	public void Build_FluentChaining_SetAllProperties()
	{
		// Arrange
		var body = new byte[] { 10, 20, 30 };

		// Act
		var envelope = new MessageEnvelopeBuilder()
			.WithMessageId("msg-1")
			.WithBody(body)
			.WithTimestamp(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
			.WithHeader("key", "value")
			.WithCorrelationId("corr-1")
			.WithMessageType("TestEvent")
			.WithPriority(5)
			.WithTimeToLive(TimeSpan.FromHours(1))
			.Build();

		// Assert
		envelope.MessageId.ShouldBe("msg-1");
		envelope.Body.ToArray().ShouldBe(body);
		envelope.CorrelationId.ShouldBe("corr-1");
		envelope.MessageType.ShouldBe("TestEvent");
		envelope.Priority.ShouldBe((byte)5);
		envelope.TimeToLiveSeconds.ShouldBe(3600);
	}

	[Fact]
	public void WithHeaders_ThrowOnNull()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => builder.WithHeaders(null!));
	}

	[Fact]
	public void Equals_SameProperties_ReturnTrue()
	{
		// Arrange
		var builder1 = new MessageEnvelopeBuilder()
			.WithMessageId("test-id")
			.WithCorrelationId("corr-1")
			.WithPriority(5);

		var builder2 = new MessageEnvelopeBuilder()
			.WithMessageId("test-id")
			.WithCorrelationId("corr-1")
			.WithPriority(5);

		// Act & Assert
		builder1.Equals(builder2).ShouldBeTrue();
		(builder1 == builder2).ShouldBeTrue();
		(builder1 != builder2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_DifferentProperties_ReturnFalse()
	{
		// Arrange
		var builder1 = new MessageEnvelopeBuilder()
			.WithMessageId("test-id-1");

		var builder2 = new MessageEnvelopeBuilder()
			.WithMessageId("test-id-2");

		// Act & Assert
		builder1.Equals(builder2).ShouldBeFalse();
		(builder1 != builder2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_Object_ReturnTrue()
	{
		// Arrange
		var builder1 = new MessageEnvelopeBuilder()
			.WithMessageId("test");
		object builder2 = new MessageEnvelopeBuilder()
			.WithMessageId("test");

		// Act & Assert
		builder1.Equals(builder2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_NullObject_ReturnFalse()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder();

		// Act & Assert
		builder.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_SameProperties_ReturnSameHash()
	{
		// Arrange
		var builder1 = new MessageEnvelopeBuilder()
			.WithMessageId("test")
			.WithPriority(5);

		var builder2 = new MessageEnvelopeBuilder()
			.WithMessageId("test")
			.WithPriority(5);

		// Act & Assert
		builder1.GetHashCode().ShouldBe(builder2.GetHashCode());
	}

	[Fact]
	public void Dispose_NotThrow()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder();

		// Act & Assert - should not throw
		builder.Dispose();
	}
}
