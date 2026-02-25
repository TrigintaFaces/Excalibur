using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RabbitMqToKafkaMapperShould
{
	[Fact]
	public void HaveCorrectNameAndTransports()
	{
		var mapper = new RabbitMqToKafkaMapper();

		mapper.Name.ShouldBe("RabbitMqToKafka");
		mapper.SourceTransport.ShouldBe("rabbitmq");
		mapper.TargetTransport.ShouldBe("kafka");
	}

	[Fact]
	public void HaveHeaderConstants()
	{
		RabbitMqToKafkaMapper.PriorityHeader.ShouldBe("x-priority");
		RabbitMqToKafkaMapper.ExpirationHeader.ShouldBe("x-expiration");
		RabbitMqToKafkaMapper.ReplyToHeader.ShouldBe("x-reply-to");
	}

	[Fact]
	public void CanMap_RabbitMqToKafka()
	{
		var mapper = new RabbitMqToKafkaMapper();

		mapper.CanMap("rabbitmq", "kafka").ShouldBeTrue();
		mapper.CanMap("kafka", "rabbitmq").ShouldBeFalse();
	}

	[Fact]
	public void Map_CopiesCommonProperties()
	{
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			CorrelationId = "corr-1",
			ContentType = "application/json",
		};

		var result = mapper.Map(source, "kafka");

		result.MessageId.ShouldBe("msg-1");
		result.CorrelationId.ShouldBe("corr-1");
		result.ContentType.ShouldBe("application/json");
		result.TargetTransport.ShouldBe("kafka");
	}

	[Fact]
	public void Map_TranslatesRoutingKeyToKey()
	{
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1") { RoutingKey = "orders.created" };

		var result = mapper.Map(source, "kafka");

		result.ShouldBeOfType<KafkaMessageContext>();
		var kafka = (KafkaMessageContext)result;
		kafka.Key.ShouldBe("orders.created");
	}

	[Fact]
	public void Map_PreservesPriorityAsHeader()
	{
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1") { Priority = 5 };

		var result = mapper.Map(source, "kafka");

		result.Headers[RabbitMqToKafkaMapper.PriorityHeader].ShouldBe("5");
	}

	[Fact]
	public void Map_PreservesExpirationAsHeader()
	{
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1") { Expiration = "60000" };

		var result = mapper.Map(source, "kafka");

		result.Headers[RabbitMqToKafkaMapper.ExpirationHeader].ShouldBe("60000");
	}

	[Fact]
	public void Map_PreservesReplyToAsHeader()
	{
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1") { ReplyTo = "reply-queue" };

		var result = mapper.Map(source, "kafka");

		result.Headers[RabbitMqToKafkaMapper.ReplyToHeader].ShouldBe("reply-queue");
	}

	[Fact]
	public void Map_HandlesNullRoutingKeyGracefully()
	{
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1"); // RoutingKey is null

		var result = mapper.Map(source, "kafka");

		var kafka = (KafkaMessageContext)result;
		kafka.Key.ShouldBe(string.Empty);
	}

	[Fact]
	public void Map_SkipsPriorityHeaderWhenNull()
	{
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1"); // Priority is null

		var result = mapper.Map(source, "kafka");

		result.Headers.ContainsKey(RabbitMqToKafkaMapper.PriorityHeader).ShouldBeFalse();
	}
}
