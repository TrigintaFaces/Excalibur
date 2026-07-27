// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

/// <summary>
/// Independent behavioral regression lock (author≠impl) for the m084l4 outbox-read ingress: the outbox
/// publisher must reject a stored message whose payload exceeds the configured maximum <em>before</em>
/// handing it to the transport — fail-closed, so an over-limit body is marked failed and never dispatched.
/// The receive-side (RabbitMQ) ingress is covered separately; inbox-read was ruled already-covered at receive.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Feature", "Outbox")]
public sealed class OutboxPayloadSizeGuardShould
{
	private const int MaxBytes = 4;

	private static IServiceProvider ServiceProviderWithLimit(int? maxPayloadBytes)
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(Microsoft.Extensions.Options.IOptions<OutboxDeliveryOptions>)))
			.Returns(Microsoft.Extensions.Options.Options.Create(
				new OutboxDeliveryOptions { MaxPayloadBytes = maxPayloadBytes }));
		return serviceProvider;
	}

	private static (MessageBusOutboxPublisher Publisher, ITransportAdapter Adapter)
		CreatePublisher(byte[] payload, int? maxPayloadBytes)
	{
		var store = A.Fake<IOutboxStore>(o =>
		{
			_ = o.Implements<IMultiTransportOutboxStore>();
			_ = o.Implements<IMultiTransportOutboxStoreAdmin>();
		}).WithHonestCapabilities();
		var admin = store.ShouldBeAssignableTo<IMultiTransportOutboxStoreAdmin>();

		var adapter = A.Fake<ITransportAdapter>();
		A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var registry = new TransportRegistry();
		registry.RegisterTransport("kafka", adapter, "Kafka", TransportLocality.Remote);

		var publisher = new MessageBusOutboxPublisher(
			store, A.Fake<IPayloadSerializer>(), registry, ServiceProviderWithLimit(maxPayloadBytes),
			NullLogger<MessageBusOutboxPublisher>.Instance);

		var message = new OutboundMessage("OrderCreated", payload, "orders-default");
		var transport = new OutboundMessageTransport(message.Id, "kafka") { Destination = "orders-topic" };
		A.CallTo(() => admin.GetPendingTransportDeliveriesAsync("kafka", 10, A<CancellationToken>._))
			.Returns(new[] { (message, transport) });

		return (publisher, adapter);
	}

	[Fact]
	public async Task Reject_a_stored_message_whose_payload_exceeds_the_limit()
	{
		// Payload length 5 > MaxBytes 4 → rejected before dispatch.
		var (publisher, adapter) = CreatePublisher(new byte[] { 1, 2, 3, 4, 5 }, MaxBytes);

		await publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10);

		// Non-vacuity: pre-fix the oversized body was handed straight to the transport. Now the guard rejects
		// it before dispatch (fail-closed), so the transport is never asked to send it.
		A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Dispatch_a_stored_message_at_the_limit()
	{
		// Payload length 4 == MaxBytes 4 → allowed (boundary: only strictly-greater is rejected).
		var (publisher, adapter) = CreatePublisher(new byte[] { 1, 2, 3, 4 }, MaxBytes);

		await publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10);

		A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Opt_out_when_the_limit_is_null_even_for_a_large_payload()
	{
		var (publisher, adapter) = CreatePublisher(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, maxPayloadBytes: null);

		await publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10);

		A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
