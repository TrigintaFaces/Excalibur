// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ConfiguredMessageMapperShould
{
	[Fact]
	public void ExposeConfiguredMapperIdentity_AndMapAnyTransport()
	{
		var mapper = CreateMapper();

		mapper.Name.ShouldBe("ConfiguredMapper");
		mapper.SourceTransport.ShouldBe(DefaultMessageMapper.WildcardTransport);
		mapper.TargetTransport.ShouldBe(DefaultMessageMapper.WildcardTransport);
		mapper.CanMap("rabbitmq", "kafka").ShouldBeTrue();
	}

	[Fact]
	public void Map_ThrowForInvalidArguments()
	{
		var mapper = CreateMapper();

		Should.Throw<ArgumentNullException>(() => mapper.Map(null!, "kafka"));
		Should.Throw<ArgumentException>(() => mapper.Map(new TransportMessageContext("msg-1"), string.Empty));
	}

	[Theory]
	[InlineData("rabbitmq", typeof(RabbitMqMessageContext))]
	[InlineData("kafka", typeof(KafkaMessageContext))]
	[InlineData("custom", typeof(TransportMessageContext))]
	public void Map_CreateExpectedTargetType_AndCopyCommonProperties(string targetTransport, Type expectedType)
	{
		var mapper = CreateMapper();
		var source = new TransportMessageContext("msg-42")
		{
			CorrelationId = "corr-1",
			CausationId = "cause-1",
			SourceTransport = "source-a",
			Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
			ContentType = "application/json",
		};
		source.SetHeader("h1", "v1");
		source.SetTransportProperty("p1", 123);

		var result = mapper.Map(source, targetTransport);

		result.GetType().ShouldBe(expectedType);
		result.MessageId.ShouldBe("msg-42");
		result.CorrelationId.ShouldBe("corr-1");
		result.CausationId.ShouldBe("cause-1");
		result.SourceTransport.ShouldBe("source-a");
		result.TargetTransport.ShouldBe(targetTransport);
		result.ContentType.ShouldBe("application/json");
		result.Headers["h1"].ShouldBe("v1");
		result.GetTransportProperty<int>("p1").ShouldBe(123);
	}

	[Fact]
	public void Map_UseRegisteredMapper_ThenApplyDefaultConfiguration()
	{
		var delegatedResult = new RabbitMqMessageContext("delegated-msg");
		var delegatedMapper = new DelegatingMapper(
			"delegating",
			"amqp",
			"rabbitmq",
			_ => delegatedResult);

		var mapper = CreateMapper(
			builder => builder.ConfigureDefaults(defaults => defaults.ForRabbitMq(rmq =>
			{
				rmq.Exchange = "default-exchange";
				rmq.SetHeader("mapped-by", "defaults");
			})),
			delegatedMapper);

		var source = new TransportMessageContext("source-msg") { SourceTransport = "amqp" };

		var result = mapper.Map(source, "rabbitmq").ShouldBeOfType<RabbitMqMessageContext>();

		delegatedMapper.CallCount.ShouldBe(1);
		result.MessageId.ShouldBe("delegated-msg");
		result.Exchange.ShouldBe("default-exchange");
		result.Headers["mapped-by"].ShouldBe("defaults");
	}

	[Theory]
	[InlineData("TestMappedMessage")]
	[InlineData("Excalibur.Dispatch.Tests.Messaging.Transport.ConfiguredMessageMapperShould+TestMappedMessage")]
	public void Map_ApplyTypeConfiguration_WhenHeaderContainsMessageType(string typeHeader)
	{
		var mapper = CreateMapper(builder =>
		{
			_ = builder.MapMessage<TestMappedMessage>()
				.ToRabbitMq(rmq => rmq.RoutingKey = "typed-route")
				.And();
		});

		var source = new TransportMessageContext("msg-1");
		source.SetHeader("X-Message-Type", typeHeader);

		var result = mapper.Map(source, "rabbitmq").ShouldBeOfType<RabbitMqMessageContext>();

		result.RoutingKey.ShouldBe("typed-route");
	}

	[Fact]
	public void Map_ApplyDefaultConfiguration_WhenHeaderTypeHasNoMapping()
	{
		var mapper = CreateMapper(builder =>
		{
			builder.ConfigureDefaults(defaults => defaults.ForRabbitMq(rmq =>
			{
				rmq.Exchange = "fallback-ex";
			}));
		});

		var source = new TransportMessageContext("msg-1");
		source.SetHeader("X-Message-Type", "UnknownMessageType");

		var result = mapper.Map(source, "rabbitmq").ShouldBeOfType<RabbitMqMessageContext>();

		result.Exchange.ShouldBe("fallback-ex");
	}

	[Fact]
	public void MapOfT_ApplyRabbitMqTypeSpecificConfiguration()
	{
		var mapper = CreateMapper(builder =>
		{
			_ = builder.MapMessage<TestMappedMessage>()
				.ToRabbitMq(rmq =>
				{
					rmq.Exchange = "orders";
					rmq.RoutingKey = "order.created";
					rmq.Priority = 7;
					rmq.SetHeader("x-type", "typed");
				})
				.And();
		});

		var source = new TransportMessageContext("msg-1");

		var result = mapper.Map<TestMappedMessage>(source, "rabbitmq").ShouldBeOfType<RabbitMqMessageContext>();

		result.Exchange.ShouldBe("orders");
		result.RoutingKey.ShouldBe("order.created");
		result.Priority.ShouldBe((byte)7);
		result.Headers["x-type"].ShouldBe("typed");
	}

	[Fact]
	public void MapOfT_ApplyKafkaTypeSpecificConfiguration()
	{
		var mapper = CreateMapper(builder =>
		{
			_ = builder.MapMessage<TestMappedMessage>()
				.ToKafka(kafka =>
				{
					kafka.Topic = "dispatch-events";
					kafka.Key = "k1";
					kafka.Partition = 3;
					kafka.SetHeader("k-header", "k-value");
				})
				.And();
		});

		var source = new TransportMessageContext("msg-1");

		var result = mapper.Map<TestMappedMessage>(source, "kafka").ShouldBeOfType<KafkaMessageContext>();

		result.Topic.ShouldBe("dispatch-events");
		result.Key.ShouldBe("k1");
		result.Partition.ShouldBe(3);
		result.Headers["k-header"].ShouldBe("k-value");
	}

	[Fact]
	public void MapOfT_ApplyAzureServiceBusTypeSpecificConfiguration()
	{
		var mapper = CreateMapper(builder =>
		{
			_ = builder.MapMessage<TestMappedMessage>()
				.ToAzureServiceBus(asb =>
				{
					asb.SetProperty("asb.prop", "v1");
				})
				.And();
		});

		var source = new TransportMessageContext("msg-1");

		var result = mapper.Map<TestMappedMessage>(source, "azureservicebus").ShouldBeOfType<TransportMessageContext>();

		result.GetTransportProperty<string>("asb.prop").ShouldBe("v1");
	}

	[Theory]
	[InlineData("sqs", "aws.sqs.priority")]
	[InlineData("awssqs", "aws.sqs.priority")]
	public void MapOfT_ApplyAwsSqsTypeSpecificConfiguration_ForAliases(string targetTransport, string expectedProperty)
	{
		var mapper = CreateMapper(builder =>
		{
			_ = builder.MapMessage<TestMappedMessage>()
				.ToAwsSqs(sqs =>
				{
					sqs.SetAttribute("priority", "high");
				})
				.And();
		});

		var source = new TransportMessageContext("msg-1");

		var result = mapper.Map<TestMappedMessage>(source, targetTransport).ShouldBeOfType<TransportMessageContext>();

		result.GetTransportProperty<string>(expectedProperty).ShouldBe("high");
	}

	[Theory]
	[InlineData("sns", "aws.sns.subject")]
	[InlineData("awssns", "aws.sns.subject")]
	public void MapOfT_ApplyAwsSnsTypeSpecificConfiguration_ForAliases(string targetTransport, string expectedProperty)
	{
		var mapper = CreateMapper(builder =>
		{
			_ = builder.MapMessage<TestMappedMessage>()
				.ToAwsSns(sns =>
				{
					sns.SetAttribute("subject", "subject-1");
				})
				.And();
		});

		var source = new TransportMessageContext("msg-1");

		var result = mapper.Map<TestMappedMessage>(source, targetTransport).ShouldBeOfType<TransportMessageContext>();

		result.GetTransportProperty<string>(expectedProperty).ShouldBe("subject-1");
	}

	[Theory]
	[InlineData("pubsub", "gcp.pubsub.ordering-key")]
	[InlineData("googlepubsub", "gcp.pubsub.ordering-key")]
	public void MapOfT_ApplyGooglePubSubTypeSpecificConfiguration_ForAliases(string targetTransport, string expectedProperty)
	{
		var mapper = CreateMapper(builder =>
		{
			_ = builder.MapMessage<TestMappedMessage>()
				.ToGooglePubSub(pubSub =>
				{
					pubSub.SetAttribute("ordering-key", "ok-1");
				})
				.And();
		});

		var source = new TransportMessageContext("msg-1");

		var result = mapper.Map<TestMappedMessage>(source, targetTransport).ShouldBeOfType<TransportMessageContext>();

		result.GetTransportProperty<string>(expectedProperty).ShouldBe("ok-1");
	}

	[Fact]
	public void MapOfT_ApplyGrpcTypeSpecificConfiguration()
	{
		var mapper = CreateMapper(builder =>
		{
			_ = builder.MapMessage<TestMappedMessage>()
				.ToGrpc(grpc =>
				{
					grpc.SetHeader("method", "DispatchService/Publish");
				})
				.And();
		});

		var source = new TransportMessageContext("msg-1");

		var result = mapper.Map<TestMappedMessage>(source, "grpc").ShouldBeOfType<TransportMessageContext>();

		result.GetTransportProperty<string>("grpc.method").ShouldBe("DispatchService/Publish");
	}

	[Fact]
	public void MapOfT_ApplyCustomTransportTypeSpecificConfiguration()
	{
		var mapper = CreateMapper(builder =>
		{
			_ = builder.MapMessage<TestMappedMessage>()
				.ToTransport("my-bus", ctx =>
				{
					ctx.SetTransportProperty("custom.tag", "custom-v");
				})
				.And();
		});

		var source = new TransportMessageContext("msg-1");

		var result = mapper.Map<TestMappedMessage>(source, "my-bus").ShouldBeOfType<TransportMessageContext>();

		result.GetTransportProperty<string>("custom.tag").ShouldBe("custom-v");
	}

	[Theory]
	[InlineData("kafka")]
	[InlineData("azureservicebus")]
	[InlineData("sqs")]
	[InlineData("pubsub")]
	public void Map_ApplyDefaultConfiguration_ForSupportedTargetTransports(string targetTransport)
	{
		var mapper = CreateMapper(builder =>
		{
			builder.ConfigureDefaults(defaults =>
			{
				if (string.Equals(targetTransport, "kafka", StringComparison.OrdinalIgnoreCase))
				{
					defaults.ForKafka(kafka => kafka.SetHeader("default-k", "1"));
				}
				else if (string.Equals(targetTransport, "azureservicebus", StringComparison.OrdinalIgnoreCase))
				{
					defaults.ForAzureServiceBus(asb => asb.SetProperty("default-asb", "1"));
				}
				else if (string.Equals(targetTransport, "sqs", StringComparison.OrdinalIgnoreCase))
				{
					defaults.ForAwsSqs(sqs => sqs.SetAttribute("default-sqs", "1"));
				}
				else if (string.Equals(targetTransport, "pubsub", StringComparison.OrdinalIgnoreCase))
				{
					defaults.ForGooglePubSub(pubsub => pubsub.SetAttribute("default-ps", "1"));
				}
			});
		});

		var source = new TransportMessageContext("msg-1");

		var result = mapper.Map(source, targetTransport);

		if (string.Equals(targetTransport, "kafka", StringComparison.OrdinalIgnoreCase))
		{
			result.ShouldBeOfType<KafkaMessageContext>().Headers["default-k"].ShouldBe("1");
		}
		else if (string.Equals(targetTransport, "azureservicebus", StringComparison.OrdinalIgnoreCase))
		{
			result.ShouldBeOfType<TransportMessageContext>().GetTransportProperty<string>("default-asb").ShouldBe("1");
		}
		else if (string.Equals(targetTransport, "sqs", StringComparison.OrdinalIgnoreCase))
		{
			result.ShouldBeOfType<TransportMessageContext>().GetTransportProperty<string>("aws.sqs.default-sqs").ShouldBe("1");
		}
		else if (string.Equals(targetTransport, "pubsub", StringComparison.OrdinalIgnoreCase))
		{
			result.ShouldBeOfType<TransportMessageContext>().GetTransportProperty<string>("gcp.pubsub.default-ps").ShouldBe("1");
		}
	}

	private static ConfiguredMessageMapper CreateMapper(
		Action<MessageMappingBuilder>? configure = null,
		IMessageMapper? registeredMapper = null)
	{
		var services = new ServiceCollection();
		var registry = new MessageMapperRegistry();
		if (registeredMapper is not null)
		{
			registry.Register(registeredMapper);
		}

		var builder = new MessageMappingBuilder(services, registry);
		configure?.Invoke(builder);

		return builder.Build().ShouldBeOfType<ConfiguredMessageMapper>();
	}

	private sealed class DelegatingMapper(
		string name,
		string sourceTransport,
		string targetTransport,
		Func<ITransportMessageContext, ITransportMessageContext> mapFunc) : IMessageMapper
	{
		public int CallCount { get; private set; }

		public string Name { get; } = name;

		public string SourceTransport { get; } = sourceTransport;

		public string TargetTransport { get; } = targetTransport;

		public bool CanMap(string sourceTransportName, string targetTransportName) =>
			string.Equals(sourceTransportName, SourceTransport, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(targetTransportName, TargetTransport, StringComparison.OrdinalIgnoreCase);

		public ITransportMessageContext Map(ITransportMessageContext source, string targetTransportName)
		{
			CallCount++;
			return mapFunc(source);
		}
	}

	private sealed record TestMappedMessage : IDispatchMessage;
}
