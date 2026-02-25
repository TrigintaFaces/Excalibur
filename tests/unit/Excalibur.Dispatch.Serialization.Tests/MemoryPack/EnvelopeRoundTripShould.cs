// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Excalibur.Dispatch.Serialization.MemoryPack;

namespace Excalibur.Dispatch.Serialization.Tests.MemoryPack;

/// <summary>
/// Conformance tests for envelope type round-trip serialization per ADR-058.
/// </summary>
/// <remarks>
/// These tests verify that all internal envelope types serialize and deserialize correctly,
/// preserving all fields including nullable properties and collection types.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class EnvelopeRoundTripShould
{
	private readonly IInternalSerializer _sut;

	public EnvelopeRoundTripShould()
	{
		_sut = new MemoryPackInternalSerializer();
	}

	#region OutboxEnvelope Tests

	[Fact]
	public void RoundTrip_OutboxEnvelope_With_All_Fields()
	{
		// Arrange
		var original = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "OrderCreated",
			Payload = [1, 2, 3, 4, 5],
			CreatedAt = DateTimeOffset.UtcNow,
			Headers = new Dictionary<string, string>
			{
				["tenant"] = "acme",
				["source"] = "api",
			},
			CorrelationId = "corr-123",
			CausationId = "cause-456",
			SchemaVersion = 1,
		};

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<OutboxEnvelope>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.MessageId.ShouldBe(original.MessageId);
		deserialized.MessageType.ShouldBe(original.MessageType);
		deserialized.Payload.ShouldBe(original.Payload);
		deserialized.CreatedAt.ShouldBe(original.CreatedAt);
		_ = deserialized.Headers.ShouldNotBeNull();
		deserialized.Headers["tenant"].ShouldBe("acme");
		deserialized.Headers["source"].ShouldBe("api");
		deserialized.CorrelationId.ShouldBe(original.CorrelationId);
		deserialized.CausationId.ShouldBe(original.CausationId);
		deserialized.SchemaVersion.ShouldBe(1);
	}

	[Fact]
	public void RoundTrip_OutboxEnvelope_With_Null_Optional_Fields()
	{
		// Arrange
		var original = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "MinimalMessage",
			Payload = [],
			CreatedAt = DateTimeOffset.UtcNow,
			Headers = null,
			CorrelationId = null,
			CausationId = null,
		};

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<OutboxEnvelope>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.MessageId.ShouldBe(original.MessageId);
		deserialized.Headers.ShouldBeNull();
		deserialized.CorrelationId.ShouldBeNull();
		deserialized.CausationId.ShouldBeNull();
	}

	#endregion OutboxEnvelope Tests

	#region InboxEnvelope Tests

	[Fact]
	public void RoundTrip_InboxEnvelope_With_All_Fields()
	{
		// Arrange
		var original = new InboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "OrderReceived",
			Payload = [10, 20, 30],
			ReceivedAt = DateTimeOffset.UtcNow,
			Metadata = new Dictionary<string, string>
			{
				["processed-by"] = "inbox-processor-1",
			},
			CorrelationId = "corr-inbox",
			SourceTransport = "rabbitmq",
			SchemaVersion = 1,
		};

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<InboxEnvelope>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.MessageId.ShouldBe(original.MessageId);
		deserialized.MessageType.ShouldBe(original.MessageType);
		deserialized.Payload.ShouldBe(original.Payload);
		deserialized.ReceivedAt.ShouldBe(original.ReceivedAt);
		_ = deserialized.Metadata.ShouldNotBeNull();
		deserialized.Metadata["processed-by"].ShouldBe("inbox-processor-1");
		deserialized.CorrelationId.ShouldBe(original.CorrelationId);
		deserialized.SourceTransport.ShouldBe(original.SourceTransport);
		deserialized.SchemaVersion.ShouldBe(1);
	}

	#endregion InboxEnvelope Tests

	#region EventEnvelope Tests

	[Fact]
	public void RoundTrip_EventEnvelope_With_All_Fields()
	{
		// Arrange
		var original = new EventEnvelope
		{
			EventId = Guid.NewGuid(),
			AggregateId = Guid.NewGuid(),
			AggregateType = "Order",
			EventType = "OrderCreatedEvent",
			Version = 5,
			Payload = [100, 101, 102, 103],
			OccurredAt = DateTimeOffset.UtcNow,
			Metadata = new Dictionary<string, string>
			{
				["user-id"] = "user-123",
				["ip-address"] = "192.168.1.1",
			},
			SchemaVersion = 1,
		};

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<EventEnvelope>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.EventId.ShouldBe(original.EventId);
		deserialized.AggregateId.ShouldBe(original.AggregateId);
		deserialized.AggregateType.ShouldBe(original.AggregateType);
		deserialized.EventType.ShouldBe(original.EventType);
		deserialized.Version.ShouldBe(5);
		deserialized.Payload.ShouldBe(original.Payload);
		deserialized.OccurredAt.ShouldBe(original.OccurredAt);
		_ = deserialized.Metadata.ShouldNotBeNull();
		deserialized.Metadata["user-id"].ShouldBe("user-123");
		deserialized.SchemaVersion.ShouldBe(1);
	}

	#endregion EventEnvelope Tests

	#region SnapshotEnvelope Tests

	[Fact]
	public void RoundTrip_SnapshotEnvelope_With_All_Fields()
	{
		// Arrange
		var original = new SnapshotEnvelope
		{
			AggregateId = Guid.NewGuid(),
			AggregateType = "ShoppingCart",
			Version = 100,
			State = new byte[1024], // 1KB snapshot state
			CreatedAt = DateTimeOffset.UtcNow,
			Metadata = new Dictionary<string, string>
			{
				["snapshot-reason"] = "periodic",
			},
			SchemaVersion = 1,
		};

		// Fill state with test data
		for (var i = 0; i < original.State.Length; i++)
		{
			original.State[i] = (byte)(i % 256);
		}

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<SnapshotEnvelope>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.AggregateId.ShouldBe(original.AggregateId);
		deserialized.AggregateType.ShouldBe(original.AggregateType);
		deserialized.Version.ShouldBe(100);
		deserialized.State.ShouldBe(original.State);
		deserialized.CreatedAt.ShouldBe(original.CreatedAt);
		deserialized.Metadata["snapshot-reason"].ShouldBe("periodic");
		deserialized.SchemaVersion.ShouldBe(1);
	}

	#endregion SnapshotEnvelope Tests

	#region TransportEnvelope Tests

	[Fact]
	public void RoundTrip_TransportEnvelope_With_All_Fields()
	{
		// Arrange
		var original = new TransportEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "IntegrationEvent",
			Payload = [200, 201, 202],
			Timestamp = DateTimeOffset.UtcNow,
			SourceTransport = "kafka",
			TargetTransport = "rabbitmq",
			Headers = new Dictionary<string, string>
			{
				["routing-key"] = "orders.created",
			},
			CorrelationId = "corr-transport",
			CausationId = "cause-transport",
			SchemaVersion = 1,
		};

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<TransportEnvelope>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.MessageId.ShouldBe(original.MessageId);
		deserialized.MessageType.ShouldBe(original.MessageType);
		deserialized.Payload.ShouldBe(original.Payload);
		deserialized.Timestamp.ShouldBe(original.Timestamp);
		deserialized.SourceTransport.ShouldBe(original.SourceTransport);
		deserialized.TargetTransport.ShouldBe(original.TargetTransport);
		deserialized.Headers["routing-key"].ShouldBe("orders.created");
		deserialized.CorrelationId.ShouldBe(original.CorrelationId);
		deserialized.CausationId.ShouldBe(original.CausationId);
		deserialized.SchemaVersion.ShouldBe(1);
	}

	#endregion TransportEnvelope Tests

	#region Large Payload Tests

	[Fact]
	public void RoundTrip_OutboxEnvelope_With_Large_Payload()
	{
		// Arrange
		var largePayload = new byte[64 * 1024]; // 64KB payload
		Random.Shared.NextBytes(largePayload);

		var original = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "LargeMessage",
			Payload = largePayload,
			CreatedAt = DateTimeOffset.UtcNow,
		};

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<OutboxEnvelope>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Payload.ShouldBe(largePayload);
	}

	#endregion Large Payload Tests

	#region Schema Version Tests

	[Fact]
	public void Preserve_Default_SchemaVersion()
	{
		// Arrange - verify default schema version is 1
		var envelope = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "Test",
			Payload = [],
			CreatedAt = DateTimeOffset.UtcNow,
		};

		// Act
		var bytes = _sut.Serialize(envelope);
		var deserialized = _sut.Deserialize<OutboxEnvelope>(bytes.AsSpan());

		// Assert
		deserialized.SchemaVersion.ShouldBe(1);
	}

	#endregion Schema Version Tests
}
