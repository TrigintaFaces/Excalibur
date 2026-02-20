// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="MessageEnvelopeStruct"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class MessageEnvelopeStructShould
{
	#region Constructor Tests

	[Fact]
	public void Construct_WithRequiredParameters_SetsAllProperties()
	{
		// Arrange
		var messageId = "msg-123";
		var body = new byte[] { 1, 2, 3 };
		var timestampTicks = DateTime.UtcNow.Ticks;

		// Act
		var envelope = new MessageEnvelopeStruct(messageId, body, timestampTicks);

		// Assert
		envelope.MessageId.ShouldBe(messageId);
		envelope.Body.ToArray().ShouldBe(body);
		envelope.TimestampTicks.ShouldBe(timestampTicks);
		envelope.Headers.ShouldBeNull();
		envelope.CorrelationId.ShouldBeNull();
		envelope.MessageType.ShouldBeNull();
		envelope.Priority.ShouldBe((byte)0);
		envelope.TimeToLiveSeconds.ShouldBe(0);
	}

	[Fact]
	public void Construct_WithAllParameters_SetsAllProperties()
	{
		// Arrange
		var messageId = "msg-456";
		var body = new byte[] { 4, 5, 6 };
		var timestampTicks = DateTime.UtcNow.Ticks;
		var headers = new Dictionary<string, string> { ["key"] = "value" };
		var correlationId = "corr-789";
		var messageType = "TestMessage";
		byte priority = 5;
		var timeToLiveSeconds = 300;

		// Act
		var envelope = new MessageEnvelopeStruct(
			messageId,
			body,
			timestampTicks,
			headers,
			correlationId,
			messageType,
			priority,
			timeToLiveSeconds);

		// Assert
		envelope.MessageId.ShouldBe(messageId);
		envelope.Body.ToArray().ShouldBe(body);
		envelope.TimestampTicks.ShouldBe(timestampTicks);
		envelope.Headers.ShouldBe(headers);
		envelope.CorrelationId.ShouldBe(correlationId);
		envelope.MessageType.ShouldBe(messageType);
		envelope.Priority.ShouldBe(priority);
		envelope.TimeToLiveSeconds.ShouldBe(timeToLiveSeconds);
	}

	[Fact]
	public void Construct_WithNullMessageId_ThrowsArgumentNullException()
	{
		// Arrange
		var body = new byte[] { 1, 2, 3 };
		var timestampTicks = DateTime.UtcNow.Ticks;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MessageEnvelopeStruct(null!, body, timestampTicks));
	}

	#endregion

	#region Computed Property Tests

	[Fact]
	public void Timestamp_ReturnsCorrectDateTime()
	{
		// Arrange
		var expectedTimestamp = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
		var envelope = new MessageEnvelopeStruct("msg-1", Array.Empty<byte>(), expectedTimestamp.Ticks);

		// Act
		var timestamp = envelope.Timestamp;

		// Assert
		timestamp.ShouldBe(expectedTimestamp);
		timestamp.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	public void TimeToLive_ReturnsCorrectTimeSpan()
	{
		// Arrange
		var envelope = new MessageEnvelopeStruct(
			"msg-1",
			Array.Empty<byte>(),
			DateTime.UtcNow.Ticks,
			timeToLiveSeconds: 300);

		// Act
		var ttl = envelope.TimeToLive;

		// Assert
		ttl.TotalSeconds.ShouldBe(300);
	}

	[Fact]
	public void TimeToLive_WithZeroSeconds_ReturnsZeroTimeSpan()
	{
		// Arrange
		var envelope = new MessageEnvelopeStruct("msg-1", Array.Empty<byte>(), DateTime.UtcNow.Ticks);

		// Act
		var ttl = envelope.TimeToLive;

		// Assert
		ttl.ShouldBe(TimeSpan.Zero);
	}

	#endregion

	#region With Method Tests

	[Fact]
	public void WithMessageId_CreatesNewEnvelopeWithModifiedId()
	{
		// Arrange
		var original = CreateStandardEnvelope();

		// Act
		var modified = original.WithMessageId("new-id");

		// Assert
		modified.MessageId.ShouldBe("new-id");
		modified.Body.ToArray().ShouldBe(original.Body.ToArray());
		modified.CorrelationId.ShouldBe(original.CorrelationId);
	}

	[Fact]
	public void WithBody_CreatesNewEnvelopeWithModifiedBody()
	{
		// Arrange
		var original = CreateStandardEnvelope();
		var newBody = new byte[] { 100, 101, 102 };

		// Act
		var modified = original.WithBody(newBody);

		// Assert
		modified.Body.ToArray().ShouldBe(newBody);
		modified.MessageId.ShouldBe(original.MessageId);
	}

	[Fact]
	public void WithCorrelationId_CreatesNewEnvelopeWithModifiedCorrelationId()
	{
		// Arrange
		var original = CreateStandardEnvelope();

		// Act
		var modified = original.WithCorrelationId("new-corr-id");

		// Assert
		modified.CorrelationId.ShouldBe("new-corr-id");
		modified.MessageId.ShouldBe(original.MessageId);
	}

	[Fact]
	public void WithMessageType_CreatesNewEnvelopeWithModifiedType()
	{
		// Arrange
		var original = CreateStandardEnvelope();

		// Act
		var modified = original.WithMessageType("NewMessageType");

		// Assert
		modified.MessageType.ShouldBe("NewMessageType");
		modified.MessageId.ShouldBe(original.MessageId);
	}

	[Fact]
	public void WithPriority_CreatesNewEnvelopeWithModifiedPriority()
	{
		// Arrange
		var original = CreateStandardEnvelope();

		// Act
		var modified = original.WithPriority(10);

		// Assert
		modified.Priority.ShouldBe((byte)10);
		modified.MessageId.ShouldBe(original.MessageId);
	}

	[Fact]
	public void WithTimeToLive_CreatesNewEnvelopeWithModifiedTtl()
	{
		// Arrange
		var original = CreateStandardEnvelope();

		// Act
		var modified = original.WithTimeToLive(TimeSpan.FromMinutes(10));

		// Assert
		modified.TimeToLiveSeconds.ShouldBe(600);
		modified.MessageId.ShouldBe(original.MessageId);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameValues_ReturnsTrue()
	{
		// Arrange
		var timestampTicks = DateTime.UtcNow.Ticks;
		var body = new byte[] { 1, 2, 3 }; // Same byte array reference
		var envelope1 = new MessageEnvelopeStruct("msg-1", body, timestampTicks);
		var envelope2 = new MessageEnvelopeStruct("msg-1", body, timestampTicks);

		// Act & Assert
		envelope1.Equals(envelope2).ShouldBeTrue();
		(envelope1 == envelope2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentMessageIds_ReturnsFalse()
	{
		// Arrange
		var timestampTicks = DateTime.UtcNow.Ticks;
		var envelope1 = new MessageEnvelopeStruct("msg-1", new byte[] { 1, 2, 3 }, timestampTicks);
		var envelope2 = new MessageEnvelopeStruct("msg-2", new byte[] { 1, 2, 3 }, timestampTicks);

		// Act & Assert
		envelope1.Equals(envelope2).ShouldBeFalse();
		(envelope1 != envelope2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentBodies_ReturnsFalse()
	{
		// Arrange
		var timestampTicks = DateTime.UtcNow.Ticks;
		var envelope1 = new MessageEnvelopeStruct("msg-1", new byte[] { 1, 2, 3 }, timestampTicks);
		var envelope2 = new MessageEnvelopeStruct("msg-1", new byte[] { 4, 5, 6 }, timestampTicks);

		// Act & Assert
		envelope1.Equals(envelope2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithSameHeaders_ReturnsTrue()
	{
		// Arrange
		var timestampTicks = DateTime.UtcNow.Ticks;
		var headers1 = new Dictionary<string, string> { ["key"] = "value" };
		var headers2 = new Dictionary<string, string> { ["key"] = "value" };
		var envelope1 = new MessageEnvelopeStruct("msg-1", Array.Empty<byte>(), timestampTicks, headers1);
		var envelope2 = new MessageEnvelopeStruct("msg-1", Array.Empty<byte>(), timestampTicks, headers2);

		// Act & Assert
		envelope1.Equals(envelope2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentHeaders_ReturnsFalse()
	{
		// Arrange
		var timestampTicks = DateTime.UtcNow.Ticks;
		var headers1 = new Dictionary<string, string> { ["key"] = "value1" };
		var headers2 = new Dictionary<string, string> { ["key"] = "value2" };
		var envelope1 = new MessageEnvelopeStruct("msg-1", Array.Empty<byte>(), timestampTicks, headers1);
		var envelope2 = new MessageEnvelopeStruct("msg-1", Array.Empty<byte>(), timestampTicks, headers2);

		// Act & Assert
		envelope1.Equals(envelope2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithNullAndNonNullHeaders_ReturnsFalse()
	{
		// Arrange
		var timestampTicks = DateTime.UtcNow.Ticks;
		var headers = new Dictionary<string, string> { ["key"] = "value" };
		var envelope1 = new MessageEnvelopeStruct("msg-1", Array.Empty<byte>(), timestampTicks, null);
		var envelope2 = new MessageEnvelopeStruct("msg-1", Array.Empty<byte>(), timestampTicks, headers);

		// Act & Assert
		envelope1.Equals(envelope2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithObject_ReturnsCorrectResult()
	{
		// Arrange
		var timestampTicks = DateTime.UtcNow.Ticks;
		var envelope = new MessageEnvelopeStruct("msg-1", Array.Empty<byte>(), timestampTicks);
		object sameEnvelope = new MessageEnvelopeStruct("msg-1", Array.Empty<byte>(), timestampTicks);
		object differentType = "not an envelope";

		// Act & Assert
		envelope.Equals(sameEnvelope).ShouldBeTrue();
		envelope.Equals(differentType).ShouldBeFalse();
		envelope.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_WithSameValues_ReturnsSameValue()
	{
		// Arrange
		var timestampTicks = DateTime.UtcNow.Ticks;
		var body = new byte[] { 1, 2, 3 }; // Same byte array reference
		var envelope1 = new MessageEnvelopeStruct("msg-1", body, timestampTicks);
		var envelope2 = new MessageEnvelopeStruct("msg-1", body, timestampTicks);

		// Act & Assert
		envelope1.GetHashCode().ShouldBe(envelope2.GetHashCode());
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var body = new byte[] { 1, 2, 3 };
		var envelope = new MessageEnvelopeStruct(
			"msg-123",
			body,
			DateTime.UtcNow.Ticks,
			messageType: "TestMessage");

		// Act
		var result = envelope.ToString();

		// Assert
		result.ShouldContain("msg-123");
		result.ShouldContain("TestMessage");
		result.ShouldContain("3"); // Body length
	}

	[Fact]
	public void ToString_WithNullMessageType_ShowsUnknown()
	{
		// Arrange
		var envelope = new MessageEnvelopeStruct("msg-123", Array.Empty<byte>(), DateTime.UtcNow.Ticks);

		// Act
		var result = envelope.ToString();

		// Assert
		result.ShouldContain("unknown");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Envelope_ForHighPriorityMessage_HasHighPriority()
	{
		// Arrange & Act
		var envelope = new MessageEnvelopeStruct(
			"urgent-msg",
			new byte[] { 1, 2, 3 },
			DateTime.UtcNow.Ticks,
			messageType: "UrgentCommand",
			priority: 10);

		// Assert
		envelope.Priority.ShouldBe((byte)10);
	}

	[Fact]
	public void Envelope_ForExpiringMessage_HasTtl()
	{
		// Arrange & Act
		var envelope = new MessageEnvelopeStruct(
			"temp-msg",
			new byte[] { 1, 2, 3 },
			DateTime.UtcNow.Ticks,
			timeToLiveSeconds: 60);

		// Assert
		envelope.TimeToLive.TotalMinutes.ShouldBe(1);
	}

	[Fact]
	public void Envelope_ForCorrelatedMessages_SharesCorrelationId()
	{
		// Arrange
		var correlationId = "workflow-123";
		var timestampTicks = DateTime.UtcNow.Ticks;

		// Act
		var request = new MessageEnvelopeStruct(
			"req-1",
			new byte[] { 1 },
			timestampTicks,
			correlationId: correlationId,
			messageType: "Request");

		var response = new MessageEnvelopeStruct(
			"resp-1",
			new byte[] { 2 },
			timestampTicks,
			correlationId: correlationId,
			messageType: "Response");

		// Assert
		request.CorrelationId.ShouldBe(response.CorrelationId);
	}

	#endregion

	#region Helper Methods

	private static MessageEnvelopeStruct CreateStandardEnvelope() =>
		new(
			"msg-standard",
			new byte[] { 10, 20, 30 },
			DateTime.UtcNow.Ticks,
			new Dictionary<string, string> { ["header1"] = "value1" },
			"corr-standard",
			"StandardMessage",
			5,
			120);

	#endregion
}
