using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KafkaToRabbitMqMapperShould
{
	[Fact]
	public void HaveCorrectNameAndTransports()
	{
		var mapper = new KafkaToRabbitMqMapper();

		mapper.Name.ShouldBe("KafkaToRabbitMq");
		mapper.SourceTransport.ShouldBe("kafka");
		mapper.TargetTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void HaveHeaderConstants()
	{
		KafkaToRabbitMqMapper.PriorityHeader.ShouldBe("x-priority");
		KafkaToRabbitMqMapper.ExpirationHeader.ShouldBe("x-expiration");
		KafkaToRabbitMqMapper.ReplyToHeader.ShouldBe("x-reply-to");
	}

	[Fact]
	public void CanMap_KafkaToRabbitMq()
	{
		var mapper = new KafkaToRabbitMqMapper();

		mapper.CanMap("kafka", "rabbitmq").ShouldBeTrue();
		mapper.CanMap("rabbitmq", "kafka").ShouldBeFalse();
	}

	[Fact]
	public void Map_CopiesCommonProperties()
	{
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1")
		{
			CorrelationId = "corr-1",
			ContentType = "application/json",
		};

		var result = mapper.Map(source, "rabbitmq");

		result.MessageId.ShouldBe("msg-1");
		result.CorrelationId.ShouldBe("corr-1");
		result.ContentType.ShouldBe("application/json");
		result.TargetTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void Map_TranslatesKeyToRoutingKey()
	{
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1") { Key = "order-123" };

		var result = mapper.Map(source, "rabbitmq");

		result.ShouldBeOfType<RabbitMqMessageContext>();
		var rmq = (RabbitMqMessageContext)result;
		rmq.RoutingKey.ShouldBe("order-123");
	}

	[Fact]
	public void Map_SetsPersistentDeliveryMode()
	{
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");

		var result = mapper.Map(source, "rabbitmq");

		var rmq = (RabbitMqMessageContext)result;
		rmq.DeliveryMode.ShouldBe((byte)2);
	}

	[Fact]
	public void Map_RestoresPriorityFromHeader()
	{
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "5");

		var result = mapper.Map(source, "rabbitmq");

		var rmq = (RabbitMqMessageContext)result;
		rmq.Priority.ShouldBe((byte)5);
	}

	[Fact]
	public void Map_RestoresExpirationFromHeader()
	{
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.ExpirationHeader, "60000");

		var result = mapper.Map(source, "rabbitmq");

		var rmq = (RabbitMqMessageContext)result;
		rmq.Expiration.ShouldBe("60000");
	}

	[Fact]
	public void Map_RestoresReplyToFromHeader()
	{
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.ReplyToHeader, "reply-queue");

		var result = mapper.Map(source, "rabbitmq");

		var rmq = (RabbitMqMessageContext)result;
		rmq.ReplyTo.ShouldBe("reply-queue");
	}

	[Fact]
	public void Map_HandlesNullKeyGracefully()
	{
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1"); // Key is null

		var result = mapper.Map(source, "rabbitmq");

		var rmq = (RabbitMqMessageContext)result;
		rmq.RoutingKey.ShouldBe(string.Empty);
	}
}
