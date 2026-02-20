// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SerializationModelsShould
{
	[Fact]
	public void CreateSchemaDefinitionRecord()
	{
		// Arrange & Act
		var schema = new SchemaDefinition("schema-1", "JSON", "{\"type\":\"object\"}");

		// Assert
		schema.SchemaId.ShouldBe("schema-1");
		schema.SchemaType.ShouldBe("JSON");
		schema.Definition.ShouldBe("{\"type\":\"object\"}");
		schema.Metadata.ShouldBeNull();
	}

	[Fact]
	public void CreateSchemaDefinitionWithMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, string> { ["version"] = "1.0" };

		// Act
		var schema = new SchemaDefinition("schema-1", "AVRO", "...", metadata);

		// Assert
		schema.Metadata.ShouldNotBeNull();
		schema.Metadata!["version"].ShouldBe("1.0");
	}

	[Fact]
	public void SupportSchemaDefinitionRecordEquality()
	{
		// Arrange
		var s1 = new SchemaDefinition("id", "JSON", "def");
		var s2 = new SchemaDefinition("id", "JSON", "def");

		// Assert
		s1.ShouldBe(s2);
	}

	[Fact]
	public void CreateSchemaMetadataWithDefaults()
	{
		// Arrange & Act
		var metadata = new SchemaMetadata();

		// Assert
		metadata.TypeName.ShouldBe(string.Empty);
		metadata.Schema.ShouldBe(string.Empty);
		metadata.Version.ShouldBe(0);
		metadata.Format.ShouldBe(SerializationFormat.Json);
		metadata.Metadata.ShouldNotBeNull();
		metadata.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingSchemaMetadataProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var metadata = new SchemaMetadata
		{
			TypeName = "OrderCreated",
			Schema = "{\"type\":\"object\"}",
			Version = 3,
			Format = SerializationFormat.MessagePack,
			RegisteredAt = now,
		};
		metadata.Metadata["author"] = "test";

		// Assert
		metadata.TypeName.ShouldBe("OrderCreated");
		metadata.Schema.ShouldBe("{\"type\":\"object\"}");
		metadata.Version.ShouldBe(3);
		metadata.Format.ShouldBe(SerializationFormat.MessagePack);
		metadata.RegisteredAt.ShouldBe(now);
		metadata.Metadata["author"].ShouldBe("test");
	}

	[Fact]
	public void CreateSerializationStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new SerializationStatistics();

		// Assert
		stats.ArrayPoolInUse.ShouldBe(0);
		stats.TotalMessagesSerialized.ShouldBe(0);
		stats.TotalBytesSerialized.ShouldBe(0);
		stats.AverageSerializationTimeMicros.ShouldBe(0.0);
	}

	[Fact]
	public void AllowSettingSerializationStatisticsProperties()
	{
		// Arrange & Act
		var stats = new SerializationStatistics
		{
			ArrayPoolInUse = 4096,
			TotalMessagesSerialized = 100000,
			TotalBytesSerialized = 500_000_000,
			AverageSerializationTimeMicros = 12.5,
		};

		// Assert
		stats.ArrayPoolInUse.ShouldBe(4096);
		stats.TotalMessagesSerialized.ShouldBe(100000);
		stats.TotalBytesSerialized.ShouldBe(500_000_000);
		stats.AverageSerializationTimeMicros.ShouldBe(12.5);
	}

	[Fact]
	public void CreateDeadLetterMetadataRecord()
	{
		// Arrange
		var firstFailure = DateTimeOffset.UtcNow.AddMinutes(-10);
		var lastFailure = DateTimeOffset.UtcNow;

		// Act
		var metadata = new DeadLetterMetadata(5, "MaxRetries", firstFailure, lastFailure, "projects/p/topics/t");

		// Assert
		metadata.DeliveryAttempts.ShouldBe(5);
		metadata.LastErrorReason.ShouldBe("MaxRetries");
		metadata.FirstFailureTime.ShouldBe(firstFailure);
		metadata.LastFailureTime.ShouldBe(lastFailure);
		metadata.OriginalTopic.ShouldBe("projects/p/topics/t");
	}

	[Fact]
	public void CreateDeadLetterMetadataWithDefaultTopic()
	{
		// Arrange & Act
		var now = DateTimeOffset.UtcNow;
		var metadata = new DeadLetterMetadata(1, "Error", now, now);

		// Assert
		metadata.OriginalTopic.ShouldBeNull();
	}

	[Fact]
	public void CreateFlowControlStateRecord()
	{
		// Arrange & Act
		var state = new FlowControlState(1000, 100_000_000, 500, 50_000_000, true);

		// Assert
		state.MaxOutstandingMessages.ShouldBe(1000);
		state.MaxOutstandingBytes.ShouldBe(100_000_000);
		state.CurrentOutstandingMessages.ShouldBe(500);
		state.CurrentOutstandingBytes.ShouldBe(50_000_000);
		state.IsFlowControlActive.ShouldBeTrue();
	}

	[Fact]
	public void SupportFlowControlStateRecordEquality()
	{
		// Arrange
		var s1 = new FlowControlState(100, 1000, 50, 500, false);
		var s2 = new FlowControlState(100, 1000, 50, 500, false);

		// Assert
		s1.ShouldBe(s2);
	}

	[Fact]
	public void CreateOrderingKeyStateRecord()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var state = new OrderingKeyState("key-1", false, 10, now, "msg-42");

		// Assert
		state.OrderingKey.ShouldBe("key-1");
		state.IsPaused.ShouldBeFalse();
		state.PendingCount.ShouldBe(10);
		state.LastProcessedTime.ShouldBe(now);
		state.LastMessageId.ShouldBe("msg-42");
	}

	[Fact]
	public void CreateOrderingKeyStateWithDefaults()
	{
		// Arrange & Act
		var state = new OrderingKeyState("key-2", true, 0, null);

		// Assert
		state.LastProcessedTime.ShouldBeNull();
		state.LastMessageId.ShouldBeNull();
	}
}
