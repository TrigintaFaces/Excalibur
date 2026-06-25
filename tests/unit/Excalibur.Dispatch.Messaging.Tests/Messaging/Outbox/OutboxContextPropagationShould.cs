// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

/// <summary>
/// Author≠impl regression lock for Sprint 849 Lane R1 (<c>maqsxi</c>): the outbox→transport publish
/// path must propagate <c>TenantId</c> + <c>CausationId</c> onto the rebuilt outbound
/// <see cref="IMessageContext"/>, symmetric to the inbox RESTORE side
/// (<c>InboxProcessor.CreateFromMetadata</c>). Pre-fix, <see cref="MessageBusOutboxPublisher"/> rebuilt
/// the context with only <c>MessageId</c> + <c>CorrelationId</c> — silently dropping tenant routing and
/// the causation chain.
/// </summary>
/// <remarks>
/// Binds the EXACT accessors the round-trip serializer reads (<c>MessageMetadata.FromContext</c>:
/// <c>CausationId: context.CausationId</c>, <c>TenantId: context.GetTenantId()</c>), so the lock is
/// mechanism-agnostic about how the fix re-attaches them. Non-vacuity (RED on the pre-fix parent):
/// <c>GetTenantId()</c> returns <see langword="null"/> (no identity feature) and <c>CausationId</c> does
/// not equal the message's distinct causation value. Mirrors the producer-side harness in
/// <see cref="OutboxTraceparentPropagationShould"/>.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Feature", "Outbox")]
public sealed class OutboxContextPropagationShould
{
	private const string Corr = "corr-11111111";
	private const string Cause = "cause-22222222"; // deliberately != Corr to defeat the lazy CausationId default
	private const string Tenant = "tenant-33333333";

	[Fact]
	public async Task RestoreTenantIdAndCausationIdOntoSingleTransportContext()
	{
		// Arrange -- single-transport publish path (PublishSingleTransportMessageAsync).
		var store = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>());
		var messageBus = A.Fake<IMessageBusAdapter>();
		var publisher = new MessageBusOutboxPublisher(
			store, A.Fake<IPayloadSerializer>(), messageBus, A.Fake<IServiceProvider>(),
			NullLogger<MessageBusOutboxPublisher>.Instance);

		var message = new OutboundMessage("OrderCreated", [1], "queue-1")
		{
			CorrelationId = Corr,
			CausationId = Cause,
			TenantId = Tenant,
		};
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		IMessageContext? publishedContext = null;
		A.CallTo(() => messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext c, CancellationToken _) => publishedContext = c)
			.Returns(A.Fake<IMessageResult>());

		// Act
		await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert -- RED pre-fix: GetTenantId() null; CausationId == Corr (lazy default), not Cause.
		publishedContext.ShouldNotBeNull();
		publishedContext.GetTenantId().ShouldBe(Tenant);
		publishedContext.CausationId.ShouldBe(Cause);
	}

	[Fact]
	public async Task RestoreTenantIdAndCausationIdOntoMultiTransportContext()
	{
		// Arrange -- multi-transport per-transport publish path (PublishToTransportAsync).
		var multiStoreBase = A.Fake<IOutboxStore>(o =>
		{
			_ = o.Implements<IMultiTransportOutboxStore>();
			_ = o.Implements<IMultiTransportOutboxStoreAdmin>();
		});
		var multiStoreAdmin = multiStoreBase.ShouldBeAssignableTo<IMultiTransportOutboxStoreAdmin>();

		var adapter = A.Fake<ITransportAdapter>();
		IMessageContext? sentContext = null;
		A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, string _, IMessageContext c, CancellationToken _) => sentContext = c)
			.Returns(Task.CompletedTask);

		var transportRegistry = new TransportRegistry();
		transportRegistry.RegisterTransport("kafka", adapter, "Kafka");

		var publisher = new MessageBusOutboxPublisher(
			multiStoreBase, A.Fake<IPayloadSerializer>(), transportRegistry, A.Fake<IServiceProvider>(),
			NullLogger<MessageBusOutboxPublisher>.Instance);

		var message = new OutboundMessage("OrderCreated", [1, 2, 3], "orders-default")
		{
			CorrelationId = Corr,
			CausationId = Cause,
			TenantId = Tenant,
		};
		var transport = new OutboundMessageTransport(message.Id, "kafka") { Destination = "orders-topic" };
		A.CallTo(() => multiStoreAdmin.GetPendingTransportDeliveriesAsync("kafka", 10, A<CancellationToken>._))
			.Returns(new[] { (message, transport) });

		// Act
		await publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10);

		// Assert -- EC-R1.2: both publish sites symmetric.
		sentContext.ShouldNotBeNull();
		sentContext.GetTenantId().ShouldBe(Tenant);
		sentContext.CausationId.ShouldBe(Cause);
	}

	[Fact]
	public async Task LeaveTenantIdAbsentWhenMessageHasNone()
	{
		// EC-R1.1: a null TenantId must propagate as ABSENT (null), never fabricated as empty-string.
		var store = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>());
		var messageBus = A.Fake<IMessageBusAdapter>();
		var publisher = new MessageBusOutboxPublisher(
			store, A.Fake<IPayloadSerializer>(), messageBus, A.Fake<IServiceProvider>(),
			NullLogger<MessageBusOutboxPublisher>.Instance);

		var message = new OutboundMessage("OrderCreated", [1], "queue-1") { CorrelationId = Corr };
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		IMessageContext? publishedContext = null;
		A.CallTo(() => messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext c, CancellationToken _) => publishedContext = c)
			.Returns(A.Fake<IMessageResult>());

		// Act
		await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert -- absent, not empty-string.
		publishedContext.ShouldNotBeNull();
		publishedContext.GetTenantId().ShouldBeNull();
	}
}
