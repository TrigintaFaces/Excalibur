using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KafkaMessageContextShould
{
	[Fact]
	public void CreateWithMessageId()
	{
		var ctx = new KafkaMessageContext("msg-1");

		ctx.MessageId.ShouldBe("msg-1");
		ctx.SourceTransport.ShouldBe("kafka");
	}

	[Fact]
	public void CreateWithGeneratedMessageId()
	{
		var ctx = new KafkaMessageContext();

		ctx.MessageId.ShouldNotBeNullOrWhiteSpace();
		ctx.SourceTransport.ShouldBe("kafka");
	}

	[Fact]
	public void HavePropertyNameConstants()
	{
		KafkaMessageContext.TopicPropertyName.ShouldBe("Topic");
		KafkaMessageContext.PartitionPropertyName.ShouldBe("Partition");
		KafkaMessageContext.OffsetPropertyName.ShouldBe("Offset");
		KafkaMessageContext.KeyPropertyName.ShouldBe("Key");
		KafkaMessageContext.LeaderEpochPropertyName.ShouldBe("LeaderEpoch");
		KafkaMessageContext.SchemaIdPropertyName.ShouldBe("SchemaId");
	}

	[Fact]
	public void SetAndGetTopic()
	{
		var ctx = new KafkaMessageContext("msg-1");

		ctx.Topic = "my-topic";

		ctx.Topic.ShouldBe("my-topic");
	}

	[Fact]
	public void SetAndGetPartition()
	{
		var ctx = new KafkaMessageContext("msg-1");

		ctx.Partition = 3;

		ctx.Partition.ShouldBe(3);
	}

	[Fact]
	public void SetAndGetOffset()
	{
		var ctx = new KafkaMessageContext("msg-1");

		ctx.Offset = 42L;

		ctx.Offset.ShouldBe(42L);
	}

	[Fact]
	public void SetAndGetKey()
	{
		var ctx = new KafkaMessageContext("msg-1");

		ctx.Key = "order-123";

		ctx.Key.ShouldBe("order-123");
	}

	[Fact]
	public void SetAndGetLeaderEpoch()
	{
		var ctx = new KafkaMessageContext("msg-1");

		ctx.LeaderEpoch = 5;

		ctx.LeaderEpoch.ShouldBe(5);
	}

	[Fact]
	public void SetAndGetSchemaId()
	{
		var ctx = new KafkaMessageContext("msg-1");

		ctx.SchemaId = 100;

		ctx.SchemaId.ShouldBe(100);
	}

	[Fact]
	public void ReturnDefaultForUnsetProperties()
	{
		var ctx = new KafkaMessageContext("msg-1");

		ctx.Topic.ShouldBeNull();
		ctx.Partition.ShouldBe(0);
		ctx.Offset.ShouldBe(0L);
		ctx.Key.ShouldBeNull();
		ctx.LeaderEpoch.ShouldBeNull();
		ctx.SchemaId.ShouldBeNull();
	}
}
