using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MappingContextsShould
{
	// --- KafkaMappingContext ---

	[Fact]
	public void KafkaContext_SetAndGetProperties()
	{
		var ctx = new KafkaMappingContext
		{
			Topic = "my-topic",
			Key = "order-123",
			Partition = 3,
			SchemaId = 42,
		};

		ctx.Topic.ShouldBe("my-topic");
		ctx.Key.ShouldBe("order-123");
		ctx.Partition.ShouldBe(3);
		ctx.SchemaId.ShouldBe(42);
	}

	[Fact]
	public void KafkaContext_SetHeader()
	{
		var ctx = new KafkaMappingContext();
		ctx.SetHeader("X-Custom", "value");

		ctx.Headers["X-Custom"].ShouldBe("value");
	}

	[Fact]
	public void KafkaContext_ThrowOnNullHeaderKey()
	{
		var ctx = new KafkaMappingContext();
		Should.Throw<ArgumentException>(() => ctx.SetHeader(null!, "value"));
	}

	[Fact]
	public void KafkaContext_ApplyTo()
	{
		var ctx = new KafkaMappingContext
		{
			Topic = "orders",
			Key = "k1",
			Partition = 2,
		};
		ctx.SetHeader("X-H", "v");
		var target = new KafkaMessageContext("msg-1");

		ctx.ApplyTo(target);

		target.Topic.ShouldBe("orders");
		target.Key.ShouldBe("k1");
		target.Partition.ShouldBe(2);
		target.Headers["X-H"].ShouldBe("v");
	}

	[Fact]
	public void KafkaContext_ApplyToThrowsOnNull()
	{
		var ctx = new KafkaMappingContext();
		Should.Throw<ArgumentNullException>(() => ctx.ApplyTo(null!));
	}

	// --- RabbitMqMappingContext ---

	[Fact]
	public void RabbitMqContext_SetAndGetProperties()
	{
		var ctx = new RabbitMqMappingContext
		{
			Exchange = "my-exchange",
			RoutingKey = "orders.created",
			Priority = 5,
			ReplyTo = "reply-q",
			Expiration = "60000",
			DeliveryMode = 2,
		};

		ctx.Exchange.ShouldBe("my-exchange");
		ctx.RoutingKey.ShouldBe("orders.created");
		ctx.Priority.ShouldBe((byte)5);
		ctx.ReplyTo.ShouldBe("reply-q");
		ctx.Expiration.ShouldBe("60000");
		ctx.DeliveryMode.ShouldBe((byte)2);
	}

	[Fact]
	public void RabbitMqContext_SetHeader()
	{
		var ctx = new RabbitMqMappingContext();
		ctx.SetHeader("X-Custom", "value");

		ctx.Headers["X-Custom"].ShouldBe("value");
	}

	[Fact]
	public void RabbitMqContext_ThrowOnNullHeaderKey()
	{
		var ctx = new RabbitMqMappingContext();
		Should.Throw<ArgumentException>(() => ctx.SetHeader(null!, "value"));
	}

	[Fact]
	public void RabbitMqContext_ApplyTo()
	{
		var ctx = new RabbitMqMappingContext
		{
			Exchange = "ex1",
			RoutingKey = "rk1",
			Priority = 3,
			ReplyTo = "rq1",
			Expiration = "30000",
			DeliveryMode = 1,
		};
		ctx.SetHeader("X-H", "v");
		var target = new RabbitMqMessageContext("msg-1");

		ctx.ApplyTo(target);

		target.Exchange.ShouldBe("ex1");
		target.RoutingKey.ShouldBe("rk1");
		target.Priority.ShouldBe((byte)3);
		target.ReplyTo.ShouldBe("rq1");
		target.Expiration.ShouldBe("30000");
		target.DeliveryMode.ShouldBe((byte)1);
		target.Headers["X-H"].ShouldBe("v");
	}

	[Fact]
	public void RabbitMqContext_ApplyToThrowsOnNull()
	{
		var ctx = new RabbitMqMappingContext();
		Should.Throw<ArgumentNullException>(() => ctx.ApplyTo(null!));
	}

	[Fact]
	public void RabbitMqContext_ApplyToSkipsNullProperties()
	{
		var ctx = new RabbitMqMappingContext(); // all null
		var target = new RabbitMqMessageContext("msg-1");
		target.Exchange = "original";

		ctx.ApplyTo(target);

		target.Exchange.ShouldBe("original"); // not overwritten
	}

	// --- AzureServiceBusMappingContext ---

	[Fact]
	public void AzureContext_SetAndGetProperties()
	{
		var ctx = new AzureServiceBusMappingContext
		{
			TopicOrQueueName = "my-topic",
			SessionId = "session-1",
			PartitionKey = "pk",
			ReplyToSessionId = "reply-session",
			TimeToLive = TimeSpan.FromMinutes(5),
			ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddMinutes(10),
		};

		ctx.TopicOrQueueName.ShouldBe("my-topic");
		ctx.SessionId.ShouldBe("session-1");
		ctx.PartitionKey.ShouldBe("pk");
		ctx.ReplyToSessionId.ShouldBe("reply-session");
		ctx.TimeToLive.ShouldBe(TimeSpan.FromMinutes(5));
		ctx.ScheduledEnqueueTime.ShouldNotBeNull();
	}

	[Fact]
	public void AzureContext_SetProperty()
	{
		var ctx = new AzureServiceBusMappingContext();
		ctx.SetProperty("custom", "value");

		ctx.Properties["custom"].ShouldBe("value");
	}

	[Fact]
	public void AzureContext_ThrowOnNullPropertyKey()
	{
		var ctx = new AzureServiceBusMappingContext();
		Should.Throw<ArgumentException>(() => ctx.SetProperty(null!, "value"));
	}

	[Fact]
	public void AzureContext_ApplyTo()
	{
		var ctx = new AzureServiceBusMappingContext();
		ctx.SetProperty("custom", "value");
		var target = new TransportMessageContext("msg-1");

		ctx.ApplyTo(target);

		target.GetTransportProperty<object>("custom").ShouldBe("value");
	}

	[Fact]
	public void AzureContext_ApplyToThrowsOnNull()
	{
		var ctx = new AzureServiceBusMappingContext();
		Should.Throw<ArgumentNullException>(() => ctx.ApplyTo(null!));
	}

	// --- AwsSqsMappingContext ---

	[Fact]
	public void AwsSqsContext_SetAndGetProperties()
	{
		var ctx = new AwsSqsMappingContext
		{
			QueueUrl = "https://sqs.us-east-1.amazonaws.com/123/queue",
			MessageGroupId = "group-1",
			MessageDeduplicationId = "dedup-1",
			DelaySeconds = 30,
		};

		ctx.QueueUrl.ShouldBe("https://sqs.us-east-1.amazonaws.com/123/queue");
		ctx.MessageGroupId.ShouldBe("group-1");
		ctx.MessageDeduplicationId.ShouldBe("dedup-1");
		ctx.DelaySeconds.ShouldBe(30);
	}

	[Fact]
	public void AwsSqsContext_SetAttribute()
	{
		var ctx = new AwsSqsMappingContext();
		ctx.SetAttribute("my-attr", "val", "Number");

		ctx.Attributes["my-attr"].Value.ShouldBe("val");
		ctx.Attributes["my-attr"].DataType.ShouldBe("Number");
	}

	[Fact]
	public void AwsSqsContext_SetAttributeDefaultDataType()
	{
		var ctx = new AwsSqsMappingContext();
		ctx.SetAttribute("my-attr", "val");

		ctx.Attributes["my-attr"].DataType.ShouldBe("String");
	}

	[Fact]
	public void AwsSqsContext_ApplyTo()
	{
		var ctx = new AwsSqsMappingContext();
		ctx.SetAttribute("OrderId", "123");
		var target = new TransportMessageContext("msg-1");

		ctx.ApplyTo(target);

		target.GetTransportProperty<string>("aws.sqs.OrderId").ShouldBe("123");
	}

	[Fact]
	public void AwsSqsContext_ApplyToThrowsOnNull()
	{
		var ctx = new AwsSqsMappingContext();
		Should.Throw<ArgumentNullException>(() => ctx.ApplyTo(null!));
	}

	// --- AwsSnsMappingContext ---

	[Fact]
	public void AwsSnsContext_SetAndGetProperties()
	{
		var ctx = new AwsSnsMappingContext
		{
			TopicArn = "arn:aws:sns:us-east-1:123:topic",
			MessageGroupId = "group-1",
			MessageDeduplicationId = "dedup-1",
			Subject = "Test Subject",
		};

		ctx.TopicArn.ShouldBe("arn:aws:sns:us-east-1:123:topic");
		ctx.MessageGroupId.ShouldBe("group-1");
		ctx.MessageDeduplicationId.ShouldBe("dedup-1");
		ctx.Subject.ShouldBe("Test Subject");
	}

	[Fact]
	public void AwsSnsContext_SetAttribute()
	{
		var ctx = new AwsSnsMappingContext();
		ctx.SetAttribute("my-attr", "val", "Number");

		ctx.Attributes["my-attr"].Value.ShouldBe("val");
		ctx.Attributes["my-attr"].DataType.ShouldBe("Number");
	}

	[Fact]
	public void AwsSnsContext_ApplyTo()
	{
		var ctx = new AwsSnsMappingContext();
		ctx.SetAttribute("EventType", "OrderCreated");
		var target = new TransportMessageContext("msg-1");

		ctx.ApplyTo(target);

		target.GetTransportProperty<string>("aws.sns.EventType").ShouldBe("OrderCreated");
	}

	[Fact]
	public void AwsSnsContext_ApplyToThrowsOnNull()
	{
		var ctx = new AwsSnsMappingContext();
		Should.Throw<ArgumentNullException>(() => ctx.ApplyTo(null!));
	}

	// --- GooglePubSubMappingContext ---

	[Fact]
	public void GoogleContext_SetAndGetProperties()
	{
		var ctx = new GooglePubSubMappingContext
		{
			TopicName = "projects/my-proj/topics/my-topic",
			OrderingKey = "order-123",
		};

		ctx.TopicName.ShouldBe("projects/my-proj/topics/my-topic");
		ctx.OrderingKey.ShouldBe("order-123");
	}

	[Fact]
	public void GoogleContext_SetAttribute()
	{
		var ctx = new GooglePubSubMappingContext();
		ctx.SetAttribute("eventType", "OrderCreated");

		ctx.Attributes["eventType"].ShouldBe("OrderCreated");
	}

	[Fact]
	public void GoogleContext_ApplyTo()
	{
		var ctx = new GooglePubSubMappingContext();
		ctx.SetAttribute("region", "us-central1");
		var target = new TransportMessageContext("msg-1");

		ctx.ApplyTo(target);

		target.GetTransportProperty<string>("gcp.pubsub.region").ShouldBe("us-central1");
	}

	[Fact]
	public void GoogleContext_ApplyToThrowsOnNull()
	{
		var ctx = new GooglePubSubMappingContext();
		Should.Throw<ArgumentNullException>(() => ctx.ApplyTo(null!));
	}

	// --- GrpcMappingContext ---

	[Fact]
	public void GrpcContext_SetAndGetProperties()
	{
		var ctx = new GrpcMappingContext
		{
			MethodName = "SayHello",
			Deadline = TimeSpan.FromSeconds(30),
		};

		ctx.MethodName.ShouldBe("SayHello");
		ctx.Deadline.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void GrpcContext_SetHeader()
	{
		var ctx = new GrpcMappingContext();
		ctx.SetHeader("authorization", "Bearer token");

		ctx.Headers["authorization"].ShouldBe("Bearer token");
	}

	[Fact]
	public void GrpcContext_ThrowOnNullHeaderKey()
	{
		var ctx = new GrpcMappingContext();
		Should.Throw<ArgumentException>(() => ctx.SetHeader(null!, "value"));
	}

	[Fact]
	public void GrpcContext_ApplyTo()
	{
		var ctx = new GrpcMappingContext();
		ctx.SetHeader("X-Custom", "value");
		var target = new TransportMessageContext("msg-1");

		ctx.ApplyTo(target);

		target.GetTransportProperty<string>("grpc.X-Custom").ShouldBe("value");
	}

	[Fact]
	public void GrpcContext_ApplyToThrowsOnNull()
	{
		var ctx = new GrpcMappingContext();
		Should.Throw<ArgumentNullException>(() => ctx.ApplyTo(null!));
	}
}
