// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

using KafkaConsumerGroupState = Excalibur.Dispatch.Transport.Kafka.ConsumerGroupState;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaSerializationModelsShould
{
	[Fact]
	public void CreateKafkaMessage()
	{
		// Arrange
		var headers = new Dictionary<string, byte[]> { ["key"] = [1, 2, 3] };
		var timestamp = new Timestamp(DateTime.UtcNow);

		// Act
		var message = new KafkaMessage("test-topic", 0, 42, "key1", [10, 20], headers, timestamp);

		// Assert
		message.Topic.ShouldBe("test-topic");
		message.Partition.ShouldBe(0);
		message.Offset.ShouldBe(42);
		message.Key.ShouldBe("key1");
		message.Value.ShouldBe([10, 20]);
		message.Headers.ShouldNotBeNull();
		message.Headers!["key"].ShouldBe([1, 2, 3]);
		message.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void CreateKafkaMessageWithNullKeyAndHeaders()
	{
		// Arrange & Act
		var message = new KafkaMessage("topic", 1, 0, null, [1], null, new Timestamp(DateTime.UtcNow));

		// Assert
		message.Key.ShouldBeNull();
		message.Headers.ShouldBeNull();
	}

	[Fact]
	public void SupportKafkaMessageRecordEquality()
	{
		// Arrange — share the same byte[] reference since record equality uses reference equality for arrays
		var timestamp = new Timestamp(DateTime.UtcNow);
		byte[] value = [1];
		var msg1 = new KafkaMessage("topic", 0, 1, "k", value, null, timestamp);
		var msg2 = new KafkaMessage("topic", 0, 1, "k", value, null, timestamp);

		// Assert
		msg1.ShouldBe(msg2);
	}

	[Fact]
	public void CreateBatchMetadata()
	{
		// Arrange
		var receivedAt = DateTimeOffset.UtcNow;

		// Act
		var metadata = new BatchMetadata("events", 2, 100, 199, 100, receivedAt);

		// Assert
		metadata.Topic.ShouldBe("events");
		metadata.Partition.ShouldBe(2);
		metadata.FirstOffset.ShouldBe(100);
		metadata.LastOffset.ShouldBe(199);
		metadata.MessageCount.ShouldBe(100);
		metadata.ReceivedAt.ShouldBe(receivedAt);
	}

	[Fact]
	public void CreateKafkaBatch()
	{
		// Arrange
		var messages = new Collection<KafkaMessage>
		{
			new("topic", 0, 1, "k1", [1], null, new Timestamp(DateTime.UtcNow)),
			new("topic", 0, 2, "k2", [2], null, new Timestamp(DateTime.UtcNow)),
		};
		var metadata = new BatchMetadata("topic", 0, 1, 2, 2, DateTimeOffset.UtcNow);

		// Act
		var batch = new KafkaBatch(messages, metadata);

		// Assert
		batch.Messages.Count.ShouldBe(2);
		batch.Metadata.ShouldBe(metadata);
	}

	[Fact]
	public void CreateConsumerGroupState()
	{
		// Arrange
		var members = new Collection<string> { "member-1", "member-2" };

		// Act
		var state = new KafkaConsumerGroupState("my-group", "Stable", members, "range", "consumer");

		// Assert
		state.GroupId.ShouldBe("my-group");
		state.State.ShouldBe("Stable");
		state.Members.Count.ShouldBe(2);
		state.Protocol.ShouldBe("range");
		state.ProtocolType.ShouldBe("consumer");
	}

	[Fact]
	public void CreatePartitionAssignment()
	{
		// Arrange & Act
		var assignment = new PartitionAssignment("events", 3, 500, "consumer-1");

		// Assert
		assignment.Topic.ShouldBe("events");
		assignment.Partition.ShouldBe(3);
		assignment.Offset.ShouldBe(500);
		assignment.ConsumerId.ShouldBe("consumer-1");
	}

	[Fact]
	public void SupportPartitionAssignmentRecordEquality()
	{
		// Arrange
		var a1 = new PartitionAssignment("t", 0, 1, "c");
		var a2 = new PartitionAssignment("t", 0, 1, "c");
		var a3 = new PartitionAssignment("t", 0, 1, "d");

		// Assert
		a1.ShouldBe(a2);
		a1.ShouldNotBe(a3);
	}
}
