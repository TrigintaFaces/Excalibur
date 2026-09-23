// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

/// <summary>
/// Locks the publisher drain to the fenced claim and mark-sent members whenever a leadership tenure and a
/// fencing-capable store are both present.
/// </summary>
/// <remarks>
/// <para>
/// The background service checks <c>ShouldProcess</c> before it drains, and that check is not a fence. It is
/// check-then-act: a dispatcher that is paused past the end of its tenure — a long GC, a stalled host —
/// resumes, reads its own stale leadership snapshot, and writes. The fencing token is the only value the
/// store can compare against its durable high-water, so a drain that never presents one cannot be refused,
/// and a superseded leader publishes messages the live leader has already claimed.
/// </para>
/// <para>
/// The store capability is discovered through the store's own <c>GetService</c> seam rather than a cast, so
/// the fake is given honest capability answers — a bare fake reports a capability it genuinely implements as
/// absent, which would make the safety arm pass for the wrong reason.
/// </para>
/// <para>
/// The two liveness arms are load-bearing. Without them a publisher that fenced unconditionally, or one that
/// threw on every drain, would satisfy the safety arms — so they pin the two configurations in which draining
/// through the unfenced members is the correct behavior rather than a defect.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class OutboxPublisherPresentsFencingTokenShould
{
	private const long Tenure = 7L;

	private static MessageBusOutboxPublisher CreatePublisher(
		IOutboxStore store,
		ILeaderProcessingGate? gate,
		bool singleActiveWriter = false)
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		var options = Microsoft.Extensions.Options.Options.Create(
			new OutboxDeliveryOptions { SingleActiveWriter = singleActiveWriter });

		A.CallTo(() => serviceProvider.GetService(A<Type>._)).ReturnsLazily((Type t) =>
		{
			if (t == typeof(ILeaderProcessingGate))
			{
				return (object?)gate;
			}

			return t == typeof(IOptions<OutboxDeliveryOptions>) ? options : null;
		});

		return new MessageBusOutboxPublisher(
			store,
			A.Fake<IPayloadSerializer>(),
			A.Fake<IMessageBusAdapter>(),
			serviceProvider,
			A.Fake<ILogger<MessageBusOutboxPublisher>>());
	}

	private static IOutboxStore FencedStore()
	{
		var store = A.Fake<IOutboxStore>(o => o.Implements<IFencedOutboxStore>()).WithHonestCapabilities();

		A.CallTo(() => ((IFencedOutboxStore)store).GetUnsentMessagesAsync(A<int>._, A<long>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>([]));
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>([]));

		return store;
	}

	private static ILeaderProcessingGate Gate(long? token)
	{
		var gate = A.Fake<ILeaderProcessingGate>();
		A.CallTo(() => gate.ShouldProcess).Returns(true);
		A.CallTo(() => gate.FencingToken).Returns(token);
		return gate;
	}

	/// <summary>
	/// SAFETY: under an active tenure against a fencing-capable store, the claim carries the token — and the
	/// unfenced overload is not reached. RED before the fix, which claimed through the unfenced member.
	/// </summary>
	[Fact]
	public async Task ClaimThroughTheFencedMemberWhenATenureIsActive()
	{
		var store = FencedStore();
		var publisher = CreatePublisher(store, Gate(Tenure));

		_ = await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		A.CallTo(() => ((IFencedOutboxStore)store).GetUnsentMessagesAsync(A<int>._, Tenure, A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// SAFETY: a tenure that is active but yields no token fails closed rather than falling through to the
	/// unfenced members — the drain cannot be refused by the store, so it refuses itself.
	/// </summary>
	/// <remarks>
	/// The refusal is a member of the fence-refusal family, not a general failure. That distinction is the
	/// contract, not a detail: the drain paths catch the family specifically, and a refusal typed as a
	/// general failure falls to their generic handler, which disposes of it as a delivery failure and
	/// dead-letters or retries a message this tenure no longer owns. Asserting the precise type is therefore
	/// STRONGER than the general one it replaced — the old expectation was satisfied by exactly the failure
	/// mode that made the fence bypassable.
	/// </remarks>
	[Fact]
	public async Task RefuseToDrainWhenTheTenureYieldsNoToken()
	{
		var store = FencedStore();
		var publisher = CreatePublisher(store, Gate(token: null));

		_ = await Should.ThrowAsync<OutboxFencingTokenUnavailableException>(
			() => publisher.PublishPendingMessagesAsync(CancellationToken.None));

		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// LIVENESS: with no election configured the unfenced members are the legitimate path, so the drain runs
	/// normally. Without this arm a publisher that threw on every drain would satisfy the safety arms.
	/// </summary>
	[Fact]
	public async Task DrainThroughTheUnfencedMemberWhenNoTenureIsConfigured()
	{
		var store = FencedStore();
		var publisher = CreatePublisher(store, gate: null);

		_ = await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	/// <summary>
	/// LIVENESS: a consumer declaring a single active writer has stated that exactly one writer exists by
	/// construction, which is the configuration where an unfenced drain is correct rather than a defect.
	/// </summary>
	[Fact]
	public async Task DrainThroughTheUnfencedMemberWhenTheConsumerDeclaresASingleActiveWriter()
	{
		var store = FencedStore();
		var publisher = CreatePublisher(store, Gate(Tenure), singleActiveWriter: true);

		_ = await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	/// <summary>
	/// SAFETY: a stale-token refusal on the fenced mark-sent must abort that message's drain with NO further
	/// store write — never the unfenced <c>MarkFailedAsync</c>. RED before the fix, which let
	/// <c>StaleOutboxFencingTokenException</c> fall into the generic catch that marks the message failed on
	/// the superseded leader's behalf: an unfenced write the fence exists to prevent.
	/// </summary>
	[Fact]
	public async Task AbortWithNoFurtherWriteWhenTheFencedMarkSentIsRefused()
	{
		var store = A.Fake<IOutboxStore>(o => o.Implements<IFencedOutboxStore>()).WithHonestCapabilities();
		var staged = new OutboundMessage("Test.Message", "payload"u8.ToArray(), "test-destination");

		A.CallTo(() => ((IFencedOutboxStore)store).GetUnsentMessagesAsync(A<int>._, Tenure, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>([staged]));
		A.CallTo(() => ((IFencedOutboxStore)store).MarkSentAsync(staged.Id, Tenure, A<CancellationToken>._))
			.ThrowsAsync(new StaleOutboxFencingTokenException("stale") { PresentedToken = Tenure, HighWaterToken = Tenure + 1 });

		var serviceProvider = A.Fake<IServiceProvider>();
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxDeliveryOptions());
		A.CallTo(() => serviceProvider.GetService(A<Type>._)).ReturnsLazily((Type t) =>
			t == typeof(ILeaderProcessingGate) ? Gate(Tenure) : t == typeof(IOptions<OutboxDeliveryOptions>) ? options : null);

		var messageBus = A.Fake<IMessageBusAdapter>();
		A.CallTo(() => messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(MessageResult.Success()));

		var publisher = new MessageBusOutboxPublisher(
			store, A.Fake<IPayloadSerializer>(), messageBus, serviceProvider, A.Fake<ILogger<MessageBusOutboxPublisher>>());

		_ = await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		A.CallTo(() => store.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}
}
