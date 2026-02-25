// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GooglePubSubSerializationRecordsShould
{
	[Fact]
	public void CreateDeadLetterMetadataWithRequiredParams()
	{
		// Arrange
		var firstFailure = DateTimeOffset.UtcNow.AddMinutes(-10);
		var lastFailure = DateTimeOffset.UtcNow;

		// Act
		var metadata = new DeadLetterMetadata(3, "Timeout", firstFailure, lastFailure);

		// Assert
		metadata.DeliveryAttempts.ShouldBe(3);
		metadata.LastErrorReason.ShouldBe("Timeout");
		metadata.FirstFailureTime.ShouldBe(firstFailure);
		metadata.LastFailureTime.ShouldBe(lastFailure);
		metadata.OriginalTopic.ShouldBeNull();
	}

	[Fact]
	public void CreateDeadLetterMetadataWithOptionalTopic()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var metadata = new DeadLetterMetadata(5, "Schema error", now, now, "orders-topic");

		// Assert
		metadata.OriginalTopic.ShouldBe("orders-topic");
	}

	[Fact]
	public void SupportDeadLetterMetadataEquality()
	{
		// Arrange
		var time = DateTimeOffset.UtcNow;
		var m1 = new DeadLetterMetadata(1, "err", time, time);
		var m2 = new DeadLetterMetadata(1, "err", time, time);

		// Act & Assert
		m1.ShouldBe(m2);
	}

	[Fact]
	public void CreateFlowControlState()
	{
		// Act
		var state = new FlowControlState(100, 1_048_576, 50, 524_288, true);

		// Assert
		state.MaxOutstandingMessages.ShouldBe(100);
		state.MaxOutstandingBytes.ShouldBe(1_048_576);
		state.CurrentOutstandingMessages.ShouldBe(50);
		state.CurrentOutstandingBytes.ShouldBe(524_288);
		state.IsFlowControlActive.ShouldBeTrue();
	}

	[Fact]
	public void SupportFlowControlStateEquality()
	{
		// Arrange
		var s1 = new FlowControlState(10, 100, 5, 50, false);
		var s2 = new FlowControlState(10, 100, 5, 50, false);

		// Act & Assert
		s1.ShouldBe(s2);
	}

	[Fact]
	public void CreateOrderingKeyState()
	{
		// Arrange
		var lastProcessed = DateTimeOffset.UtcNow;

		// Act
		var state = new OrderingKeyState("key-1", false, 5, lastProcessed);

		// Assert
		state.OrderingKey.ShouldBe("key-1");
		state.IsPaused.ShouldBeFalse();
		state.PendingCount.ShouldBe(5);
		state.LastProcessedTime.ShouldBe(lastProcessed);
		state.LastMessageId.ShouldBeNull();
	}

	[Fact]
	public void CreateOrderingKeyStateWithOptionalMessageId()
	{
		// Arrange
		var lastProcessed = DateTimeOffset.UtcNow;

		// Act
		var state = new OrderingKeyState("key-2", true, 0, lastProcessed, "msg-42");

		// Assert
		state.IsPaused.ShouldBeTrue();
		state.LastMessageId.ShouldBe("msg-42");
	}

	[Fact]
	public void CreateSchemaDefinition()
	{
		// Act
		var schema = new SchemaDefinition("schema-1", "AVRO", "{\"type\":\"record\"}");

		// Assert
		schema.SchemaId.ShouldBe("schema-1");
		schema.SchemaType.ShouldBe("AVRO");
		schema.Definition.ShouldBe("{\"type\":\"record\"}");
		schema.Metadata.ShouldBeNull();
	}

	[Fact]
	public void CreateSchemaDefinitionWithMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, string> { ["version"] = "2.0" };

		// Act
		var schema = new SchemaDefinition("s-1", "JSON", "{}", metadata);

		// Assert
		schema.Metadata.ShouldNotBeNull();
		schema.Metadata!["version"].ShouldBe("2.0");
	}

	[Fact]
	public void CreateRetryPolicyRecord()
	{
		// Act
		var policy = new Google.RetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), 2.0);

		// Assert
		policy.MaxAttempts.ShouldBe(3);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
		policy.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		policy.BackoffMultiplier.ShouldBe(2.0);
		policy.ExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void CreateRetryPolicyWithLinearBackoff()
	{
		// Act
		var policy = new Google.RetryPolicy(5, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(10), 1.0, false);

		// Assert
		policy.ExponentialBackoff.ShouldBeFalse();
	}

	[Fact]
	public void CreateSchemaMetadataWithDefaults()
	{
		// Act
		var metadata = new SchemaMetadata();

		// Assert
		metadata.TypeName.ShouldBe(string.Empty);
		metadata.Schema.ShouldBe(string.Empty);
		metadata.Version.ShouldBe(0);
		metadata.Format.ShouldBe(default);
		metadata.RegisteredAt.ShouldBe(default);
		metadata.Metadata.ShouldNotBeNull();
		metadata.Metadata.Count.ShouldBe(0);
	}

	[Fact]
	public void SetSchemaMetadataProperties()
	{
		// Arrange
		var registeredAt = DateTimeOffset.UtcNow;

		// Act
		var metadata = new SchemaMetadata
		{
			TypeName = "OrderCreatedEvent",
			Schema = "{\"type\":\"object\"}",
			Version = 3,
			Format = SerializationFormat.Json,
			RegisteredAt = registeredAt,
			Metadata = new Dictionary<string, string> { ["author"] = "system" },
		};

		// Assert
		metadata.TypeName.ShouldBe("OrderCreatedEvent");
		metadata.Schema.ShouldBe("{\"type\":\"object\"}");
		metadata.Version.ShouldBe(3);
		metadata.Format.ShouldBe(SerializationFormat.Json);
		metadata.RegisteredAt.ShouldBe(registeredAt);
		metadata.Metadata["author"].ShouldBe("system");
	}

	[Fact]
	public void CreateSerializationStatisticsWithDefaults()
	{
		// Act
		var stats = new SerializationStatistics();

		// Assert
		stats.ArrayPoolInUse.ShouldBe(0);
		stats.TotalMessagesSerialized.ShouldBe(0);
		stats.TotalBytesSerialized.ShouldBe(0);
		stats.AverageSerializationTimeMicros.ShouldBe(0);
	}

	[Fact]
	public void SetSerializationStatisticsProperties()
	{
		// Act
		var stats = new SerializationStatistics
		{
			ArrayPoolInUse = 65536,
			TotalMessagesSerialized = 10000,
			TotalBytesSerialized = 5_000_000,
			AverageSerializationTimeMicros = 15.7,
		};

		// Assert
		stats.ArrayPoolInUse.ShouldBe(65536);
		stats.TotalMessagesSerialized.ShouldBe(10000);
		stats.TotalBytesSerialized.ShouldBe(5_000_000);
		stats.AverageSerializationTimeMicros.ShouldBe(15.7);
	}
}
