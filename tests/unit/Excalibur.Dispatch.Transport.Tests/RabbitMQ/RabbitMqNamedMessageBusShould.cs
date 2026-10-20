// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

using BasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Pattern", "Regression")]
public sealed class RabbitMqNamedMessageBusShould
{
	[Fact]
	public async Task PublishThroughIndependentNamedChannelsAndOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson()));
		foreach (var name in new[] { "orders", "audit" })
		{
			_ = services.AddRabbitMQTransport(name, rabbit => rabbit.ConnectionString("amqps://user:password@" + name + "-broker/")
				.ConfigureExchange(exchange => exchange.Name(name + "-exchange")));
			_ = services.Configure<RabbitMqOptions>(name, options => options.RoutingKey = name + "-key");
			_ = services.Configure<RabbitMqCloudEventOptions>(name, options =>
			{
				options.Exchange.EnablePublisherConfirms = false;
				options.Publisher.EnableConfirms = false;
			});
		}
		services.Any(d => d.ServiceType == typeof(NamedRabbitMqChannel) && Equals(d.ServiceKey, "orders")).ShouldBeTrue();
		var orders = A.Fake<IChannel>();
		var audit = A.Fake<IChannel>();
		ReadOnlyMemory<byte> ordersBody = default;
		ReadOnlyMemory<byte> auditBody = default;
		A.CallTo(() => orders.BasicPublishAsync(A<string>._, A<string>._, A<bool>._, A<BasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes((string _, string _, bool _, BasicProperties _, ReadOnlyMemory<byte> body, CancellationToken _) => ordersBody = body)
			.Returns(ValueTask.CompletedTask);
		A.CallTo(() => audit.BasicPublishAsync(A<string>._, A<string>._, A<bool>._, A<BasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes((string _, string _, bool _, BasicProperties _, ReadOnlyMemory<byte> body, CancellationToken _) => auditBody = body)
			.Returns(ValueTask.CompletedTask);
		_ = services.AddKeyedSingleton<IChannel>("orders", orders);
		_ = services.AddKeyedSingleton<IChannel>("audit", audit);
		_ = services.AddSingleton<IConnectionFactory>(_ => throw new InvalidOperationException("A supplied channel must bypass connection creation"));
		await using (var provider = services.BuildServiceProvider())
		{
			var buses = provider.GetRequiredService<IMessageBusProvider>();
			await buses.GetRemoteMessageBus("orders").ShouldNotBeNull().PublishAsync(new ChannelEvent("orders"), CreateContext(), CancellationToken.None);
			await buses.GetRemoteMessageBus("audit").ShouldNotBeNull().PublishAsync(new ChannelEvent("audit"), CreateContext(), CancellationToken.None);
			var serializer = provider.GetRequiredService<IPayloadSerializer>();
			serializer.Deserialize<ChannelEvent>(ordersBody.ToArray()).Value.ShouldBe("orders");
			serializer.Deserialize<ChannelEvent>(auditBody.ToArray()).Value.ShouldBe("audit");
		}
		A.CallTo(() => orders.BasicPublishAsync("orders-exchange", "orders-key", A<bool>._, A<BasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => audit.BasicPublishAsync("audit-exchange", "audit-key", A<bool>._, A<BasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => orders.DisposeAsync()).MustNotHaveHappened();
		A.CallTo(() => audit.DisposeAsync()).MustNotHaveHappened();
	}

	[Fact]
	public void DetachBusConfirmationHandlersWithoutDisposingBorrowedChannel()
	{
		var channel = A.Fake<IChannel>();
		var cloudEvents = new RabbitMqCloudEventOptions();
		cloudEvents.Exchange.EnablePublisherConfirms = true;
		cloudEvents.Exchange.MandatoryPublishing = true;
		var bus = new RabbitMqMessageBus(channel, A.Fake<IPayloadSerializer>(),
			Microsoft.Extensions.Options.Options.Create(new RabbitMqOptions()), NullLogger<RabbitMqMessageBus>.Instance,
			cloudEventOptions: cloudEvents);
		var cleanup = bus.ShouldBeAssignableTo<IDisposable>();
		cleanup.Dispose();
		cleanup.Dispose();
		FakeItEasy.Fake.GetCalls(channel).Count(c => c.Method.Name == "remove_BasicAcksAsync").ShouldBe(1);
		FakeItEasy.Fake.GetCalls(channel).Count(c => c.Method.Name == "remove_BasicNacksAsync").ShouldBe(1);
		FakeItEasy.Fake.GetCalls(channel).Count(c => c.Method.Name == "remove_BasicReturnAsync").ShouldBe(1);
		A.CallTo(() => channel.Dispose()).MustNotHaveHappened();
		A.CallTo(() => channel.DisposeAsync()).MustNotHaveHappened();
	}

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		return context;
	}

	[Theory]
	[InlineData("add_BasicNacksAsync", "remove_BasicAcksAsync")]
	[InlineData("add_BasicReturnAsync", "remove_BasicNacksAsync")]
	public void RollBackConfirmationSubscriptionsWhenConstructionFails(string failingAccessor, string expectedRemoval)
	{
		var channel = A.Fake<IChannel>();
		A.CallTo(channel).Where(call => call.Method.Name == failingAccessor).Throws(new InvalidOperationException("event add failed"));
		var cloudEvents = new RabbitMqCloudEventOptions();
		cloudEvents.Exchange.EnablePublisherConfirms = true;
		cloudEvents.Exchange.MandatoryPublishing = true;
		Should.Throw<InvalidOperationException>(() => new RabbitMqMessageBus(channel, A.Fake<IPayloadSerializer>(),
			Microsoft.Extensions.Options.Options.Create(new RabbitMqOptions()), NullLogger<RabbitMqMessageBus>.Instance,
			cloudEventOptions: cloudEvents));
		FakeItEasy.Fake.GetCalls(channel).Count(call => call.Method.Name == expectedRemoval).ShouldBe(1);
		A.CallTo(() => channel.DisposeAsync()).MustNotHaveHappened();
	}

	[Fact]
	public void AttemptEveryConfirmationDetachWhenOneAccessorFails()
	{
		var channel = A.Fake<IChannel>();
		var cloudEvents = new RabbitMqCloudEventOptions();
		cloudEvents.Exchange.EnablePublisherConfirms = true;
		cloudEvents.Exchange.MandatoryPublishing = true;
		var bus = new RabbitMqMessageBus(channel, A.Fake<IPayloadSerializer>(),
			Microsoft.Extensions.Options.Options.Create(new RabbitMqOptions()), NullLogger<RabbitMqMessageBus>.Instance,
			cloudEventOptions: cloudEvents);
		A.CallTo(channel).Where(call => call.Method.Name == "remove_BasicAcksAsync").Throws(new InvalidOperationException("event remove failed"));
		Should.Throw<InvalidOperationException>(() => bus.Dispose());
		FakeItEasy.Fake.GetCalls(channel).Count(call => call.Method.Name == "remove_BasicNacksAsync").ShouldBe(1);
		FakeItEasy.Fake.GetCalls(channel).Count(call => call.Method.Name == "remove_BasicReturnAsync").ShouldBe(1);
		A.CallTo(() => channel.DisposeAsync()).MustNotHaveHappened();
	}

	private sealed record ChannelEvent(string Value) : IDispatchEvent;
	private sealed record QueueEvent(string Value) : IDispatchEvent;

	[Fact]
	public async Task ShareReentrantOwnedShutdownAndCloseTheConnectionOnlyAfterTheChannel()
	{
		var channel = A.Fake<IChannel>();
		var connection = A.Fake<IConnection>();
		var bundle = new NamedRabbitMqChannel(channel, connection, ownsChannel: true, ownsConnection: true);
		var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Task? reentrant = null;
		A.CallTo(() => channel.DisposeAsync()).Invokes(() => reentrant = bundle.DisposeAsync().AsTask())
			.Returns(new ValueTask(pending.Task));
		var shutdown = bundle.DisposeAsync().AsTask();
		reentrant.ShouldBeSameAs(shutdown);
		shutdown.IsCompleted.ShouldBeFalse();
		A.CallTo(() => connection.DisposeAsync()).MustNotHaveHappened();
		pending.SetResult();
		await shutdown;
		bundle.DisposeAsync().AsTask().ShouldBeSameAs(shutdown);
		A.CallTo(() => channel.DisposeAsync()).MustHaveHappenedOnceExactly();
		A.CallTo(() => connection.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AttemptBothOwnedCleanupsAndPreserveTheirFailures()
	{
		var channel = A.Fake<IChannel>();
		var connection = A.Fake<IConnection>();
		var first = new IOException("channel cleanup failed");
		var second = new IOException("connection cleanup failed");
		A.CallTo(() => channel.DisposeAsync()).Throws(first);
		A.CallTo(() => connection.DisposeAsync()).Throws(second);
		var bundle = new NamedRabbitMqChannel(channel, connection, ownsChannel: true, ownsConnection: true);
		var shutdown = bundle.DisposeAsync().AsTask();
		var failure = await Should.ThrowAsync<AggregateException>(() => shutdown);
		failure.InnerExceptions.Count.ShouldBe(2);
		failure.InnerExceptions[0].ShouldBeSameAs(first);
		failure.InnerExceptions[1].ShouldBeSameAs(second);
		bundle.DisposeAsync().AsTask().ShouldBeSameAs(shutdown);
		A.CallTo(() => channel.DisposeAsync()).MustHaveHappenedOnceExactly();
		A.CallTo(() => connection.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(RabbitMQExchangeType.Direct, "#", false, "#")]
	[InlineData(RabbitMQExchangeType.Topic, "orders.created", false, "orders.created")]
	[InlineData(RabbitMQExchangeType.Topic, "orders.*", true, "")]
	[InlineData(RabbitMQExchangeType.Topic, "#", true, "")]
	[InlineData(RabbitMQExchangeType.Fanout, "#", false, "")]
	[InlineData(RabbitMQExchangeType.Headers, "#", false, "")]
	public void InferOnlyUnambiguousLiteralPublicationKeys(RabbitMQExchangeType exchangeType, string bindingKey,
		bool shouldFail, string expected)
	{
		var topology = new RabbitMQTopologyOptions();
		topology.ExchangeMappings[typeof(ChannelEvent)] = "events";
		topology.QueueMappings[typeof(ChannelEvent)] = "orders";
		topology.Exchanges.Add(new RabbitMQExchangeOptions { Name = "events", Type = exchangeType });
		topology.Bindings.Add(new RabbitMQBindingOptions { Exchange = "events", Queue = "orders", RoutingKey = bindingKey });
		var routes = new RabbitMqPublishRoutes(topology);
		if (shouldFail)
		{
			Should.Throw<InvalidOperationException>(() => routes.Resolve(typeof(ChannelEvent), "fallback", ""));
		}
		else
		{
			routes.Resolve(typeof(ChannelEvent), "fallback", "").ShouldBe(("events", expected));
		}
		routes.Resolve(typeof(ChannelEvent), "fallback", "explicit").ShouldBe(("events", "explicit"));
		routes.Resolve(typeof(QueueEvent), "fallback", "healthy").ShouldBe(("fallback", "healthy"));
	}

	[Fact]
	public void OwnTheRouteSnapshotAndRejectMultipleDistinctBindingKeys()
	{
		var topology = new RabbitMQTopologyOptions();
		topology.ExchangeMappings[typeof(ChannelEvent)] = "events";
		topology.QueueMappings[typeof(ChannelEvent)] = "orders";
		topology.Exchanges.Add(new RabbitMQExchangeOptions { Name = "events", Type = RabbitMQExchangeType.Direct });
		topology.Bindings.Add(new RabbitMQBindingOptions { Exchange = "events", Queue = "orders", RoutingKey = "one" });
		topology.Bindings.Add(new RabbitMQBindingOptions { Exchange = "events", Queue = "orders", RoutingKey = "one" });
		var routes = new RabbitMqPublishRoutes(topology);
		topology.ExchangeMappings[typeof(ChannelEvent)] = "changed";
		topology.Bindings[0].RoutingKey = "changed";
		routes.Resolve(typeof(ChannelEvent), "fallback", "").ShouldBe(("events", "one"));
		topology.ExchangeMappings[typeof(ChannelEvent)] = "events";
		Should.Throw<InvalidOperationException>(() => new RabbitMqPublishRoutes(topology).Resolve(typeof(ChannelEvent), "fallback", ""));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void ResolveSuccessfulPublicationRoutesWithoutPerMessageAllocations(bool inferred)
	{
		var topology = new RabbitMQTopologyOptions();
		topology.ExchangeMappings[typeof(ChannelEvent)] = "events";
		topology.QueueMappings[typeof(ChannelEvent)] = "orders";
		topology.Exchanges.Add(new RabbitMQExchangeOptions { Name = "events", Type = RabbitMQExchangeType.Topic });
		topology.Bindings.Add(new RabbitMQBindingOptions { Exchange = "events", Queue = "orders", RoutingKey = "orders.created" });
		var routes = new RabbitMqPublishRoutes(topology);
		var messageType = inferred ? typeof(ChannelEvent) : typeof(QueueEvent);
		for (var i = 0; i < 100; i++) { _ = routes.Resolve(messageType, "fallback", ""); }
		var before = GC.GetAllocatedBytesForCurrentThread();
		var consumed = 0;
		for (var i = 0; i < 1000; i++)
		{
			var destination = routes.Resolve(messageType, "fallback", "");
			consumed += destination.Exchange.Length + destination.RoutingKey.Length;
		}
		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		consumed.ShouldBeGreaterThan(0);
		allocated.ShouldBe(0);
	}

	[Theory]
	[InlineData("")]
	[InlineData("prod-")]
	public async Task HonorExactExchangeAndDirectQueueMappingsInsteadOfTheGenericFallback(string prefix)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson()));
		_ = services.AddRabbitMQTransport("events", rabbit =>
		{
			_ = rabbit.ConnectionString("amqps://user:password@broker/")
			.ConfigureExchange(exchange => exchange.Name("fallback-exchange"))
			.ConfigureExchange(exchange => exchange.Name("orders-exchange"))
			.MapExchange<ChannelEvent>("orders-exchange").MapQueue<QueueEvent>("direct-queue");
			if (prefix.Length > 0)
			{
				_ = rabbit.WithExchangePrefix(prefix).WithQueuePrefix(prefix);
			}
		});
		_ = services.Configure<RabbitMqOptions>("events", options => options.RoutingKey = "orders-key");
		_ = services.Configure<RabbitMqCloudEventOptions>("events", options =>
		{
			options.Exchange.EnablePublisherConfirms = false;
			options.Publisher.EnableConfirms = false;
		});
		var channel = A.Fake<IChannel>();
		_ = services.AddKeyedSingleton<IChannel>("events", channel);
		A.CallTo(() => channel.BasicPublishAsync(A<string>._, A<string>._, A<bool>._, A<BasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);
		await using var provider = services.BuildServiceProvider();
		var bus = provider.GetRequiredService<IMessageBusProvider>().GetRemoteMessageBus("events").ShouldNotBeNull();
		await bus.PublishAsync(new ChannelEvent("orders"), CreateContext(), CancellationToken.None);
		await bus.PublishAsync(new QueueEvent("queue"), CreateContext(), CancellationToken.None);
		A.CallTo(() => channel.BasicPublishAsync(prefix + "orders-exchange", "orders-key", A<bool>._, A<BasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => channel.BasicPublishAsync("", prefix + "direct-queue", A<bool>._, A<BasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
