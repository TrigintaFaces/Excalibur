// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Telemetry;
using Excalibur.Dispatch.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Confluent.Kafka;
using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Outbox")]
public sealed class MessageBusOutboxPublisherShould
{
	private readonly IOutboxStore _outboxStore;
	private readonly IPayloadSerializer _serializer;
	private readonly IMessageBusAdapter _messageBus;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<MessageBusOutboxPublisher> _logger;
	private readonly MessageBusOutboxPublisher _publisher;

	public MessageBusOutboxPublisherShould()
	{
		_outboxStore = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>()).WithHonestCapabilities();
		_serializer = OutboxFakeCapabilityExtensions.CreateRegisteredSerializer();
		_messageBus = A.Fake<IMessageBusAdapter>();
		_serviceProvider = A.Fake<IServiceProvider>();
		_logger = A.Fake<ILogger<MessageBusOutboxPublisher>>();

		_publisher = new MessageBusOutboxPublisher(
			_outboxStore,
			_serializer,
			_messageBus,
			_serviceProvider,
			_logger);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RestoreOriginalEventThroughAStartedFrameworkKafkaAdapter(bool historicalAlias)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDispatch(dispatch => dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson()));
		services.AddKafkaTransport("durable", kafka => kafka.BootstrapServers("unused:9092").MapTopic<DurableEvent>("durable-events"));
		var producer = A.Fake<IProducer<string, byte[]>>();
		services.AddKeyedSingleton<IProducer<string, byte[]>>("durable", producer);
		Message<string, byte[]>? sent = null;
		A.CallTo(() => producer.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Invokes((string _, Message<string, byte[]> value, CancellationToken _) => sent = value)
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));
		await using var provider = services.BuildServiceProvider();
		var frameworkRegistry = provider.GetRequiredService<ITransportRegistry>();
		frameworkRegistry.ShouldBeOfType<TransportRegistry>().InitializeFactories(provider);
		var adapter = frameworkRegistry.GetTransportAdapter("durable").ShouldNotBeNull();
		await adapter.ShouldBeAssignableTo<ITransportAdapterLifecycle>().StartAsync(CancellationToken.None);
		var registry = new TransportRegistry();
		registry.RegisterTransport("durable", adapter, "Kafka", TransportLocality.Remote);
		var store = A.Fake<IOutboxStore>(o => o.Implements<IMultiTransportOutboxStore>()
			.Implements<IMultiTransportOutboxStoreAdmin>().Implements<IOutboxStoreAdmin>()).WithHonestCapabilities();
		var serializer = provider.GetRequiredService<IPayloadSerializer>();
		var publisher = new MessageBusOutboxPublisher(store, serializer, registry, provider, _logger);
		var original = new DurableEvent { OrderId = Guid.NewGuid(), Quantity = 7 };
		var row = await publisher.PublishMultiTransportAsync(original,
			new[] { new OutboundMessageTransport("pending", "durable") { Destination = "durable-events" } }, CancellationToken.None);
		if (historicalAlias) { row.MessageType = "arb-durable-event-restoration-old"; }
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { row });
		var result = await publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.FailureCount.ShouldBe(0);
		sent.ShouldNotBeNull();
		var restored = serializer.DeserializeObject(sent.Value, typeof(DurableEvent)).ShouldBeOfType<DurableEvent>();
		restored.OrderId.ShouldBe(original.OrderId);
		restored.Quantity.ShouldBe(7);
		A.CallTo(() => producer.ProduceAsync("durable-events", A<Message<string, byte[]>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => ((IMultiTransportOutboxStore)store).MarkTransportSentAsync(row.Id, "durable", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[MessageName("arb-durable-event-restoration")]
	[MessageNameAlias("arb-durable-event-restoration-old")]
	private sealed class DurableEvent : IIntegrationEvent
	{
		public Guid OrderId { get; set; }
		public int Quantity { get; set; }
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	public async Task RetainUndecodableRowsWithoutBlockingHealthyRows(int failure)
	{
		Excalibur.Dispatch.Delivery.Registry.MessageTypeRegistry.RegisterType<DurableEvent>();
		Excalibur.Dispatch.Delivery.Registry.MessageTypeRegistry.RegisterType(typeof(string));
		Excalibur.Dispatch.Delivery.Registry.MessageTypeRegistry.RegisterType<AmbiguousFirst>();
		Excalibur.Dispatch.Delivery.Registry.MessageTypeRegistry.RegisterType<AmbiguousSecond>();
		var typeName = failure switch
		{
			0 => "UNREGISTERED-OUTBOX-TYPE-9977",
			1 => typeof(string).AssemblyQualifiedName!,
			5 => "arb-outbox-ambiguous-identity",
			_ => typeof(DurableEvent).AssemblyQualifiedName!,
		};
		var bad = new OutboundMessage(typeName, [1], "bad");
		var healthy = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, [2], "healthy");
		A.CallTo(() => _serializer.DeserializeObject(bad.Payload, typeof(DurableEvent)))
			.Returns(failure == 3 ? new TestMessage() : null!);
		if (failure == 4)
		{
			A.CallTo(() => _serializer.DeserializeObject(bad.Payload, typeof(DurableEvent)))
				.Throws(new InvalidDataException("CORRUPT-PERSISTED-PAYLOAD"));
		}
		A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { bad, healthy });
		A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(MessageResult.Success());
		var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.FailureCount.ShouldBe(1);
		result.SuccessCount.ShouldBe(1);
		A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _outboxStore.MarkSentAsync(bad.Id, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _outboxStore.MarkFailedAsync(bad.Id, A<string>._, 1, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		if (failure is 0 or 1 or 5)
		{
			A.CallTo(() => _serializer.DeserializeObject(bad.Payload, A<Type>._)).MustNotHaveHappened();
		}
	}

	[MessageName("arb-outbox-ambiguous-identity")]
	private sealed class AmbiguousFirst : IDispatchEvent;
	[MessageName("arb-outbox-ambiguous-identity")]
	private sealed class AmbiguousSecond : IDispatchEvent;

	[Fact]
	public async Task PreserveCustomAdaptersThatAcceptTheBareDispatchMessageContract()
	{
		var original = new BareDispatchMessage();
		A.CallTo(() => _serializer.SerializeObject(original, typeof(BareDispatchMessage))).Returns([1]);
		A.CallTo(() => _serializer.DeserializeObject(A<byte[]>._, typeof(BareDispatchMessage))).Returns(new BareDispatchMessage());
		var row = await _publisher.PublishAsync(original, "custom", null, CancellationToken.None);
		A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { row });
		A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>.That.Matches(message => message is BareDispatchMessage), A<IMessageContext>._, A<CancellationToken>._))
			.Returns(MessageResult.Success());
		var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(0);
		row.MessageType.ShouldBe($"{typeof(BareDispatchMessage).FullName}, {typeof(BareDispatchMessage).Assembly.GetName().Name}");
		row.MessageType.ShouldNotContain("Version=");
		A.CallTo(() => _outboxStore.MarkSentAsync(row.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	private sealed class BareDispatchMessage : IDispatchMessage;

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectNonMessagesBeforeSerializationOrStorage(bool multiTransport)
	{
		if (multiTransport)
		{
			var store = A.Fake<IOutboxStore>(o => o.Implements<IMultiTransportOutboxStore>()).WithHonestCapabilities();
			var publisher = new MessageBusOutboxPublisher(store, _serializer, new TransportRegistry(), _serviceProvider, _logger);
			await Should.ThrowAsync<ArgumentException>(() => publisher.PublishMultiTransportAsync(new object(),
				new[] { new OutboundMessageTransport("pending", "durable") }, CancellationToken.None));
			A.CallTo(() => ((IMultiTransportOutboxStore)store).StageMessageWithTransportsAsync(
				A<OutboundMessage>._, A<IEnumerable<OutboundMessageTransport>>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
		else
		{
			await Should.ThrowAsync<ArgumentException>(() => _publisher.PublishAsync(new object(), "destination", null, CancellationToken.None));
		}
		A.CallTo(() => _serializer.SerializeObject(A<object>._, A<Type>._)).MustNotHaveHappened();
		A.CallTo(() => _outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseUnknownOrCorruptRowsBeforeCallingATransport(bool corrupt)
	{
		var store = A.Fake<IOutboxStore>(o => o.Implements<IMultiTransportOutboxStore>()
			.Implements<IMultiTransportOutboxStoreAdmin>().Implements<IOutboxStoreAdmin>()).WithHonestCapabilities();
		var multi = store.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var row = new OutboundMessage(corrupt ? OutboxFakeCapabilityExtensions.RegisteredMessageType : "UNKNOWN-MULTI-TYPE-9444", [1], "destination")
		{ IsMultiTransport = true };
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "remote"));
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { row });
		if (corrupt)
		{
			A.CallTo(() => _serializer.DeserializeObject(row.Payload, A<Type>._)).Throws(new InvalidDataException("CORRUPT-MULTI-PAYLOAD"));
		}
		var adapter = A.Fake<ITransportAdapter>();
		var registry = new TransportRegistry();
		registry.RegisterTransport("remote", adapter, "test", TransportLocality.Remote);
		var publisher = new MessageBusOutboxPublisher(store, _serializer, registry, _serviceProvider, _logger);
		var result = await publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.FailureCount.ShouldBe(1);
		A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => multi.MarkTransportSentAsync(row.Id, "remote", A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => multi.MarkTransportFailedAsync(row.Id, "remote", A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IsolateDecodedMessagesAndRetryOwnershipAcrossOverlappingTransports()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDispatch(dispatch => dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson()));
		await using var provider = services.BuildServiceProvider();
		var store = A.Fake<IOutboxStore>(o => o.Implements<IMultiTransportOutboxStore>()
			.Implements<IMultiTransportOutboxStoreAdmin>().Implements<IOutboxStoreAdmin>()).WithHonestCapabilities();
		var multi = store.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var registry = new TransportRegistry();
		var first = A.Fake<ITransportAdapter>();
		var second = A.Fake<ITransportAdapter>();
		registry.RegisterTransport("first", first, "test", TransportLocality.Remote);
		registry.RegisterTransport("second", second, "test", TransportLocality.Remote);
		var publisher = new MessageBusOutboxPublisher(store, provider.GetRequiredService<IPayloadSerializer>(), registry, provider, _logger);
		var row = await publisher.PublishMultiTransportAsync(new DurableEvent { Quantity = 7 },
			new[] { new OutboundMessageTransport("pending", "first"), new OutboundMessageTransport("pending", "second") }, CancellationToken.None);
		row.MessageType.ShouldBe("arb-durable-event-restoration");
		row.MessageType.ShouldNotContain("Version=");
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { row });
		var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var mutated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var entered = 0;
		var ownedAttempts = 0;
		var nestedAttempts = 0;
		IDispatchMessage? firstMessage = null, secondMessage = null;
		IMessageContext? firstContext = null, secondContext = null;
		var middleware = new RetryMiddleware(Microsoft.Extensions.Options.Options.Create(new RetryOptions
		{
			MaxRetryAttempts = 1, BaseDelay = TimeSpan.Zero, UseJitter = false,
		}), NullTelemetrySanitizer.Instance, NullLogger<RetryMiddleware>.Instance);
		A.CallTo(() => first.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(async (IDispatchMessage message, string _, IMessageContext context, CancellationToken token) =>
			{
				firstMessage = message; firstContext = context;
				if (Interlocked.Increment(ref entered) == 2) { bothEntered.TrySetResult(); }
				await bothEntered.Task.WaitAsync(token);
				message.ShouldBeOfType<DurableEvent>().Quantity = 99;
				mutated.TrySetResult();
				var nested = new DurableEvent();
				await Should.ThrowAsync<IOException>(() => middleware.InvokeAsync(nested, new MessageContext(nested, provider), (_, _, _) =>
				{
					nestedAttempts++; throw new IOException("NESTED-INDEPENDENT-FAULT");
				}, token).AsTask());
				await middleware.InvokeAsync(message, context, (_, _, _) =>
				{
					ownedAttempts++; throw new IOException("OWNED-TRANSPORT-FAULT");
				}, token);
			});
		A.CallTo(() => second.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(async (IDispatchMessage message, string _, IMessageContext context, CancellationToken token) =>
			{
				secondMessage = message; secondContext = context;
				if (Interlocked.Increment(ref entered) == 2) { bothEntered.TrySetResult(); }
				await mutated.Task.WaitAsync(token);
				message.ShouldBeOfType<DurableEvent>().Quantity.ShouldBe(7);
			});
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await publisher.PublishPendingMessagesAsync(deadline.Token);
		firstMessage.ShouldNotBeSameAs(secondMessage);
		firstContext.ShouldNotBeSameAs(secondContext);
		ownedAttempts.ShouldBe(1);
		nestedAttempts.ShouldBe(2);
		A.CallTo(() => multi.MarkTransportSentAsync(row.Id, "second", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => multi.MarkTransportFailedAsync(row.Id, "first", A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PreserveAlreadyPersistedSiblingSuccessWhenAnotherTransportCancels()
	{
		var store = A.Fake<IOutboxStore>(o => o.Implements<IMultiTransportOutboxStore>()
			.Implements<IMultiTransportOutboxStoreAdmin>().Implements<IOutboxStoreAdmin>()).WithHonestCapabilities();
		var multi = store.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var row = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, [1], "destination") { IsMultiTransport = true };
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "sent"));
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "cancel"));
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { row });
		using var caller = new CancellationTokenSource();
		var sentPersisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => multi.MarkTransportSentAsync(row.Id, "sent", A<CancellationToken>._)).Invokes(() => sentPersisted.TrySetResult());
		var sent = A.Fake<ITransportAdapter>();
		var cancel = A.Fake<ITransportAdapter>();
		A.CallTo(() => cancel.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(async () =>
			{
				await sentPersisted.Task.WaitAsync(TimeSpan.FromSeconds(5));
				caller.Cancel();
				throw new InvalidOperationException("WRAPPED-CALLER-CANCELLATION", new OperationCanceledException(caller.Token));
			});
		var registry = new TransportRegistry();
		registry.RegisterTransport("sent", sent, "test", TransportLocality.Remote);
		registry.RegisterTransport("cancel", cancel, "test", TransportLocality.Remote);
		var publisher = new MessageBusOutboxPublisher(store, _serializer, registry, _serviceProvider, _logger);
		await Should.ThrowAsync<OperationCanceledException>(() => publisher.PublishPendingMessagesAsync(caller.Token));
		A.CallTo(() => multi.MarkTransportSentAsync(row.Id, "sent", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => multi.MarkTransportFailedAsync(A<string>._, A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => store.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task RetainMessageWhenAdapterReturnsFailureWithoutThrowing()
	{
		var message = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 1 }, "queue1");
		A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { message });
		A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(MessageResult.Failed(new MessageProblemDetails { Status = 503, Detail = "CONTROLLED-RETURNED-PUBLISH-REFUSAL" }));

		var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(1);
		A.CallTo(() => _outboxStore.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _outboxStore.MarkFailedAsync(message.Id, A<string>.That.Contains("CONTROLLED-RETURNED-PUBLISH-REFUSAL"), 1, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task KeepPersistedAdapterAttemptsFromMultiplyingTheMiddlewareBudget()
	{
		var row = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 1 }, "queue1");
		A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { row });
		var middleware = new RetryMiddleware(Microsoft.Extensions.Options.Options.Create(new RetryOptions
		{
			MaxRetryAttempts = 1, BaseDelay = TimeSpan.Zero, UseJitter = false,
		}), NullTelemetrySanitizer.Instance, NullLogger<RetryMiddleware>.Instance);
		var attempts = 0;
		A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily((IDispatchMessage message, IMessageContext context, CancellationToken token) =>
				middleware.InvokeAsync(message, context, (_, _, _) =>
				{
					attempts++;
					throw new IOException("CONTROLLED-ADAPTER-FAULT");
				}, token).AsTask());
		var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);
		attempts.ShouldBe(1);
		result.FailureCount.ShouldBe(1);
		result.SuccessCount.ShouldBe(0);
		A.CallTo(() => _outboxStore.MarkFailedAsync(row.Id, A<string>._, 1, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _outboxStore.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		var foreground = A.Fake<IDispatchAction>();
		var foregroundContext = new MessageContext(foreground, _serviceProvider);
		await Should.ThrowAsync<IOException>(() => middleware.InvokeAsync(foreground, foregroundContext, (_, _, _) =>
		{
			attempts++;
			throw new IOException("FOREGROUND-FAULT");
		}, CancellationToken.None).AsTask());
		attempts.ShouldBe(3);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PropagateCallerCancellationWithoutChargingThePersistedAdapterAttempt(bool returnedFailure)
	{
		var row = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 1 }, "queue1");
		A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { row });
		using var caller = new CancellationTokenSource();
		var publish = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes(() => caller.Cancel());
		if (returnedFailure)
		{
			publish.Returns(MessageResult.Failed(new MessageProblemDetails { Status = 500, Detail = "Canceled adapter returned failure" }));
		}
		else { publish.Throws(new OperationCanceledException(caller.Token)); }
		await Should.ThrowAsync<OperationCanceledException>(() => _publisher.PublishPendingMessagesAsync(caller.Token));
		A.CallTo(() => _outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _outboxStore.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task OwnEachTransportMiddlewareAttemptWithoutPreventingHealthySiblingDelivery()
	{
		var store = A.Fake<IOutboxStore>(options => options.Implements<IMultiTransportOutboxStore>()
			.Implements<IMultiTransportOutboxStoreAdmin>().Implements<IOutboxStoreAdmin>()).WithHonestCapabilities();
		var multi = store.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var row = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 1 }, "queue1") { IsMultiTransport = true };
		foreach (var name in new[] { "healthy", "fault" })
		{
			row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, name) { Status = TransportDeliveryStatus.Pending });
		}
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { row });
		var healthy = A.Fake<ITransportAdapter>();
		var fault = A.Fake<ITransportAdapter>();
		var middleware = new RetryMiddleware(Microsoft.Extensions.Options.Options.Create(new RetryOptions
		{
			MaxRetryAttempts = 1, BaseDelay = TimeSpan.Zero, UseJitter = false,
		}), NullTelemetrySanitizer.Instance, NullLogger<RetryMiddleware>.Instance);
		var attempts = 0;
		A.CallTo(() => fault.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(async (IDispatchMessage message, string _, IMessageContext context, CancellationToken token) =>
			{
				await middleware.InvokeAsync(message, context, (_, _, _) =>
				{
					attempts++;
					throw new IOException("CONTROLLED-MULTI-FAULT");
				}, token);
			});
		var registry = new TransportRegistry();
		registry.RegisterTransport("healthy", healthy, "Healthy", TransportLocality.Remote);
		registry.RegisterTransport("fault", fault, "Fault", TransportLocality.Remote);
		var publisher = new MessageBusOutboxPublisher(store, _serializer, registry, _serviceProvider, _logger);
		_ = await publisher.PublishPendingMessagesAsync(CancellationToken.None);
		attempts.ShouldBe(1);
		A.CallTo(() => healthy.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => multi.MarkTransportSentAsync(row.Id, "healthy", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => multi.MarkTransportFailedAsync(row.Id, "fault", A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PropagateWrappedTransportCancellationWithoutChargingEitherFailureStore()
	{
		var store = A.Fake<IOutboxStore>(options => options.Implements<IMultiTransportOutboxStore>()
			.Implements<IMultiTransportOutboxStoreAdmin>().Implements<IOutboxStoreAdmin>()).WithHonestCapabilities();
		var multi = store.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var row = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 1 }, "queue1") { IsMultiTransport = true };
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "fault") { Status = TransportDeliveryStatus.Pending });
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { row });
		using var caller = new CancellationTokenSource();
		var fault = A.Fake<ITransportAdapter>();
		A.CallTo(() => fault.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes(() => caller.Cancel()).Throws(new InvalidOperationException("wrapped cancellation", new OperationCanceledException(caller.Token)));
		var registry = new TransportRegistry();
		registry.RegisterTransport("fault", fault, "Fault", TransportLocality.Remote);
		var publisher = new MessageBusOutboxPublisher(store, _serializer, registry, _serviceProvider, _logger);
		await Should.ThrowAsync<OperationCanceledException>(() => publisher.PublishPendingMessagesAsync(caller.Token));
		A.CallTo(() => multi.MarkTransportFailedAsync(A<string>._, A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => store.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task RetainAnIndependentOperationCancellationAsARealFailureWhenTheCallerIsNotCanceled()
	{
		var row = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 1 }, "queue1");
		A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(new[] { row });
		A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Throws(new OperationCanceledException("Independent adapter deadline"));
		var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.FailureCount.ShouldBe(1);
		A.CallTo(() => _outboxStore.MarkFailedAsync(row.Id, A<string>._, 1, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StageMessageInOutboxStore()
	{
		// Arrange
		var message = new TestMessage { Content = "test" };
		var destination = "test-queue";
		var serializedPayload = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _serializer.SerializeObject(A<object>._, A<Type>._)).Returns(serializedPayload);

		// Act
		var result = await _publisher.PublishAsync(message, destination, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Destination.ShouldBe(destination);
		result.Status.ShouldBe(OutboxStatus.Staged);
		result.Payload.ShouldBe(serializedPayload);

		_ = A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>.That.Matches(m => m.Destination == destination),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StageScheduledMessage()
	{
		// Arrange
		var message = new TestMessage { Content = "test" };
		var destination = "test-queue";
		var scheduledAt = DateTimeOffset.UtcNow.AddHours(1);

		_ = A.CallTo(() => _serializer.SerializeObject(A<object>._, A<Type>._)).Returns(new byte[] { 1 });

		// Act
		var result = await _publisher.PublishAsync(message, destination, scheduledAt, CancellationToken.None);

		// Assert
		result.ScheduledAt.ShouldBe(scheduledAt);
	}

	[Fact]
	public async Task ThrowWhenMessageIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _publisher.PublishAsync(null!, "destination", null, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenDestinationIsNullOrEmpty()
	{
		// Arrange
		var message = new TestMessage { Content = "test" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _publisher.PublishAsync(message, null!, null, CancellationToken.None));

		_ = await Should.ThrowAsync<ArgumentException>(
			() => _publisher.PublishAsync(message, "", null, CancellationToken.None));
	}

	[Fact]
	public async Task PublishPendingMessagesFromStore()
	{
		// Arrange
		var messages = new List<OutboundMessage>
		{
			new(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 1 }, "queue1"),
			new(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 2 }, "queue2")
		};

		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(messages);

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(MessageResult.Success());

		// Act
		var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(2);
		result.FailureCount.ShouldBe(0);
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task MarkMessagesAsSentAfterPublishing()
	{
		// Arrange
		var message = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 1 }, "queue1");

		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(MessageResult.Success());

		// Act
		_ = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _outboxStore.MarkSentAsync(message.Id, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkMessagesAsFailedOnError()
	{
		// Arrange
		var message = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 1 }, "queue1");

		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Bus error"));

		// Act
		var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(1);
		result.IsSuccess.ShouldBeFalse();

		_ = A.CallTo(() => _outboxStore.MarkFailedAsync(
			message.Id,
			A<string>.That.Contains("Bus error"),
			A<int>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RetryFailedMessagesWithinMaxRetries()
	{
		// Arrange
		var message = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, new byte[] { 1 }, "queue1")
		{
			Status = OutboxStatus.Failed,
			RetryCount = 1
		};

		// The retry pass drains through the atomic claim, never a plain read of failed rows: the claim is what
		// admits a failed message once its backoff floor has elapsed, and it is what stops a second dispatcher
		// publishing the same row.
		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(100, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(MessageResult.Success());

		// Act
		var result = await _publisher.RetryFailedMessagesAsync(3, CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(1);

		// The unclaimed read must not be reachable from a path that dispatches.
		A.CallTo(() => ((IOutboxStoreAdmin)_outboxStore).GetAllTenantsFailedMessagesAsync(
				A<int>._, A<DateTimeOffset?>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowWhenMaxRetriesIsNegative()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _publisher.RetryFailedMessagesAsync(-1, CancellationToken.None));
	}

	[Fact]
	public void ReturnAccurateStatistics()
	{
		// Act
		var stats = _publisher.GetStatistics();

		// Assert
		_ = stats.ShouldNotBeNull();
		stats.TotalOperations.ShouldBe(0);
		stats.TotalMessagesPublished.ShouldBe(0);
		stats.TotalMessagesFailed.ShouldBe(0);
		stats.CurrentSuccessRate.ShouldBe(100.0);
	}

	[Fact]
	public async Task PublishPendingTransportDeliveries_WhenAdapterMissing_Throws()
	{
		// Arrange
		var multiStore = A.Fake<IOutboxStore>(o =>
		{
			_ = o.Implements<IMultiTransportOutboxStore>();
			_ = o.Implements<IMultiTransportOutboxStoreAdmin>();
		});
		var transportRegistry = new TransportRegistry();
		var publisher = new MessageBusOutboxPublisher(
			multiStore,
			_serializer,
			transportRegistry,
			_serviceProvider,
			_logger);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10));
	}

	[Fact]
	public async Task PublishPendingTransportDeliveries_MarksTransportSent_OnSuccess()
	{
		// Arrange
		var multiStoreBase = A.Fake<IOutboxStore>(o =>
		{
			_ = o.Implements<IMultiTransportOutboxStore>();
			_ = o.Implements<IMultiTransportOutboxStoreAdmin>();
		}).WithHonestCapabilities();
		var multiStore = multiStoreBase.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var multiStoreAdmin = multiStoreBase.ShouldBeAssignableTo<IMultiTransportOutboxStoreAdmin>();

		var adapter = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var transportRegistry = new TransportRegistry();
		transportRegistry.RegisterTransport("kafka", adapter, "Kafka", TransportLocality.Remote);

		var publisher = new MessageBusOutboxPublisher(
			multiStoreBase,
			_serializer,
			transportRegistry,
			_serviceProvider,
			_logger);

		var message = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, [1, 2, 3], "orders-default");
		var transport = new OutboundMessageTransport(message.Id, "kafka")
		{
			Destination = "orders-topic"
		};

		_ = A.CallTo(() => multiStoreAdmin.GetPendingTransportDeliveriesAsync("kafka", 10, A<CancellationToken>._))
			.Returns(new[] { (message, transport) });

		// Act
		var result = await publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10);

		// Assert
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(0);
		_ = A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, "orders-topic", A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => multiStore.MarkTransportSentAsync(message.Id, "kafka", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishPendingTransportDeliveries_MarksTransportFailed_OnAdapterException()
	{
		// Arrange
		var multiStoreBase = A.Fake<IOutboxStore>(o =>
		{
			_ = o.Implements<IMultiTransportOutboxStore>();
			_ = o.Implements<IMultiTransportOutboxStoreAdmin>();
		}).WithHonestCapabilities();
		var multiStore = multiStoreBase.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var multiStoreAdmin = multiStoreBase.ShouldBeAssignableTo<IMultiTransportOutboxStoreAdmin>();

		var adapter = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("transport unavailable"));

		var transportRegistry = new TransportRegistry();
		transportRegistry.RegisterTransport("kafka", adapter, "Kafka", TransportLocality.Remote);

		var publisher = new MessageBusOutboxPublisher(
			multiStoreBase,
			_serializer,
			transportRegistry,
			_serviceProvider,
			_logger);

		var message = new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, [9, 9], "orders-default");
		var transport = new OutboundMessageTransport(message.Id, "kafka")
		{
			Destination = "orders-topic"
		};

		_ = A.CallTo(() => multiStoreAdmin.GetPendingTransportDeliveriesAsync("kafka", 10, A<CancellationToken>._))
			.Returns(new[] { (message, transport) });

		// Act
		var result = await publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10);

		// Assert
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(1);
		result.Errors.ShouldContain(error => error.MessageId == message.Id);
		_ = A.CallTo(() => multiStore.MarkTransportFailedAsync(
				message.Id,
				"kafka",
				A<string>.That.Contains("transport unavailable"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishMultiTransportAsync_Should_SetTargetTransportsCsvInInputOrder()
	{
		// Arrange
		var multiStoreBase = A.Fake<IOutboxStore>(o => o.Implements<IMultiTransportOutboxStore>()).WithHonestCapabilities();
		var multiStore = multiStoreBase.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var serializer = OutboxFakeCapabilityExtensions.CreateRegisteredSerializer();
		var messageBus = A.Fake<IMessageBusAdapter>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<MessageBusOutboxPublisher>>();
		var publisher = new MessageBusOutboxPublisher(
			multiStoreBase,
			serializer,
			messageBus,
			serviceProvider,
			logger);

		var payload = new byte[] { 1, 2, 3, 4 };
		_ = A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).Returns(payload);

		var transports = new[]
		{
			new OutboundMessageTransport("msg-1", "sns") { Destination = "topic-a" },
			new OutboundMessageTransport("msg-1", "sqs") { Destination = "queue-b" },
			new OutboundMessageTransport("msg-1", "grpc") { Destination = "endpoint-c" }
		};

		// Act
		var result = await publisher.PublishMultiTransportAsync(new TestMessage { Content = "payload" }, transports, CancellationToken.None);

		// Assert
		result.TargetTransports.ShouldBe("sns,sqs,grpc");
		result.TransportDeliveries.Count.ShouldBe(3);
		_ = A.CallTo(() => multiStore.StageMessageWithTransportsAsync(
				A<OutboundMessage>.That.Matches(m => m.TargetTransports == "sns,sqs,grpc"),
				A<IEnumerable<OutboundMessageTransport>>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private sealed class TestMessage : IDispatchEvent
	{
		public string Content { get; set; } = string.Empty;
	}
}
