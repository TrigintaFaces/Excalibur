// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

/// <summary>
/// The SECOND outbox drain must report a delivery failure under the claim it holds, exactly as the first
/// one does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a second file for the same property.</b> There are two drains, and until recently only one of
/// them had a lock. The sibling arm in the outbox test project binds this property for
/// <c>OutboxProcessor</c>; its class doc originally said "the drain" as though there were one, which is
/// how a lock covering half a property comes to read as covering all of it. This file is the other half,
/// and the two are deliberately named for their subjects rather than for the property they share.
/// </para>
/// <para>
/// <b>Why this drain was the riskier one.</b> Its failure path sits in a <c>catch</c> whose sibling
/// <c>catch</c> — seven lines above — carries a comment forbidding exactly the unfenced write the general
/// branch used to perform. The reasoning was right there and the adjacent branch did the opposite. A
/// comment is the one artifact in the tree that nothing checks; an arm is not.
/// </para>
/// <para>
/// <b>Scope, so a green is not over-read.</b> This binds ROUTING: given a store that offers the
/// claim-scoped surface and a message carrying a claim, the failure is reported through that surface,
/// under that identity, and not through the member that carries none. It says nothing about atomicity —
/// a fake returns what it was told, and whether the store evaluates the claim and the mutation in one
/// action is a property of the statement, observable only against real infrastructure. It also says
/// nothing about the documented fallback taken when a store offers no claim-scoped surface; that
/// disposition is recorded as a known gap rather than asserted here.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "1")]
public sealed class OutboxPublisherReportsFailureUnderItsClaimShould
{
	private const string StampedClaim = "claim-alpha";

	/// <summary>
	/// SAFETY. The failure is reported through the surface that carries the claim.
	/// </summary>
	[Fact]
	public async Task Report_the_failure_through_the_claim_scoped_surface()
	{
		var (publisher, store, claimScoped, message) = Drain();

		var result = await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		result.FailureCount.ShouldBe(
			1,
			"liveness first: an arm whose drain never reached a failure would satisfy any claim about "
			+ "which member that failure was reported through");

		_ = A.CallTo(() => claimScoped.MarkFailedAsync(
				message.Id, A<string>._, A<int>._, StampedClaim, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// SAFETY. The member that carries no claim is not used when a claim is available.
	/// </summary>
	/// <remarks>
	/// Separate from the arm above because a drain could call BOTH — reporting through the claim-scoped
	/// surface and then again through the unscoped one — which satisfies that arm completely while still
	/// performing the write no fence can refuse.
	/// </remarks>
	[Fact]
	public async Task Never_report_through_the_member_that_carries_no_claim()
	{
		var (publisher, store, _, message) = Drain();

		_ = await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		A.CallTo(() => store.MarkFailedAsync(
				message.Id, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static (MessageBusOutboxPublisher Publisher, IOutboxStore Store,
		IClaimScopedOutboxStore ClaimScoped, OutboundMessage Message) Drain()
	{
		// The store answers the capability probe the way a real one does: itself for what it implements,
		// null otherwise. A bare fake answers with a non-null dummy that is not the requested interface,
		// which reports an implemented capability ABSENT and turns a correct drain red.
		var store = A.Fake<IOutboxStore>(o => o.Implements<IClaimScopedOutboxStore>())
			.WithHonestCapabilities();
		var claimScoped = store.ShouldBeAssignableTo<IClaimScopedOutboxStore>();

		var message = new OutboundMessage("OrderCreated", [9, 9], "orders-default")
		{
			DispatcherId = StampedClaim,
		};

		_ = A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>([message]))
			.Once()
			.Then
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>([]));

		_ = A.CallTo(() => claimScoped.MarkFailedAsync(
				A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied));

		// Any dispatch fault routes to the same catch; which one it is does not matter to the routing
		// property, so the fault is left to the bus rather than manufactured through the payload.
		var messageBus = A.Fake<IMessageBusAdapter>();
		_ = A.CallTo(() => messageBus.PublishAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("transport unavailable"));

		var publisher = new MessageBusOutboxPublisher(
			store,
			A.Fake<IPayloadSerializer>(),
			messageBus,
			A.Fake<IServiceProvider>(),
			A.Fake<ILogger<MessageBusOutboxPublisher>>());

		return (publisher, store, claimScoped, message);
	}
}
