// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using FakeItEasy;

using RabbitMQ.Client;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Asserts the AMQP effect of the fluent topology configuration: that every configured exchange,
/// queue and binding reaches the broker with the settings the caller supplied, and that a configured
/// dead-letter exchange gets a queue bound to it.
/// </summary>
/// <remarks>
/// These assert the declared topology, not that a value reached a constructor. Each arm names the
/// exact broker call it requires, so a mapping that silently drops a configured value fails here.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMqConfiguredTopologyShould : UnitTestBase
{
	private static IChannel FakeChannel(out List<(string Queue, bool Durable, bool Exclusive, bool AutoDelete, IDictionary<string, object?>? Arguments)> queues,
		out List<(string Exchange, string Type, bool Durable, bool AutoDelete)> exchanges,
		out List<(string Queue, string Exchange, string RoutingKey)> bindings)
	{
		var channel = A.Fake<IChannel>();
		var declaredQueues = new List<(string, bool, bool, bool, IDictionary<string, object?>?)>();
		var declaredExchanges = new List<(string, string, bool, bool)>();
		var declaredBindings = new List<(string, string, string)>();

		_ = A.CallTo(() => channel.ExchangeDeclareAsync(
					A<string>._, A<string>._, A<bool>._, A<bool>._,
					A<IDictionary<string, object?>>._, A<bool>._, A<bool>._, A<CancellationToken>._))
			.Invokes((string exchange, string type, bool durable, bool autoDelete,
					IDictionary<string, object?> arguments, bool passive, bool noWait, CancellationToken ct) =>
				declaredExchanges.Add((exchange, type, durable, autoDelete)))
			.Returns(Task.CompletedTask);

		_ = A.CallTo(() => channel.QueueDeclareAsync(
					A<string>._, A<bool>._, A<bool>._, A<bool>._,
					A<IDictionary<string, object?>>._, A<bool>._, A<bool>._, A<CancellationToken>._))
			.Invokes((string queue, bool durable, bool exclusive, bool autoDelete,
					IDictionary<string, object?> arguments, bool passive, bool noWait, CancellationToken ct) =>
				declaredQueues.Add((queue, durable, exclusive, autoDelete, arguments)))
			.Returns(Task.FromResult(new QueueDeclareOk("q", 0, 0)));

		_ = A.CallTo(() => channel.QueueBindAsync(
					A<string>._, A<string>._, A<string>._,
					A<IDictionary<string, object?>>._, A<bool>._, A<CancellationToken>._))
			.Invokes((string queue, string exchange, string routingKey,
					IDictionary<string, object?> arguments, bool noWait, CancellationToken ct) =>
				declaredBindings.Add((queue, exchange, routingKey)))
			.Returns(Task.CompletedTask);

		queues = declaredQueues;
		exchanges = declaredExchanges;
		bindings = declaredBindings;
		return channel;
	}

	private static RabbitMQTopologyOptions TwoExchangeTopology()
	{
		var topology = new RabbitMQTopologyOptions();

		topology.Exchanges.Add(new RabbitMQExchangeOptions
		{
			Name = "orders",
			Type = RabbitMQExchangeType.Direct,
			Durable = false,
			AutoDelete = true,
		});
		topology.Exchanges.Add(new RabbitMQExchangeOptions { Name = "audit" });

		topology.Queues.Add(new RabbitMQQueueOptions
		{
			Name = "orders-handler",
			MessageTtl = TimeSpan.FromMinutes(5),
			MaxLength = 1000,
			MaxLengthBytes = 4096,
		});

		topology.Bindings.Add(new RabbitMQBindingOptions
		{
			Exchange = "orders",
			Queue = "orders-handler",
			RoutingKey = "orders.created",
		});

		return topology;
	}

	[Fact]
	public async Task DeclareEveryConfiguredExchange_NotOnlyTheFirst()
	{
		var channel = FakeChannel(out _, out var exchanges, out _);
		var initializer = new RabbitMqTopologyInitializer(
			new RabbitMqOptions(), cloudEventOptions: null, logger: null, TwoExchangeTopology());

		await initializer.EnsureInitializedAsync(channel, CancellationToken.None).ConfigureAwait(false);

		exchanges.ShouldContain(e => e.Exchange == "orders" && e.Type == ExchangeType.Direct && !e.Durable && e.AutoDelete);
		exchanges.ShouldContain(e => e.Exchange == "audit" && e.Type == ExchangeType.Topic && e.Durable && !e.AutoDelete);
	}

	[Fact]
	public async Task TranslateQueueLimitsIntoAmqpArguments()
	{
		var channel = FakeChannel(out var queues, out _, out _);
		var initializer = new RabbitMqTopologyInitializer(
			new RabbitMqOptions(), cloudEventOptions: null, logger: null, TwoExchangeTopology());

		await initializer.EnsureInitializedAsync(channel, CancellationToken.None).ConfigureAwait(false);

		var declared = queues.ShouldHaveSingleItem();
		declared.Queue.ShouldBe("orders-handler");
		_ = declared.Arguments.ShouldNotBeNull();
		declared.Arguments["x-message-ttl"].ShouldBe(300000L);
		declared.Arguments["x-max-length"].ShouldBe(1000);
		declared.Arguments["x-max-length-bytes"].ShouldBe(4096L);
	}

	[Fact]
	public async Task DeclareConfiguredBindings()
	{
		var channel = FakeChannel(out _, out _, out var bindings);
		var initializer = new RabbitMqTopologyInitializer(
			new RabbitMqOptions(), cloudEventOptions: null, logger: null, TwoExchangeTopology());

		await initializer.EnsureInitializedAsync(channel, CancellationToken.None).ConfigureAwait(false);

		bindings.ShouldContain(b => b.Queue == "orders-handler" && b.Exchange == "orders" && b.RoutingKey == "orders.created");
	}

	[Fact]
	public async Task BindADeadLetterQueueToTheDeadLetterExchange()
	{
		var channel = FakeChannel(out var queues, out var exchanges, out var bindings);
		var options = new RabbitMqOptions
		{
			Queue = new RabbitMqQueueOptions { QueueName = "orders" },
			DeadLetter = new RabbitMqDeadLetterExchangeOptions
			{
				EnableDeadLetterExchange = true,
				DeadLetterExchange = "orders.dlx",
				DeadLetterRoutingKey = "failed",
			},
		};
		var deadLetter = new RabbitMQDeadLetterOptions
		{
			Exchange = "orders.dlx",
			Queue = "orders.dlq",
			RoutingKey = "failed",
		};

		var initializer = new RabbitMqTopologyInitializer(
			options, cloudEventOptions: null, logger: null, topology: null, deadLetter: deadLetter);

		await initializer.EnsureInitializedAsync(channel, CancellationToken.None).ConfigureAwait(false);

		exchanges.ShouldContain(e => e.Exchange == "orders.dlx" && e.Type == ExchangeType.Direct);
		queues.ShouldContain(q => q.Queue == "orders.dlq" && q.Durable);
		bindings.ShouldContain(b => b.Queue == "orders.dlq" && b.Exchange == "orders.dlx" && b.RoutingKey == "failed");
	}

	[Fact]
	public async Task LeaveTheDefaultPairUntouchedWhenNoTopologyIsConfigured()
	{
		var channel = FakeChannel(out var queues, out var exchanges, out _);
		var options = new RabbitMqOptions
		{
			Exchange = "legacy.exchange",
			Queue = new RabbitMqQueueOptions { QueueName = "legacy.queue" },
		};

		var initializer = new RabbitMqTopologyInitializer(options, cloudEventOptions: null);

		await initializer.EnsureInitializedAsync(channel, CancellationToken.None).ConfigureAwait(false);

		exchanges.ShouldHaveSingleItem().Exchange.ShouldBe("legacy.exchange");
		queues.ShouldHaveSingleItem().Queue.ShouldBe("legacy.queue");
	}
}
