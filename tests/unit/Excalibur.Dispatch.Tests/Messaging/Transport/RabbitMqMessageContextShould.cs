using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RabbitMqMessageContextShould
{
	[Fact]
	public void CreateWithMessageId()
	{
		var ctx = new RabbitMqMessageContext("msg-1");

		ctx.MessageId.ShouldBe("msg-1");
		ctx.SourceTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void CreateWithGeneratedMessageId()
	{
		var ctx = new RabbitMqMessageContext();

		ctx.MessageId.ShouldNotBeNullOrWhiteSpace();
		ctx.SourceTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void HavePropertyNameConstants()
	{
		RabbitMqMessageContext.ExchangePropertyName.ShouldBe("Exchange");
		RabbitMqMessageContext.RoutingKeyPropertyName.ShouldBe("RoutingKey");
		RabbitMqMessageContext.DeliveryTagPropertyName.ShouldBe("DeliveryTag");
		RabbitMqMessageContext.PriorityPropertyName.ShouldBe("Priority");
		RabbitMqMessageContext.ReplyToPropertyName.ShouldBe("ReplyTo");
		RabbitMqMessageContext.ExpirationPropertyName.ShouldBe("Expiration");
		RabbitMqMessageContext.DeliveryModePropertyName.ShouldBe("DeliveryMode");
		RabbitMqMessageContext.RedeliveredPropertyName.ShouldBe("Redelivered");
	}

	[Fact]
	public void SetAndGetExchange()
	{
		var ctx = new RabbitMqMessageContext("msg-1");

		ctx.Exchange = "my-exchange";

		ctx.Exchange.ShouldBe("my-exchange");
	}

	[Fact]
	public void SetAndGetRoutingKey()
	{
		var ctx = new RabbitMqMessageContext("msg-1");

		ctx.RoutingKey = "orders.created";

		ctx.RoutingKey.ShouldBe("orders.created");
	}

	[Fact]
	public void SetAndGetDeliveryTag()
	{
		var ctx = new RabbitMqMessageContext("msg-1");

		ctx.DeliveryTag = 12345UL;

		ctx.DeliveryTag.ShouldBe(12345UL);
	}

	[Fact]
	public void SetAndGetPriority()
	{
		var ctx = new RabbitMqMessageContext("msg-1");

		ctx.Priority = 5;

		ctx.Priority.ShouldBe((byte)5);
	}

	[Fact]
	public void SetAndGetReplyTo()
	{
		var ctx = new RabbitMqMessageContext("msg-1");

		ctx.ReplyTo = "reply-queue";

		ctx.ReplyTo.ShouldBe("reply-queue");
	}

	[Fact]
	public void SetAndGetExpiration()
	{
		var ctx = new RabbitMqMessageContext("msg-1");

		ctx.Expiration = "60000";

		ctx.Expiration.ShouldBe("60000");
	}

	[Fact]
	public void SetAndGetDeliveryMode()
	{
		var ctx = new RabbitMqMessageContext("msg-1");

		ctx.DeliveryMode = 2;

		ctx.DeliveryMode.ShouldBe((byte)2);
	}

	[Fact]
	public void SetAndGetRedelivered()
	{
		var ctx = new RabbitMqMessageContext("msg-1");

		ctx.Redelivered = true;

		ctx.Redelivered.ShouldBeTrue();
	}

	[Fact]
	public void ReturnDefaultForUnsetProperties()
	{
		var ctx = new RabbitMqMessageContext("msg-1");

		ctx.Exchange.ShouldBeNull();
		ctx.RoutingKey.ShouldBeNull();
		ctx.DeliveryTag.ShouldBe(0UL);
		ctx.Priority.ShouldBeNull();
		ctx.ReplyTo.ShouldBeNull();
		ctx.Expiration.ShouldBeNull();
		ctx.DeliveryMode.ShouldBe((byte)0);
		ctx.Redelivered.ShouldBeFalse();
	}
}
