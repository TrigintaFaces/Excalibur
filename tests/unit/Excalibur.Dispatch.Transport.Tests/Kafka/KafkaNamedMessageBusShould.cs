// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "Registration")]
public sealed class KafkaNamedMessageBusShould
{
	[Fact]
	public void ExposeEveryNamedTransportToTheDispatchMessageBusProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddKafkaTransport("orders", kafka => kafka.BootstrapServers("orders-broker:9092"));
		_ = services.AddKafkaTransport("audit", kafka => kafka.BootstrapServers("audit-broker:9092"));

		using var provider = services.BuildServiceProvider();
		var buses = provider.GetRequiredService<IMessageBusProvider>();

		// Enumeration proves registration without constructing native clients or contacting brokers.
		buses.GetAllMessageBusNames().ShouldContain("orders");
		buses.GetAllMessageBusNames().ShouldContain("audit");
	}

	[Fact]
	public async Task PublishThroughIndependentNamedClientsAndTopicsWithoutTakingBorrowedOwnership()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson()));
		_ = services.AddKafkaTransport("orders", kafka => kafka.BootstrapServers("orders-broker:9092"));
		_ = services.AddKafkaTransport("audit", kafka => kafka.BootstrapServers("audit-broker:9092"));
		_ = services.Configure<KafkaOptions>("orders", options => options.Topic = "orders-topic");
		_ = services.Configure<KafkaOptions>("audit", options => options.Topic = "audit-topic");
		var orders = A.Fake<IProducer<string, byte[]>>();
		var audit = A.Fake<IProducer<string, byte[]>>();
		var global = A.Fake<IProducer<string, byte[]>>();
		_ = services.AddSingleton<IProducer<string, byte[]>>(_ => global);
		_ = services.AddKeyedSingleton<IProducer<string, byte[]>>("orders", orders);
		_ = services.AddKeyedSingleton<IProducer<string, byte[]>>("audit", audit);
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => orders.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));
		A.CallTo(() => audit.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));

		await using (var provider = services.BuildServiceProvider())
		{
			var buses = provider.GetRequiredService<IMessageBusProvider>();
			var ordersBus = buses.GetRemoteMessageBus("orders").ShouldNotBeNull();
			var auditBus = buses.GetRemoteMessageBus("audit").ShouldNotBeNull();
			ordersBus.ShouldNotBeSameAs(auditBus);
			await ordersBus.PublishAsync(new NamedBusEvent(), context, CancellationToken.None);
			await auditBus.PublishAsync(new NamedBusEvent(), context, CancellationToken.None);
		}

		A.CallTo(() => orders.ProduceAsync("orders-topic", A<Message<string, byte[]>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => audit.ProduceAsync("audit-topic", A<Message<string, byte[]>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => orders.ProduceAsync("audit-topic", A<Message<string, byte[]>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => orders.Dispose()).MustNotHaveHappened();
		A.CallTo(() => audit.Dispose()).MustNotHaveHappened();
		A.CallTo(() => global.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	private sealed class NamedBusEvent : IDispatchEvent;
	private sealed class AuditBusEvent : IDispatchEvent;
	private sealed class UnmappedBusEvent : IDispatchEvent;
	private sealed class RoutedBusEvent : IIntegrationEvent;

	[Fact]
	public async Task PublishThroughTheActualDispatchPipelineWhenFluentRoutingIsConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson());
			_ = dispatch.UseKafka("events", kafka => kafka.BootstrapServers("broker:9092").MapTopic<RoutedBusEvent>("routed-topic"));
			_ = dispatch.UseRouting(routing =>
			{
				_ = routing.Transport.Route<RoutedBusEvent>().To("events");
				_ = routing.Endpoints.Route<RoutedBusEvent>().To("events");
			});
		});
		var producer = A.Fake<IProducer<string, byte[]>>();
		_ = services.AddKeyedSingleton<IProducer<string, byte[]>>("events", producer);
		A.CallTo(() => producer.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));
		await using var provider = services.BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: bounds the dispatch, so a producer that never completes fails this test rather than stalling the shard
		var result = await scope.ServiceProvider.GetRequiredService<IDispatcher>().DispatchAsync(new RoutedBusEvent(), deadline.Token);
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => producer.ProduceAsync("routed-topic", A<Message<string, byte[]>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}
	private class BaseBusEvent : IDispatchEvent;
	private sealed class DerivedBusEvent : BaseBusEvent;

	[Fact]
	public async Task PublishMappedEventsWithoutDefaultAndRefuseUnmappedRuntimeTypes()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson()));
		_ = services.AddKafkaTransport("events", kafka => kafka.BootstrapServers("broker:9092")
			.MapTopic<NamedBusEvent>("orders-topic").MapTopic<BaseBusEvent>("base-topic"));
		var producer = A.Fake<IProducer<string, byte[]>>();
		_ = services.AddKeyedSingleton<IProducer<string, byte[]>>("events", producer);
		A.CallTo(() => producer.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		await using var provider = services.BuildServiceProvider();
		var bus = provider.GetRequiredService<IMessageBusProvider>().GetRemoteMessageBus("events").ShouldNotBeNull();
		await bus.PublishAsync(new NamedBusEvent(), context, CancellationToken.None);
		_ = await Should.ThrowAsync<InvalidOperationException>(() => bus.PublishAsync(new UnmappedBusEvent(), context, CancellationToken.None));
		_ = await Should.ThrowAsync<InvalidOperationException>(() => bus.PublishAsync(new DerivedBusEvent(), context, CancellationToken.None));
		A.CallTo(() => producer.ProduceAsync("orders-topic", A<Message<string, byte[]>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => producer.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task OwnTheMappingSnapshotAndIsolateTheSameTypeAcrossNames()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson()));
		IKafkaTransportBuilder? captured = null;
		_ = services.AddKafkaTransport("orders", kafka =>
		{
			captured = kafka;
			_ = kafka.BootstrapServers("orders-broker:9092").MapTopic<NamedBusEvent>("orders-topic");
		});
		_ = services.AddKafkaTransport("audit", kafka => kafka.BootstrapServers("audit-broker:9092").MapTopic<NamedBusEvent>("audit-topic"));
		_ = captured.ShouldNotBeNull().MapTopic<NamedBusEvent>("late-mutated-topic");
		var orders = A.Fake<IProducer<string, byte[]>>();
		var audit = A.Fake<IProducer<string, byte[]>>();
		_ = services.AddKeyedSingleton<IProducer<string, byte[]>>("orders", orders);
		_ = services.AddKeyedSingleton<IProducer<string, byte[]>>("audit", audit);
		A.CallTo(() => orders.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));
		A.CallTo(() => audit.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		await using var provider = services.BuildServiceProvider();
		var buses = provider.GetRequiredService<IMessageBusProvider>();
		await buses.GetRemoteMessageBus("orders").ShouldNotBeNull().PublishAsync(new NamedBusEvent(), context, CancellationToken.None);
		await buses.GetRemoteMessageBus("audit").ShouldNotBeNull().PublishAsync(new NamedBusEvent(), context, CancellationToken.None);
		A.CallTo(() => orders.ProduceAsync("orders-topic", A<Message<string, byte[]>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => audit.ProduceAsync("audit-topic", A<Message<string, byte[]>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HonorFluentExactTypeTopicMappingsAndRetainTheUnmappedFallback()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson()));
		_ = services.AddKafkaTransport("events", kafka => kafka.BootstrapServers("broker:9092")
			.MapTopic<NamedBusEvent>("orders-topic").MapTopic<AuditBusEvent>("audit-topic"));
		_ = services.Configure<KafkaOptions>("events", options => options.Topic = "fallback-topic");
		var producer = A.Fake<IProducer<string, byte[]>>();
		_ = services.AddKeyedSingleton<IProducer<string, byte[]>>("events", producer);
		A.CallTo(() => producer.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		await using var provider = services.BuildServiceProvider();
		var bus = provider.GetRequiredService<IMessageBusProvider>().GetRemoteMessageBus("events").ShouldNotBeNull();
		await bus.PublishAsync(new NamedBusEvent(), context, CancellationToken.None);
		await bus.PublishAsync(new AuditBusEvent(), context, CancellationToken.None);
		await bus.PublishAsync(new UnmappedBusEvent(), context, CancellationToken.None);
		A.CallTo(() => producer.ProduceAsync("orders-topic", A<Message<string, byte[]>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => producer.ProduceAsync("audit-topic", A<Message<string, byte[]>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => producer.ProduceAsync("fallback-topic", A<Message<string, byte[]>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData("before")]
	[InlineData("after")]
	[InlineData("between")]
	public async Task PreserveExplicitUnkeyedProducerOverrides(string registrationOrder)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson()));
		var producer = A.Fake<IProducer<string, byte[]>>();
		if (registrationOrder == "before")
		{
			_ = services.AddSingleton<IProducer<string, byte[]>>(_ => producer);
		}
		_ = services.AddKafkaTransport("orders", kafka => kafka.BootstrapServers("orders-broker:9092"));
		if (registrationOrder == "between")
		{
			_ = services.AddSingleton<IProducer<string, byte[]>>(_ => producer);
		}
		_ = services.AddKafkaTransport("audit", kafka => kafka.BootstrapServers("audit-broker:9092"));
		if (registrationOrder == "after")
		{
			_ = services.AddSingleton<IProducer<string, byte[]>>(_ => producer);
		}
		_ = services.Configure<KafkaOptions>("orders", options => options.Topic = "orders-topic");
		_ = services.Configure<KafkaOptions>("audit", options => options.Topic = "audit-topic");
		A.CallTo(() => producer.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		await using (var provider = services.BuildServiceProvider())
		{
			var bus = provider.GetRequiredService<IMessageBusProvider>().GetRemoteMessageBus("orders").ShouldNotBeNull();
			await bus.PublishAsync(new NamedBusEvent(), context, CancellationToken.None);
			var auditBus = provider.GetRequiredService<IMessageBusProvider>().GetRemoteMessageBus("audit").ShouldNotBeNull();
			await auditBus.PublishAsync(new NamedBusEvent(), context, CancellationToken.None);
		}
		A.CallTo(() => producer.ProduceAsync("orders-topic", A<Message<string, byte[]>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => producer.ProduceAsync("audit-topic", A<Message<string, byte[]>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => producer.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAnOwnedProducerOnlyOnce()
	{
		var producer = A.Fake<IProducer<string, byte[]>>();
		var bus = new KafkaMessageBus(producer, A.Fake<IPayloadSerializer>(),
			Microsoft.Extensions.Options.Options.Create(new KafkaOptions()), NullLogger<KafkaMessageBus>.Instance);
		await bus.DisposeAsync();
		await bus.DisposeAsync();
		A.CallTo(() => producer.Flush(A<TimeSpan>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => producer.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAnOwnedProducerEvenWhenFlushThrows()
	{
		var producer = A.Fake<IProducer<string, byte[]>>();
		A.CallTo(() => producer.Flush(A<TimeSpan>._)).Throws(new InvalidOperationException("Flush failed"));
		var bus = new KafkaMessageBus(producer, A.Fake<IPayloadSerializer>(),
			Microsoft.Extensions.Options.Options.Create(new KafkaOptions()), NullLogger<KafkaMessageBus>.Instance);
		_ = await Should.ThrowAsync<InvalidOperationException>(async () => await bus.DisposeAsync());
		await bus.DisposeAsync();
		A.CallTo(() => producer.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task PreserveExplicitCustomNamedMessageBus(bool registerBefore)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		var custom = A.Fake<IMessageBus>();
		if (registerBefore)
		{
			_ = services.AddRemoteMessageBus("orders", _ => custom);
		}
		_ = services.AddKafkaTransport("orders", kafka => kafka.BootstrapServers("broker:9092"));
		if (!registerBefore)
		{
			_ = services.AddKeyedSingleton<IMessageBus>("orders", custom);
		}
		await using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IMessageBusProvider>().GetRemoteMessageBus("orders").ShouldBeSameAs(custom);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task PreserveConfluentProviderPublishingInEitherRegistrationOrder(bool registerBefore)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		var producer = A.Fake<IProducer<string, byte[]>>();
		var serializer = A.Fake<IConfluentFormatSerializer>();
		_ = services.AddSingleton<IProducer<string, byte[]>>(_ => producer);
		_ = services.AddSingleton(serializer);
		if (registerBefore)
		{
			_ = services.AddConfluentFormat(registry => registry.Url = "http://schema-registry:8081");
		}
		_ = services.AddKafkaTransport(kafka => kafka.BootstrapServers("broker:9092"));
		if (!registerBefore)
		{
			_ = services.AddConfluentFormat(registry => registry.Url = "http://schema-registry:8081");
		}
		_ = services.Configure<KafkaOptions>(options => options.Topic = "confluent-topic");
		byte[] expected = [0, 0, 0, 0, 7, 123, 125];
		A.CallTo(() => serializer.SerializeAsync("confluent-topic", A<object>._, typeof(NamedBusEvent), A<CancellationToken>._))
			.Returns(Task.FromResult(expected));
		A.CallTo(() => producer.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		await using var provider = services.BuildServiceProvider();
		var bus = provider.GetRequiredService<IMessageBusProvider>().GetRemoteMessageBus("kafka").ShouldNotBeNull();
		bus.ShouldBeOfType<ConfluentKafkaMessageBus>();
		await bus.PublishAsync(new NamedBusEvent(), context, CancellationToken.None);
		A.CallTo(() => producer.ProduceAsync("confluent-topic", A<Message<string, byte[]>>.That.Matches(m => m.Value.SequenceEqual(expected)), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
