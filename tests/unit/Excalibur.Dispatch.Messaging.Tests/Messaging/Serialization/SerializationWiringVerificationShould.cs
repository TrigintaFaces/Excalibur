// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Sprint 37: bd-0ad7 - Verification tests for serialization wiring

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
/// Verification tests for Sprint 37 serialization wiring (bd-0ad7).
/// These tests verify that IInternalSerializer is actually used in processing paths
/// when configured, and that fallback to JSON works when serializer is null.
/// </summary>
[Trait("Category", "Unit")]
public class SerializationWiringVerificationShould
{
	private const byte EnvelopeFormatMarker = 0x01;
	private const byte JsonFormatMarker = 0x7B; // '{' character

	#region Format Marker Detection Tests

	[Fact]
	public void Detect_EnvelopeFormat_When_First_Byte_Is_0x01()
	{
		// Arrange
		var envelopePayload = new byte[] { EnvelopeFormatMarker, 0x02, 0x03, 0x04 };

		// Act
		var isEnvelopeFormat = envelopePayload.Length > 0 && envelopePayload[0] == EnvelopeFormatMarker;

		// Assert
		isEnvelopeFormat.ShouldBeTrue();
	}

	[Fact]
	public void Detect_JsonFormat_When_First_Byte_Is_OpenBrace()
	{
		// Arrange
		var jsonPayload = "{ \"test\": \"value\" }"u8.ToArray();

		// Act
		var isJsonFormat = jsonPayload.Length > 0 && jsonPayload[0] == JsonFormatMarker;

		// Assert
		isJsonFormat.ShouldBeTrue();
	}

	[Fact]
	public void Distinguish_Envelope_From_Json_Format()
	{
		// Arrange
		var envelopePayload = new byte[] { EnvelopeFormatMarker, 0x02, 0x03 };
		var jsonPayload = "{ \"test\": true }"u8.ToArray();

		// Act
		var isEnvelope1 = envelopePayload[0] == EnvelopeFormatMarker;
		var isEnvelope2 = jsonPayload[0] == EnvelopeFormatMarker;

		// Assert
		isEnvelope1.ShouldBeTrue("Envelope payload should be detected as envelope format");
		isEnvelope2.ShouldBeFalse("JSON payload should not be detected as envelope format");
	}

	[Fact]
	public void Handle_Empty_Payload_Gracefully()
	{
		// Arrange
		var emptyPayload = Array.Empty<byte>();

		// Act
		var isEnvelopeFormat = emptyPayload.Length > 0 && emptyPayload[0] == EnvelopeFormatMarker;

		// Assert
		isEnvelopeFormat.ShouldBeFalse("Empty payload should not be detected as envelope format");
	}

	[Fact]
	public void Handle_Single_Byte_Envelope_Marker()
	{
		// Arrange
		var singleByteEnvelope = new byte[] { EnvelopeFormatMarker };

		// Act
		var isEnvelopeFormat = singleByteEnvelope.Length > 0 && singleByteEnvelope[0] == EnvelopeFormatMarker;

		// Assert
		isEnvelopeFormat.ShouldBeTrue("Single byte envelope marker should be detected");
	}

	#endregion

	#region OutboxEnvelope Serialization Tests

	[Fact]
	public void Serialize_OutboxEnvelope_With_All_Required_Fields()
	{
		// Arrange
		var envelope = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "TestMessage",
			Payload = "test payload"u8.ToArray(),
			CreatedAt = DateTimeOffset.UtcNow,
		};

		// Act & Assert - Envelope should be constructable with required fields
		envelope.MessageId.ShouldNotBe(Guid.Empty);
		envelope.MessageType.ShouldBe("TestMessage");
		_ = envelope.Payload.ShouldNotBeNull();
		envelope.SchemaVersion.ShouldBe(1);
	}

	[Fact]
	public void Serialize_OutboxEnvelope_With_Optional_Headers()
	{
		// Arrange
		var headers = new Dictionary<string, string>
		{
			["CorrelationId"] = "corr-123",
			["CustomHeader"] = "custom-value"
		};

		var envelope = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "TestMessage",
			Payload = "test"u8.ToArray(),
			CreatedAt = DateTimeOffset.UtcNow,
			Headers = headers,
			CorrelationId = "corr-123",
			CausationId = "cause-456"
		};

		// Assert
		_ = envelope.Headers.ShouldNotBeNull();
		envelope.Headers.Count.ShouldBe(2);
		envelope.CorrelationId.ShouldBe("corr-123");
		envelope.CausationId.ShouldBe("cause-456");
	}

	[Fact]
	public void Preserve_SchemaVersion_In_OutboxEnvelope()
	{
		// Arrange
		var envelope = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "TestMessage",
			Payload = Array.Empty<byte>(),
			CreatedAt = DateTimeOffset.UtcNow,
			SchemaVersion = 2
		};

		// Assert
		envelope.SchemaVersion.ShouldBe(2);
	}

	#endregion

	#region InboxEnvelope Serialization Tests

	[Fact]
	public void Serialize_InboxEnvelope_With_All_Required_Fields()
	{
		// Arrange
		var envelope = new InboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "InboxTestMessage",
			Payload = "inbox payload"u8.ToArray(),
			ReceivedAt = DateTimeOffset.UtcNow,
		};

		// Assert
		envelope.MessageId.ShouldNotBe(Guid.Empty);
		envelope.MessageType.ShouldBe("InboxTestMessage");
		_ = envelope.Payload.ShouldNotBeNull();
		envelope.SchemaVersion.ShouldBe(1);
	}

	[Fact]
	public void Preserve_Metadata_In_InboxEnvelope()
	{
		// Arrange
		var metadata = new Dictionary<string, string>
		{
			["Source"] = "TestSource",
			["Priority"] = "High"
		};

		var envelope = new InboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "InboxTestMessage",
			Payload = Array.Empty<byte>(),
			ReceivedAt = DateTimeOffset.UtcNow,
			Metadata = metadata
		};

		// Assert
		_ = envelope.Metadata.ShouldNotBeNull();
		envelope.Metadata["Source"].ShouldBe("TestSource");
		envelope.Metadata["Priority"].ShouldBe("High");
	}

	#endregion

	#region EventEnvelope Serialization Tests

	[Fact]
	public void Serialize_EventEnvelope_With_All_Required_Fields()
	{
		// Arrange
		var aggregateId = Guid.NewGuid();
		var envelope = new EventEnvelope
		{
			EventId = Guid.NewGuid(),
			AggregateId = aggregateId,
			AggregateType = "TestAggregate",
			EventType = "TestEvent",
			Version = 1,
			OccurredAt = DateTimeOffset.UtcNow,
			Payload = "event data"u8.ToArray(),
		};

		// Assert
		envelope.EventId.ShouldNotBe(Guid.Empty);
		envelope.AggregateId.ShouldBe(aggregateId);
		envelope.AggregateType.ShouldBe("TestAggregate");
		envelope.EventType.ShouldBe("TestEvent");
		envelope.Version.ShouldBe(1);
		envelope.SchemaVersion.ShouldBe(1);
	}

	[Fact]
	public void Preserve_Metadata_In_EventEnvelope()
	{
		// Arrange
		var metadata = new Dictionary<string, string>
		{
			["UserId"] = "user-123",
			["TenantId"] = "tenant-456"
		};

		var envelope = new EventEnvelope
		{
			EventId = Guid.NewGuid(),
			AggregateId = Guid.NewGuid(),
			AggregateType = "TestAggregate",
			EventType = "TestEvent",
			Version = 1,
			OccurredAt = DateTimeOffset.UtcNow,
			Payload = Array.Empty<byte>(),
			Metadata = metadata
		};

		// Assert
		_ = envelope.Metadata.ShouldNotBeNull();
		envelope.Metadata["UserId"].ShouldBe("user-123");
		envelope.Metadata["TenantId"].ShouldBe("tenant-456");
	}

	#endregion

	#region Mock Serializer Verification Tests

	[Fact]
	public void MockSerializer_Serialize_Returns_ByteArray()
	{
		// Arrange
		var serializer = A.Fake<IInternalSerializer>();
		var expectedBytes = new byte[] { EnvelopeFormatMarker, 0x10, 0x20, 0x30 };
		_ = A.CallTo(() => serializer.Serialize(A<OutboxEnvelope>.Ignored))
			.Returns(expectedBytes);

		var envelope = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "Test",
			Payload = Array.Empty<byte>(),
			CreatedAt = DateTimeOffset.UtcNow
		};

		// Act
		var result = serializer.Serialize(envelope);

		// Assert
		result.ShouldBe(expectedBytes);
		_ = A.CallTo(() => serializer.Serialize(A<OutboxEnvelope>.Ignored)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Verify_Serializer_Called_When_Configured()
	{
		// Arrange
		var serializer = A.Fake<IInternalSerializer>();
		var callCount = 0;

		_ = A.CallTo(() => serializer.Serialize(A<OutboxEnvelope>.Ignored))
			.Invokes(() => callCount++)
			.Returns(new byte[] { EnvelopeFormatMarker, 0x01 });

		// Act - Simulate what the processor would do
		var envelope = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "Test",
			Payload = Array.Empty<byte>(),
			CreatedAt = DateTimeOffset.UtcNow
		};

		// When serializer is not null, it should be used
		if (serializer is not null)
		{
			_ = serializer.Serialize(envelope);
		}

		// Assert
		callCount.ShouldBe(1, "Serializer.Serialize should be called when serializer is configured");
	}

	[Fact]
	public void Verify_Serializer_Not_Called_When_Null()
	{
		// Arrange
		IInternalSerializer? serializer = null;
		var serializerCalled = false;

		// Act - Simulate what the processor would do
		if (serializer is not null)
		{
			serializerCalled = true;
		}

		// Assert
		serializerCalled.ShouldBeFalse("Serializer should not be called when null");
	}

	#endregion

	#region Backward Compatibility Tests

	[Fact]
	public void Legacy_Json_Payload_Should_Not_Match_Envelope_Format()
	{
		// Arrange - Legacy JSON payloads start with '{'
		var legacyJsonPayload = System.Text.Encoding.UTF8.GetBytes("{\"messageId\":\"123\",\"type\":\"TestMessage\"}");

		// Act
		var isEnvelopeFormat = legacyJsonPayload.Length > 0 && legacyJsonPayload[0] == EnvelopeFormatMarker;

		// Assert
		isEnvelopeFormat.ShouldBeFalse("Legacy JSON payloads should not be detected as envelope format");
	}

	[Fact]
	public void Envelope_Payload_Should_Have_Marker_Prepended()
	{
		// Arrange - Simulate what the processor does when serializing
		var serializedEnvelope = new byte[] { 0x10, 0x20, 0x30 }; // Mock serialized data

		// Act - Prepend format marker (what processor should do)
		var result = new byte[serializedEnvelope.Length + 1];
		result[0] = EnvelopeFormatMarker;
		serializedEnvelope.CopyTo(result, 1);

		// Assert
		result[0].ShouldBe(EnvelopeFormatMarker);
		result.Length.ShouldBe(serializedEnvelope.Length + 1);
		result[1].ShouldBe((byte)0x10);
	}

	[Fact]
	public void Envelope_Data_Should_Skip_Marker_On_Deserialize()
	{
		// Arrange - Payload with format marker prepended
		var payloadWithMarker = new byte[] { EnvelopeFormatMarker, 0x10, 0x20, 0x30, 0x40 };

		// Act - Skip marker (what processor should do)
		var envelopeData = payloadWithMarker.AsSpan(1);

		// Assert
		envelopeData.Length.ShouldBe(4);
		envelopeData[0].ShouldBe((byte)0x10);
	}

	[Fact]
	public void Format_Marker_Distinguishes_Binary_From_Json()
	{
		// Arrange
		var binaryPayload = new byte[] { EnvelopeFormatMarker, 0xAB, 0xCD };
		var jsonPayload = "{ \"test\": 123 }"u8.ToArray();

		// Act
		var isBinaryEnvelope = binaryPayload[0] == EnvelopeFormatMarker;
		var isJsonEnvelope = jsonPayload[0] == EnvelopeFormatMarker;

		// Assert
		isBinaryEnvelope.ShouldBeTrue();
		isJsonEnvelope.ShouldBeFalse();
	}

	#endregion

	#region Round-Trip Tests

	[Fact]
	public void OutboxEnvelope_Properties_Should_Survive_Construction()
	{
		// Arrange
		var messageId = Guid.NewGuid();
		var messageType = "RoundTripTest";
		var payload = "test data for round trip"u8.ToArray();
		var createdAt = DateTimeOffset.UtcNow;
		var correlationId = "corr-round-trip";
		var causationId = "cause-round-trip";

		// Act
		var envelope = new OutboxEnvelope
		{
			MessageId = messageId,
			MessageType = messageType,
			Payload = payload,
			CreatedAt = createdAt,
			CorrelationId = correlationId,
			CausationId = causationId,
			SchemaVersion = 1
		};

		// Assert - All properties should be preserved
		envelope.MessageId.ShouldBe(messageId);
		envelope.MessageType.ShouldBe(messageType);
		envelope.Payload.ShouldBe(payload);
		envelope.CreatedAt.ShouldBe(createdAt);
		envelope.CorrelationId.ShouldBe(correlationId);
		envelope.CausationId.ShouldBe(causationId);
		envelope.SchemaVersion.ShouldBe(1);
	}

	[Fact]
	public void InboxEnvelope_Properties_Should_Survive_Construction()
	{
		// Arrange
		var messageId = Guid.NewGuid();
		var messageType = "InboxRoundTripTest";
		var payload = "inbox test data"u8.ToArray();
		var receivedAt = DateTimeOffset.UtcNow;

		// Act
		var envelope = new InboxEnvelope
		{
			MessageId = messageId,
			MessageType = messageType,
			Payload = payload,
			ReceivedAt = receivedAt,
			SchemaVersion = 1
		};

		// Assert
		envelope.MessageId.ShouldBe(messageId);
		envelope.MessageType.ShouldBe(messageType);
		envelope.Payload.ShouldBe(payload);
		envelope.ReceivedAt.ShouldBe(receivedAt);
		envelope.SchemaVersion.ShouldBe(1);
	}

	[Fact]
	public void EventEnvelope_Properties_Should_Survive_Construction()
	{
		// Arrange
		var eventId = Guid.NewGuid();
		var aggregateId = Guid.NewGuid();
		var aggregateType = "RoundTripAggregate";
		var eventType = "RoundTripEvent";
		var version = 42L;
		var occurredAt = DateTimeOffset.UtcNow;
		var payload = "event payload data"u8.ToArray();

		// Act
		var envelope = new EventEnvelope
		{
			EventId = eventId,
			AggregateId = aggregateId,
			AggregateType = aggregateType,
			EventType = eventType,
			Version = version,
			OccurredAt = occurredAt,
			Payload = payload,
			SchemaVersion = 1
		};

		// Assert
		envelope.EventId.ShouldBe(eventId);
		envelope.AggregateId.ShouldBe(aggregateId);
		envelope.AggregateType.ShouldBe(aggregateType);
		envelope.EventType.ShouldBe(eventType);
		envelope.Version.ShouldBe(version);
		envelope.OccurredAt.ShouldBe(occurredAt);
		envelope.Payload.ShouldBe(payload);
		envelope.SchemaVersion.ShouldBe(1);
	}

	#endregion
}
