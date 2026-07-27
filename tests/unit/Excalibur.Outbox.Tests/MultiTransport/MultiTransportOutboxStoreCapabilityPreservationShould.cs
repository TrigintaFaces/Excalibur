// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MultiTransport;

namespace Excalibur.Outbox.Tests.MultiTransport;

// Capability-preservation lock for MultiTransportOutboxStore (author == impl regression lock).
//
// THE PROPERTY. MultiTransportOutboxStore wraps an inner IOutboxStore. A consumer discovers an optional
// capability by asking the store it was handed: outboxStore.GetService(typeof(IFencedOutboxStore)). The
// OutboxProcessor's split-brain guard does exactly this at startup (OutboxProcessor.cs:258) and fails closed
// if it resolves null. If the router wrapper does NOT forward capability resolution to the inner store, a
// consumer whose configured store is fencing-capable LOSES fencing the moment the router wraps it — silently.
//
// The fix makes the router a real OutboxStoreDecorator, whose GetService defers unknown capabilities to the
// inner store. These arms resolve the router THROUGH THE REAL DI CONTAINER (the production AddMultiTransportOutbox
// registration path) and assert the property, not the mechanism.
//
// NON-VACUITY. The pre-fix router relied on IOutboxStore's default GetService (return this if assignable, else
// null) and did not implement IFencedOutboxStore, so the fencing probe returned null. Reverting the decorator
// forwarding (e.g. overriding GetService back to `serviceType.IsInstanceOfType(this) ? this : null`) turns the
// LIVENESS arm RED while the SAFETY arm stays GREEN.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class MultiTransportOutboxStoreCapabilityPreservationShould
{
	private static ServiceProvider BuildProviderWith(IOutboxStore innerStore)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddKeyedSingleton<IOutboxStore>("default", innerStore);
		_ = services.AddMultiTransportOutbox(_ => { });
		return services.BuildServiceProvider();
	}

	[Fact]
	public void PreserveFencingCapability_WhenTheInnerStoreIsFencingCapable()
	{
		// LIVENESS (AC-B1.3 / AC-B1.4). A fencing-capable inner store must remain discoverable as
		// IFencedOutboxStore THROUGH the router, and it must be the inner store's OWN fencing capability
		// (not a fabricated one) so the operations delegate to real fencing.
		var fenced = new FencedInnerStore();
		using var provider = BuildProviderWith(fenced);

		var router = provider.GetRequiredService<IMultiTransportOutboxRouter>();

		var resolved = router.GetService(typeof(IFencedOutboxStore));

		resolved.ShouldNotBeNull(
			"The router wrapped a fencing-capable store but GetService(IFencedOutboxStore) returned null, so the " +
			"OutboxProcessor split-brain guard would fail closed and fencing is silently lost through the wrap.");
		resolved.ShouldBeSameAs(
			fenced,
			"The resolved fencing capability must be the inner store itself, so fencing operations delegate to the " +
			"inner store's real high-water-mark enforcement rather than a fabricated view.");
		_ = resolved.ShouldBeAssignableTo<IFencedOutboxStore>();
	}

	[Fact]
	public void NotFabricateFencingCapability_WhenTheInnerStoreIsNotFencingCapable()
	{
		// SAFETY (AC-B1.2). A non-fencing inner store must resolve fencing to null through the router — the
		// wrapper must never advertise a capability its inner store cannot honor.
		var plain = new PlainInnerStore();
		using var provider = BuildProviderWith(plain);

		var router = provider.GetRequiredService<IMultiTransportOutboxRouter>();

		router.GetService(typeof(IFencedOutboxStore)).ShouldBeNull(
			"The router wrapped a non-fencing store yet advertised IFencedOutboxStore. A consumer reads a non-null " +
			"result as a promise the store can enforce fencing; fabricating it would present a token to a store " +
			"that cannot honor one.");
	}

	[Fact]
	public async Task StillRouteAndDelegateStaging_ThroughTheWrap()
	{
		// LIVENESS. The decorator must not be inert: it still applies transport routing and delegates staging to
		// the inner store. Proves the capability-preserving wrapper did not break the router's own behavior.
		var fenced = new FencedInnerStore();
		using var provider = BuildProviderWith(fenced);

		var router = provider.GetRequiredService<IMultiTransportOutboxRouter>();

		var message = new OutboundMessage
		{
			Id = "msg-cap-1",
			MessageType = "TestMessage",
			Payload = new byte[] { 1, 2, 3 },
			Destination = "dest",
			CreatedAt = DateTimeOffset.UtcNow,
			Status = OutboxStatus.Staged
		};

		await router.StageMessageAsync(message, CancellationToken.None);

		fenced.Staged.ShouldContain(message, "The router did not delegate StageMessageAsync to the inner store.");
		message.TargetTransports.ShouldBe("default", "The router did not apply a transport binding on stage.");
	}

	// ── concrete inner stores (rely on IOutboxStore's default GetService, exactly as a real leaf store does) ──────

	private sealed class PlainInnerStore : IOutboxStore
	{
		public List<OutboundMessage> Staged { get; } = [];

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
		{
			Staged.Add(message);
			return default;
		}

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
			default;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
			new(Enumerable.Empty<OutboundMessage>());

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) => default;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) =>
			default;
	}

	private sealed class FencedInnerStore : IOutboxStore, IFencedOutboxStore
	{
		public List<OutboundMessage> Staged { get; } = [];

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
		{
			Staged.Add(message);
			return default;
		}

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
			default;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
			new(Enumerable.Empty<OutboundMessage>());

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) => default;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) =>
			default;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, long fencingToken, CancellationToken cancellationToken) =>
			new(Enumerable.Empty<OutboundMessage>());

		public ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken) => default;
	}
}
