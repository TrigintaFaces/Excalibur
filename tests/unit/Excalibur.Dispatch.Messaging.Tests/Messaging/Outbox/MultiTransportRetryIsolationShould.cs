// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

/// <summary>
/// Regression lock: multi-transport outbox retry is <strong>isolated per transport</strong>. When a message
/// fans out to several transports, one transport's delivery state (Sent / Failed) MUST NOT affect another
/// transport's retry decision. Concretely: a retry re-sends ONLY the still-<c>Pending</c> transports and
/// NEVER re-sends a transport already marked <c>Sent</c>, and one transport failing repeatedly does NOT
/// block, reset, or corrupt a healthy transport's dispatch.
///
/// <para>
/// Mechanism under test — <c>MessageBusOutboxPublisher.PublishMultiTransportMessageAsync</c>
/// (src/Dispatch/Excalibur.Dispatch/Outbox/MessageBusOutboxPublisher.cs):
/// </para>
/// <list type="bullet">
///   <item>the per-delivery <c>if (delivery.Status != TransportDeliveryStatus.Pending) continue;</c> filter
///     (line 739) — the structural guarantee that a <c>Sent</c> transport is never re-published; and</item>
///   <item>the demand-load of <c>TransportDeliveries</c> on the retry path via
///     <c>IMultiTransportOutboxStoreAdmin.GetAllTenantsTransportDeliveriesAsync</c> (lines 722-726) — so the
///     retry decides against per-transport state, not a shared/global flag. The drain claims across every
///     tenant, so this demand-load goes through the estate-wide admin read, not the tenant-confined consumer
///     read.</item>
/// </list>
///
/// <para>
/// NON-VACUITY — this lock fails RED if per-transport isolation regressed to shared/global retry state:
/// </para>
/// <list type="bullet">
///   <item>Remove the <c>!= Pending</c> filter (line 728) and a retry re-publishes the already-<c>Sent</c>
///     transport → the healthy adapter is invoked → <c>MustNotHaveHappened()</c> on it fails.</item>
///   <item>Remove the demand-load (lines 722-726) and the GetFailed retry path sees zero deliveries →
///     returns early → the sick transport is never retried → the "sick adapter invoked" assertion fails.</item>
///   <item>Short-circuit the fan-out on the first transport failure (a shared failure flag) → the healthy
///     transport is never sent → the "healthy adapter sent + marked Sent" assertion fails.</item>
/// </list>
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class MultiTransportRetryIsolationShould
{
	private const string HealthyTransport = "healthy-transport";
	private const string SickTransport = "sick-transport";

	private readonly IPayloadSerializer _serializer = A.Fake<IPayloadSerializer>();
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();
	private readonly ILogger<MessageBusOutboxPublisher> _logger = A.Fake<ILogger<MessageBusOutboxPublisher>>();

	private static IOutboxStore CreateMultiTransportStore() =>
		A.Fake<IOutboxStore>(o =>
		{
			_ = o.Implements<IMultiTransportOutboxStore>();
			_ = o.Implements<IMultiTransportOutboxStoreAdmin>();
			_ = o.Implements<IOutboxStoreAdmin>();
		}).WithHonestCapabilities();

	private (ITransportAdapter Healthy, ITransportAdapter Sick, TransportRegistry Registry) CreateTransports()
	{
		var healthy = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => healthy.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var sick = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => sick.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("sick transport unavailable"));

		var registry = new TransportRegistry();
		registry.RegisterTransport(HealthyTransport, healthy, "Healthy", TransportLocality.Remote);
		registry.RegisterTransport(SickTransport, sick, "Sick", TransportLocality.Remote);

		return (healthy, sick, registry);
	}

	/// <summary>
	/// GetUnsent retry path (deliveries eager-loaded on the message): a transport already <c>Sent</c> is
	/// NEVER re-published; only the <c>Pending</c> transport is retried, and its repeated failure stays
	/// isolated to itself.
	/// </summary>
	[Fact]
	public async Task NeverReSendAnAlreadySentTransport_OnGetUnsentRetry()
	{
		// Arrange
		var storeBase = CreateMultiTransportStore();
		var store = storeBase.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var (healthy, sick, registry) = CreateTransports();

		var publisher = new MessageBusOutboxPublisher(storeBase, _serializer, registry, _serviceProvider, _logger);

		var message = new OutboundMessage("OrderCreated", [1, 2, 3], "orders")
		{
			IsMultiTransport = true
		};
		// healthy is already Sent from a prior partial delivery; sick is still Pending.
		message.TransportDeliveries.Add(new OutboundMessageTransport(message.Id, HealthyTransport)
		{
			Destination = "healthy-dest",
			Status = TransportDeliveryStatus.Sent
		});
		message.TransportDeliveries.Add(new OutboundMessageTransport(message.Id, SickTransport)
		{
			Destination = "sick-dest",
			Status = TransportDeliveryStatus.Pending
		});

		_ = A.CallTo(() => storeBase.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		// Act
		_ = await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert — the already-Sent transport is NEVER touched by the retry (isolation).
		A.CallTo(() => healthy.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => store.MarkTransportSentAsync(message.Id, HealthyTransport, A<CancellationToken>._))
			.MustNotHaveHappened();

		// Assert — only the still-Pending transport is retried, and its failure marks ONLY itself failed.
		_ = A.CallTo(() => sick.SendAsync(A<IDispatchMessage>._, "sick-dest", A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => store.MarkTransportFailedAsync(
				message.Id, SickTransport, A<string>.That.Contains("sick transport unavailable"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => store.MarkTransportFailedAsync(
				message.Id, HealthyTransport, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// GetFailed retry path (deliveries demand-loaded from the store): same isolation guarantee, exercising
	/// the demand-load branch. If demand-load regressed, the sick transport would never be retried.
	/// </summary>
	[Fact]
	public async Task NeverReSendAnAlreadySentTransport_OnGetFailedRetry_DemandLoadingDeliveries()
	{
		// Arrange
		var storeBase = CreateMultiTransportStore();
		var store = storeBase.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var multiAdmin = storeBase.ShouldBeAssignableTo<IMultiTransportOutboxStoreAdmin>();
		var (healthy, sick, registry) = CreateTransports();

		var publisher = new MessageBusOutboxPublisher(storeBase, _serializer, registry, _serviceProvider, _logger);

		// Message carries NO pre-loaded deliveries -> forces the demand-load branch. The drain claims across
		// every tenant, so the demand-load goes through the estate-wide admin read, not the tenant-confined
		// consumer read.
		var message = new OutboundMessage("OrderCreated", [4, 5, 6], "orders")
		{
			IsMultiTransport = true,
			Status = OutboxStatus.Failed,
			RetryCount = 1
		};

		var deliveries = new List<OutboundMessageTransport>
		{
			new(message.Id, HealthyTransport) { Destination = "healthy-dest", Status = TransportDeliveryStatus.Sent },
			new(message.Id, SickTransport) { Destination = "sick-dest", Status = TransportDeliveryStatus.Pending }
		};

		// The retry pass drains through the atomic claim, so the message reaches the transport under a lease.
		_ = A.CallTo(() => storeBase.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });
		_ = A.CallTo(() => multiAdmin.GetAllTenantsTransportDeliveriesAsync(message.Id, A<CancellationToken>._))
			.Returns(deliveries);

		// Act
		_ = await publisher.RetryFailedMessagesAsync(3, CancellationToken.None);

		// Assert — the already-Sent transport is NEVER re-published on the demand-load retry path.
		A.CallTo(() => healthy.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		// Assert — the demand-loaded Pending transport IS retried (proves demand-load ran).
		_ = A.CallTo(() => sick.SendAsync(A<IDispatchMessage>._, "sick-dest", A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => store.MarkTransportFailedAsync(
				message.Id, SickTransport, A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// Cross-transport isolation: a transport that fails repeatedly does NOT block, reset, or corrupt a
	/// healthy transport's dispatch. Both are Pending; the healthy one still publishes (and is marked Sent)
	/// while the sick one fails (and is marked Failed) — proving retry/failure state is per-transport, not
	/// a shared flag that short-circuits the fan-out.
	/// </summary>
	[Fact]
	public async Task HealthyTransportStillPublishes_WhenAnotherTransportKeepsFailing()
	{
		// Arrange
		var storeBase = CreateMultiTransportStore();
		var store = storeBase.ShouldBeAssignableTo<IMultiTransportOutboxStore>();
		var (healthy, sick, registry) = CreateTransports();

		var publisher = new MessageBusOutboxPublisher(storeBase, _serializer, registry, _serviceProvider, _logger);

		var message = new OutboundMessage("OrderCreated", [7, 8, 9], "orders")
		{
			IsMultiTransport = true
		};
		// BOTH pending: the sick one fails, the healthy one must succeed regardless.
		message.TransportDeliveries.Add(new OutboundMessageTransport(message.Id, HealthyTransport)
		{
			Destination = "healthy-dest",
			Status = TransportDeliveryStatus.Pending
		});
		message.TransportDeliveries.Add(new OutboundMessageTransport(message.Id, SickTransport)
		{
			Destination = "sick-dest",
			Status = TransportDeliveryStatus.Pending
		});

		_ = A.CallTo(() => storeBase.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		// Act
		_ = await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert — the healthy transport publishes and is marked Sent, unaffected by the sick failure.
		_ = A.CallTo(() => healthy.SendAsync(A<IDispatchMessage>._, "healthy-dest", A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => store.MarkTransportSentAsync(message.Id, HealthyTransport, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Assert — the sick transport's failure is isolated to itself.
		_ = A.CallTo(() => sick.SendAsync(A<IDispatchMessage>._, "sick-dest", A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => store.MarkTransportFailedAsync(
				message.Id, SickTransport, A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => store.MarkTransportSentAsync(message.Id, SickTransport, A<CancellationToken>._))
			.MustNotHaveHappened();
	}
}
