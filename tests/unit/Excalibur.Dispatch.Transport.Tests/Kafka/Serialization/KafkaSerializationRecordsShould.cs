// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

using ConsumerGroupState = Excalibur.Dispatch.Transport.Kafka.ConsumerGroupState;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaSerializationRecordsShould
{
	[Fact]
	public void CreateBatchMetadataWithAllProperties()
	{
		// Arrange
		var receivedAt = DateTimeOffset.UtcNow;

		// Act
		var metadata = new BatchMetadata("test-topic", 2, 100, 200, 50, receivedAt);

		// Assert
		metadata.Topic.ShouldBe("test-topic");
		metadata.Partition.ShouldBe(2);
		metadata.FirstOffset.ShouldBe(100);
		metadata.LastOffset.ShouldBe(200);
		metadata.MessageCount.ShouldBe(50);
		metadata.ReceivedAt.ShouldBe(receivedAt);
	}

	[Fact]
	public void SupportBatchMetadataEquality()
	{
		// Arrange
		var receivedAt = DateTimeOffset.UtcNow;
		var metadata1 = new BatchMetadata("topic", 1, 0, 10, 5, receivedAt);
		var metadata2 = new BatchMetadata("topic", 1, 0, 10, 5, receivedAt);

		// Act & Assert
		metadata1.ShouldBe(metadata2);
		(metadata1 == metadata2).ShouldBeTrue();
	}

	[Fact]
	public void CreateConsumerGroupStateWithAllProperties()
	{
		// Arrange
		var members = new Collection<string> { "member-1", "member-2" };

		// Act
		var state = new ConsumerGroupState(
			"group-1",
			"Stable",
			members,
			"roundrobin",
			"consumer");

		// Assert
		state.GroupId.ShouldBe("group-1");
		state.State.ShouldBe("Stable");
		state.Members.Count.ShouldBe(2);
		state.Members.ShouldContain("member-1");
		state.Protocol.ShouldBe("roundrobin");
		state.ProtocolType.ShouldBe("consumer");
	}

	[Fact]
	public void SupportConsumerGroupStateEquality()
	{
		// Arrange
		var members = new Collection<string> { "m1" };
		var state1 = new ConsumerGroupState("g1", "Stable", members, "rr", "consumer");
		var state2 = new ConsumerGroupState("g1", "Stable", members, "rr", "consumer");

		// Act & Assert
		state1.ShouldBe(state2);
	}

	[Fact]
	public void CreateKafkaBatchWithMessagesAndMetadata()
	{
		// Arrange
		var receivedAt = DateTimeOffset.UtcNow;
		var metadata = new BatchMetadata("topic-1", 0, 0, 1, 2, receivedAt);
		var timestamp = new Timestamp(DateTimeOffset.UtcNow);
		var messages = new Collection<KafkaMessage>
		{
			new("topic-1", 0, 0, "key1", new byte[] { 1, 2, 3 }, null, timestamp),
			new("topic-1", 0, 1, "key2", new byte[] { 4, 5, 6 }, null, timestamp),
		};

		// Act
		var batch = new KafkaBatch(messages, metadata);

		// Assert
		batch.Messages.Count.ShouldBe(2);
		batch.Metadata.ShouldBe(metadata);
		batch.Messages[0].Key.ShouldBe("key1");
		batch.Messages[1].Key.ShouldBe("key2");
	}

	[Fact]
	public void CreateKafkaMessageWithAllProperties()
	{
		// Arrange
		var headers = new Dictionary<string, byte[]>
		{
			["header1"] = new byte[] { 10 },
		};
		var timestamp = new Timestamp(DateTimeOffset.UtcNow);

		// Act
		var message = new KafkaMessage("topic", 3, 42, "my-key", new byte[] { 1 }, headers, timestamp);

		// Assert
		message.Topic.ShouldBe("topic");
		message.Partition.ShouldBe(3);
		message.Offset.ShouldBe(42);
		message.Key.ShouldBe("my-key");
		message.Value.ShouldBe(new byte[] { 1 });
		message.Headers.ShouldNotBeNull();
		message.Headers!.Count.ShouldBe(1);
		message.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void CreateKafkaMessageWithNullKeyAndHeaders()
	{
		// Arrange
		var timestamp = new Timestamp(DateTimeOffset.UtcNow);

		// Act
		var message = new KafkaMessage("topic", 0, 0, null, Array.Empty<byte>(), null, timestamp);

		// Assert
		message.Key.ShouldBeNull();
		message.Headers.ShouldBeNull();
	}

	[Fact]
	public void CreatePartitionAssignmentWithAllProperties()
	{
		// Act
		var assignment = new PartitionAssignment("topic-a", 5, 1000, "consumer-1");

		// Assert
		assignment.Topic.ShouldBe("topic-a");
		assignment.Partition.ShouldBe(5);
		assignment.Offset.ShouldBe(1000);
		assignment.ConsumerId.ShouldBe("consumer-1");
	}

	[Fact]
	public void SupportPartitionAssignmentEquality()
	{
		// Arrange
		var assignment1 = new PartitionAssignment("t", 1, 100, "c1");
		var assignment2 = new PartitionAssignment("t", 1, 100, "c1");

		// Act & Assert
		assignment1.ShouldBe(assignment2);
	}
}
