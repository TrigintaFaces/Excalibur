// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Reflection;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.IbmMq;

using IBM.WMQ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.IbmMq;

/// <summary>
/// Binds the rule that a settlement the broker refused is never reported to the caller as success.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant:</b> <c>AcknowledgeAsync</c> and <c>RejectAsync</c> return normally only when the
/// queue manager accepted the settlement. When it does not, the caller is told, and is told whether the
/// message is coming back.
/// </para>
/// <para>
/// <b>The defect this locks:</b> the settle path caught <c>MQException</c>, logged it, and returned.
/// The caller was told the message was settled; it was not. The syncpoint was neither committed nor
/// backed out, so the queue manager still owned the message and presented it again once the connection
/// closed — reaching the consumer as an unexplained duplicate, with every instrument reporting success.
/// The shipped transport page publishes that a message is "always committed or backed out (never
/// leaked)", and this was exactly the case where neither happened.
/// </para>
/// <para>
/// <b>Both arms are load-bearing.</b> Without the liveness arm, an implementation that threw on every
/// settle — including the ones the broker accepted — would satisfy the safety arm while breaking every
/// working consumer. Without the disposal arm, "surface the failure" would be read as licence to throw
/// out of <c>DisposeAsync</c>, which would abandon the remaining units of work and leak their
/// connections.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class IbmMqSettlementFailureShould
{
	private const string TestSource = "ORDERS.QUEUE";
	private const string MessageId = "msg-1";

	/// <summary>
	/// SAFETY. A commit the queue manager refuses surfaces as a settlement failure rather than a
	/// successful acknowledgement. RED against the swallowing implementation, which returned normally.
	/// </summary>
	[Fact]
	public async Task SurfaceAFailedAcknowledgeInsteadOfReportingSuccess()
	{
		var receiver = CreateReceiver(out _);
		var queueManager = FailingQueueManager();
		PlaceOutstandingUnitOfWork(receiver, MessageId, queueManager);

		var thrown = await Should.ThrowAsync<TransportSettlementException>(
			() => receiver.AcknowledgeAsync(CreateReceiptMessage(MessageId), CancellationToken.None));

		// THE CONNECTION BROKE AROUND THE COMMIT, SO THE OUTCOME IS GENUINELY UNKNOWN AND IS REPORTED AS
		// SUCH. The queue manager may have committed before the reply was lost, in which case the message
		// is gone and will never arrive again. This assertion used to read Expected, which tells a caller
		// to wait for work that may not be coming; Unspecified carries the same safe obligation -- the
		// contract directs a caller to treat it as redelivery-possible -- without asserting a fact the
		// transport cannot establish. A DEFINITE refusal is a different case and is covered below.
		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.TransportName.ShouldBe("IbmMq");

		// The syncpoint this call needed is gone once the connection closes, so repeating the settle can
		// never succeed. A caller that retries on any settlement failure would loop here forever.
		thrown.Retryability.ShouldBe(SettlementRetryability.Permanent);

		// The provider's own exception type must not cross the contract — a consumer catching a
		// settlement failure should never need a reference to the IBM MQ client.
		thrown.ShouldBeAssignableTo<InvalidOperationException>();
		thrown.InnerException.ShouldBeOfType<MQException>();
	}

	/// <summary>
	/// PRECISION. An unverified reason code does not establish a definite commit refusal.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The legacy test identity is retained. Its former assertion inferred a definite refusal from any
	/// code outside a short connection-error list, without evidence about the commit's actual outcome.
	/// </para>
	/// <para>
	/// A constructed unknown-object fault is not proof that this unit of work was rolled back.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ReportACommitFailureWithAnUnspecifiedOutcome()
	{
		var receiver = CreateReceiver(out _);
		var queueManager = A.Fake<IIbmMqQueueManager>();
		A.CallTo(() => queueManager.Commit())
			.Throws(() => new MQException(MQC.MQCC_FAILED, MQC.MQRC_UNKNOWN_OBJECT_NAME));
		PlaceOutstandingUnitOfWork(receiver, MessageId, queueManager);

		var thrown = await Should.ThrowAsync<TransportSettlementException>(
			() => receiver.AcknowledgeAsync(CreateReceiptMessage(MessageId), CancellationToken.None));

		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.Retryability.ShouldBe(SettlementRetryability.Permanent);
	}

	/// <summary>
	/// PRECISION. A failed BACKOUT does not prove that rollback completed.
	/// </summary>
	/// <remarks>
	/// Closing the handles is a cleanup attempt; it does not establish broker recovery or redelivery.
	/// The legacy test identity is retained while its former unconditional-redelivery assertion is corrected.
	/// </remarks>
	[Fact]
	public async Task ReportAFailedBackoutWithAnUnspecifiedOutcome()
	{
		var receiver = CreateReceiver(out _);
		PlaceOutstandingUnitOfWork(receiver, MessageId, FailingQueueManager());

		var thrown = await Should.ThrowAsync<TransportSettlementException>(
			() => receiver.RejectAsync(
				CreateReceiptMessage(MessageId), "retry", requeue: true, CancellationToken.None));

		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.Retryability.ShouldBe(SettlementRetryability.Permanent);
	}

	/// <summary>
	/// SAFETY. The same holds for a rejection: a backout the queue manager refuses is not a successful
	/// reject.
	/// </summary>
	[Fact]
	public async Task SurfaceAFailedRejectInsteadOfReportingSuccess()
	{
		// A backout queue IS configured, so the rejection takes the move-the-message path and the failure
		// under test is the queue manager refusing it — not the absence of a destination, which is the
		// separate refusal covered below.
		var receiver = CreateReceiver(out _, backoutQueueName: "DEV.QUEUE.BACKOUT");
		PlaceOutstandingUnitOfWork(receiver, MessageId, FailingQueueManager());

		var thrown = await Should.ThrowAsync<TransportSettlementException>(
			() => receiver.RejectAsync(CreateReceiptMessage(MessageId), "poison", requeue: false, CancellationToken.None));

		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.Retryability.ShouldBe(SettlementRetryability.Permanent);
	}

	/// <summary>
	/// SAFETY. With no backout queue configured, rejecting without requeue REFUSES instead of quietly
	/// backing the message out.
	/// </summary>
	/// <remarks>
	/// Backing out redelivers the message, which is the opposite of the outcome the caller named. Doing it
	/// silently is how a poison-message arm becomes an unbounded loop that reports success on every pass, so
	/// the receiver resolves the unit of work and then reports that it could not honour the request.
	/// </remarks>
	[Fact]
	public async Task RefuseToRejectWithoutRequeueWhenNoBackoutQueueIsConfigured()
	{
		var receiver = CreateReceiver(out _);
		var queueManager = A.Fake<IIbmMqQueueManager>();
		PlaceOutstandingUnitOfWork(receiver, MessageId, queueManager);

		var thrown = await Should.ThrowAsync<TransportSettlementException>(
			() => receiver.RejectAsync(CreateReceiptMessage(MessageId), "poison", requeue: false, CancellationToken.None));

		// The caller is told the message is coming back, which is what backing it out means.
		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Expected);

		// Configuration, not weather: repeating the call cannot change the outcome.
		thrown.Retryability.ShouldBe(SettlementRetryability.Permanent);

		// LIVENESS: the unit of work is resolved rather than leaked — the message really was backed out.
		A.CallTo(() => queueManager.Backout()).MustHaveHappenedOnceExactly();
		A.CallTo(() => queueManager.Commit()).MustNotHaveHappened();
	}

	/// <summary>
	/// SAFETY. Rejecting WITH requeue still backs out, and does not consult the backout queue.
	/// </summary>
	/// <remarks>
	/// Without this arm, "route every rejection to the backout queue" would satisfy the arms above while
	/// silently removing the caller's ability to ask for a retry.
	/// </remarks>
	[Fact]
	public async Task BackOutRatherThanMoveWhenRequeueIsRequested()
	{
		var receiver = CreateReceiver(out _, backoutQueueName: "DEV.QUEUE.BACKOUT");
		var queueManager = A.Fake<IIbmMqQueueManager>();
		PlaceOutstandingUnitOfWork(receiver, MessageId, queueManager);

		await receiver.RejectAsync(
			CreateReceiptMessage(MessageId), "retry", requeue: true, CancellationToken.None);

		A.CallTo(() => queueManager.Backout()).MustHaveHappenedOnceExactly();
		A.CallTo(() => queueManager.AccessQueue(A<string>._, A<int>._)).MustNotHaveHappened();
	}

	/// <summary>
	/// SAFETY. Settling a message this receiver does not hold REFUSES rather than reporting success.
	/// </summary>
	/// <remarks>
	/// The removal from the outstanding map is the atomic step that decides which of two concurrent
	/// settlements owns the message. Returning normally when it finds nothing makes "I settled it" and
	/// "somebody else did, or it was never mine" the same observation, so two callers could each be told
	/// they settled the same message to contradictory outcomes.
	/// </remarks>
	[Fact]
	public async Task RefuseToSettleAMessageItDoesNotHold()
	{
		var receiver = CreateReceiver(out _);

		var thrown = await Should.ThrowAsync<TransportSettlementException>(
			() => receiver.AcknowledgeAsync(
				CreateReceiptMessage("never-received"), CancellationToken.None));

		// It cannot see what became of a message it never held.
		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.TransportName.ShouldBe("IbmMq");
	}

	/// <summary>
	/// LIVENESS. A settlement the queue manager accepts still completes quietly. Without this arm,
	/// "throw on every settle" would pass the safety arms above while breaking every working consumer.
	/// </summary>
	[Fact]
	public async Task CompleteQuietlyWhenTheQueueManagerAcceptsTheSettlement()
	{
		var receiver = CreateReceiver(out _);
		var queueManager = A.Fake<IIbmMqQueueManager>();
		PlaceOutstandingUnitOfWork(receiver, MessageId, queueManager);

		await receiver.AcknowledgeAsync(CreateReceiptMessage(MessageId), CancellationToken.None);

		A.CallTo(() => queueManager.Commit()).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// LIVENESS. Disposal still does not throw when a queue manager refuses its backout, and it still
	/// settles the units of work that follow the failing one. Cleanup that throws would abandon the rest
	/// and leak their connections.
	/// </summary>
	[Fact]
	public async Task NotThrowFromDisposalWhenASettlementFails()
	{
		var receiver = CreateReceiver(out _);
		var healthy = A.Fake<IIbmMqQueueManager>();
		PlaceOutstandingUnitOfWork(receiver, "msg-failing", FailingQueueManager());
		PlaceOutstandingUnitOfWork(receiver, "msg-healthy", healthy);

		await Should.NotThrowAsync(async () => await receiver.DisposeAsync());

		// The failure must not have stopped the loop before the second unit of work.
		A.CallTo(() => healthy.Backout()).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// LIVENESS. Disposal survives a failure that is NOT a settlement failure and still settles the units
	/// of work behind it.
	/// </summary>
	/// <remarks>
	/// The queue manager and the connection close beneath <c>Settle</c> can fail in ways that are not an
	/// <c>MQException</c>, so they are not wrapped as a settlement failure. A disposal handler catching
	/// only the settlement type lets those escape, break the loop, and leak every connection after the
	/// failing one — which is the outcome the handler exists to prevent, reintroduced by the throw that
	/// was added to stop silent success.
	/// </remarks>
	[Fact]
	public async Task NotThrowFromDisposalWhenAFailureIsNotASettlementFailure()
	{
		var receiver = CreateReceiver(out _);
		var healthy = A.Fake<IIbmMqQueueManager>();

		var hostile = A.Fake<IIbmMqQueueManager>();
		A.CallTo(() => hostile.Backout()).Throws(() => new ObjectDisposedException("queue-manager"));

		PlaceOutstandingUnitOfWork(receiver, "msg-hostile", hostile);
		PlaceOutstandingUnitOfWork(receiver, "msg-healthy", healthy);

		await Should.NotThrowAsync(async () => await receiver.DisposeAsync());

		A.CallTo(() => healthy.Backout()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(2549)] // MQRC_CALL_INTERRUPTED: IBM documents an indeterminate MQCMIT outcome.
	[InlineData(2195)] // MQRC_UNEXPECTED_ERROR: do not infer a definite refusal from an unclassified fault.
	public async Task ReportAnUncertainCommitReasonWithoutPromisingRedelivery(int reasonCode)
	{
		var receiver = CreateReceiver(out _);
		var manager = A.Fake<IIbmMqQueueManager>();
		var cause = new MQException(MQC.MQCC_FAILED, reasonCode);
		A.CallTo(() => manager.Commit()).Throws(cause);
		PlaceOutstandingUnitOfWork(receiver, MessageId, manager);
		var error = await Should.ThrowAsync<TransportSettlementException>(() =>
			receiver.AcknowledgeAsync(CreateReceiptMessage(MessageId), CancellationToken.None));
		AssertUnknownPermanent(error, cause);
		A.CallTo(() => manager.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task KeepAcceptedBackoutQueueCommitUnknownWhenItsReplyIsLost()
	{
		var receiver = CreateReceiver(out _, "DEV.QUEUE.BACKOUT");
		var manager = A.Fake<IIbmMqQueueManager>();
		var backoutQueue = A.Fake<IIbmMqQueue>();
		var inputQueue = A.Fake<IIbmMqQueue>();
		var cause = new MQException(MQC.MQCC_FAILED, MQC.MQRC_CONNECTION_BROKEN);
		A.CallTo(() => manager.AccessQueue("DEV.QUEUE.BACKOUT", A<int>._)).Returns(backoutQueue);
		var accepted = false;
		A.CallTo(() => manager.Commit()).Invokes(() => accepted = true).Throws(cause);
		PlaceOutstandingUnitOfWork(receiver, MessageId, manager, inputQueue);
		var error = await Should.ThrowAsync<TransportSettlementException>(() =>
			receiver.RejectAsync(CreateReceiptMessage(MessageId), "poison", false, CancellationToken.None));
		accepted.ShouldBeTrue(); // Subsequent Backout cannot undo an already committed get+put.
		AssertUnknownPermanent(error, cause);
		A.CallTo(() => backoutQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => inputQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => manager.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreservePrimarySettlementFailureWhenBackoutQueueDisposalFails(bool nonMqCleanup)
	{
		var receiver = CreateReceiver(out _, "DEV.QUEUE.BACKOUT");
		var manager = A.Fake<IIbmMqQueueManager>();
		var inputQueue = A.Fake<IIbmMqQueue>();
		var backoutQueue = A.Fake<IIbmMqQueue>();
		var cause = new MQException(MQC.MQCC_FAILED, MQC.MQRC_CONNECTION_BROKEN);
		Exception cleanup = nonMqCleanup ? new ObjectDisposedException("backout queue") : new MQException(MQC.MQCC_FAILED, MQC.MQRC_CONNECTION_BROKEN);
		A.CallTo(() => manager.AccessQueue("DEV.QUEUE.BACKOUT", A<int>._)).Returns(backoutQueue);
		A.CallTo(() => manager.Commit()).Throws(cause);
		A.CallTo(() => backoutQueue.Dispose()).Throws(cleanup);
		PlaceOutstandingUnitOfWork(receiver, MessageId, manager, inputQueue);
		var error = await Should.ThrowAsync<TransportSettlementException>(() =>
			receiver.RejectAsync(CreateReceiptMessage(MessageId), "poison", false, CancellationToken.None));
		AssertUnknownPermanent(error, cause);
		A.CallTo(() => inputQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => manager.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task TranslateNonMqCommitFailureAndIndependentlyCloseBothHandles(bool reject)
	{
		var receiver = CreateReceiver(out _, "DEV.QUEUE.BACKOUT");
		var manager = A.Fake<IIbmMqQueueManager>();
		var inputQueue = A.Fake<IIbmMqQueue>();
		var cause = new InvalidOperationException("Native commit lost its response");
		A.CallTo(() => manager.Commit()).Throws(cause);
		A.CallTo(() => inputQueue.Dispose()).Throws(new ObjectDisposedException("input queue"));
		A.CallTo(() => manager.Dispose()).Throws(new ObjectDisposedException("manager"));
		PlaceOutstandingUnitOfWork(receiver, MessageId, manager, inputQueue);
		var error = await Should.ThrowAsync<TransportSettlementException>(() => reject
			? receiver.RejectAsync(CreateReceiptMessage(MessageId), "poison", false, CancellationToken.None)
			: receiver.AcknowledgeAsync(CreateReceiptMessage(MessageId), CancellationToken.None));
		AssertUnknownPermanent(error, cause);
		A.CallTo(() => inputQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => manager.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AvoidPromisingRedeliveryWhenMissingBackoutQueueCleanupFails(bool nonMqFailure)
	{
		var receiver = CreateReceiver(out _);
		var manager = A.Fake<IIbmMqQueueManager>();
		var inputQueue = A.Fake<IIbmMqQueue>();
		Exception cause = nonMqFailure ? new InvalidOperationException("Backout failed") : new MQException(MQC.MQCC_FAILED, MQC.MQRC_CONNECTION_BROKEN);
		A.CallTo(() => manager.Backout()).Throws(cause);
		PlaceOutstandingUnitOfWork(receiver, MessageId, manager, inputQueue);
		var error = await Should.ThrowAsync<TransportSettlementException>(() =>
			receiver.RejectAsync(CreateReceiptMessage(MessageId), "poison", false, CancellationToken.None));
		AssertUnknownPermanent(error, cause);
		A.CallTo(() => inputQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => manager.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveReceivedMessageWithGetAndPutOnTheSameSyncpoint()
	{
		var receiver = CreateReceiver(out var provider, "DEV.QUEUE.BACKOUT");
		var manager = A.Fake<IIbmMqQueueManager>();
		var inputQueue = A.Fake<IIbmMqQueue>();
		var backoutQueue = A.Fake<IIbmMqQueue>();
		A.CallTo(() => provider.CreateQueueManager()).Returns(manager);
		A.CallTo(() => manager.AccessQueue(TestSource, A<int>._)).Returns(inputQueue);
		A.CallTo(() => manager.AccessQueue("DEV.QUEUE.BACKOUT", A<int>._)).Returns(backoutQueue);
		MQMessage? native = null;
		A.CallTo(() => inputQueue.Get(A<MQMessage>._, A<MQGetMessageOptions>._))
			.Invokes((MQMessage message, MQGetMessageOptions options) =>
			{
				native = message;
				(options.Options & MQC.MQGMO_SYNCPOINT).ShouldBe(MQC.MQGMO_SYNCPOINT);
			});
		var received = (await receiver.ReceiveAsync(1, CancellationToken.None))[0];
		await receiver.RejectAsync(received, "poison", false, CancellationToken.None);
		native.ShouldNotBeNull();
		A.CallTo(() => backoutQueue.Put(native, A<MQPutMessageOptions>.That.Matches(options =>
			(options.Options & MQC.MQPMO_SYNCPOINT) == MQC.MQPMO_SYNCPOINT))).MustHaveHappenedOnceExactly();
		A.CallTo(() => manager.Commit()).MustHaveHappenedOnceExactly();
		A.CallTo(() => manager.Backout()).MustNotHaveHappened();
		A.CallTo(() => provider.CreateQueueManager()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task ReportPutFailureFromObservedRollbackRatherThanCleanupAttempt(int rollbackFailure)
	{
		var receiver = CreateReceiver(out _, "DEV.QUEUE.BACKOUT");
		var manager = A.Fake<IIbmMqQueueManager>();
		var backoutQueue = A.Fake<IIbmMqQueue>();
		var inputQueue = A.Fake<IIbmMqQueue>();
		var cause = new MQException(MQC.MQCC_FAILED, MQC.MQRC_UNKNOWN_OBJECT_NAME);
		A.CallTo(() => manager.AccessQueue("DEV.QUEUE.BACKOUT", A<int>._)).Returns(backoutQueue);
		A.CallTo(() => backoutQueue.Put(A<MQMessage>._, A<MQPutMessageOptions>._)).Throws(cause);
		if (rollbackFailure != 0)
		{
			Exception cleanup = rollbackFailure == 1
				? new MQException(MQC.MQCC_FAILED, MQC.MQRC_CONNECTION_BROKEN)
				: new InvalidOperationException("Rollback failed");
			A.CallTo(() => manager.Backout()).Throws(cleanup);
		}
		PlaceOutstandingUnitOfWork(receiver, MessageId, manager, inputQueue);
		var error = await Should.ThrowAsync<TransportSettlementException>(() =>
			receiver.RejectAsync(CreateReceiptMessage(MessageId), "poison", false, CancellationToken.None));
		error.InnerException.ShouldBeSameAs(cause);
		error.TransportName.ShouldBe("IbmMq");
		error.Retryability.ShouldBe(SettlementRetryability.Permanent);
		error.RedeliveryExpectation.ShouldBe(rollbackFailure == 0
			? TransportRedeliveryExpectation.Expected : TransportRedeliveryExpectation.Unspecified);
		A.CallTo(() => manager.Commit()).MustNotHaveHappened();
		A.CallTo(() => manager.Backout()).MustHaveHappenedOnceExactly();
		A.CallTo(() => inputQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => manager.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PreserveNonMqBackoutFailureWhileBothCleanupHandlesFail()
	{
		var receiver = CreateReceiver(out _);
		var manager = A.Fake<IIbmMqQueueManager>();
		var inputQueue = A.Fake<IIbmMqQueue>();
		var cause = new InvalidOperationException("Native backout failed");
		A.CallTo(() => manager.Backout()).Throws(cause);
		A.CallTo(() => inputQueue.Dispose()).Throws(new ObjectDisposedException("input queue"));
		A.CallTo(() => manager.Dispose()).Throws(new ObjectDisposedException("manager"));
		PlaceOutstandingUnitOfWork(receiver, MessageId, manager, inputQueue);
		var error = await Should.ThrowAsync<TransportSettlementException>(() =>
			receiver.RejectAsync(CreateReceiptMessage(MessageId), "retry", true, CancellationToken.None));
		AssertUnknownPermanent(error, cause);
		A.CallTo(() => inputQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => manager.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task LeaveOutstandingReceiptUntouchedWhenCallerWasAlreadyCanceled(bool reject)
	{
		var receiver = CreateReceiver(out _);
		var manager = A.Fake<IIbmMqQueueManager>();
		PlaceOutstandingUnitOfWork(receiver, MessageId, manager);
		using var caller = new CancellationTokenSource();
		caller.Cancel();
		await Should.ThrowAsync<OperationCanceledException>(() => reject
			? receiver.RejectAsync(CreateReceiptMessage(MessageId), "retry", true, caller.Token)
			: receiver.AcknowledgeAsync(CreateReceiptMessage(MessageId), caller.Token));
		A.CallTo(() => manager.Commit()).MustNotHaveHappened();
		A.CallTo(() => manager.Backout()).MustNotHaveHappened();
		A.CallTo(() => manager.Dispose()).MustNotHaveHappened();
		await receiver.AcknowledgeAsync(CreateReceiptMessage(MessageId), CancellationToken.None);
		A.CallTo(() => manager.Commit()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task RejectOldEnvelopeAfterBackoutAndReceiveOfSameMqmdIdentity(bool reject, bool requeue)
	{
		var receiver = CreateReceiver(out var provider, "DEV.QUEUE.BACKOUT");
		var firstManager = A.Fake<IIbmMqQueueManager>();
		var nextManager = A.Fake<IIbmMqQueueManager>();
		var firstQueue = ConfigureLifetimeQueue(firstManager);
		var nextQueue = ConfigureLifetimeQueue(nextManager);
		A.CallTo(() => provider.CreateQueueManager()).ReturnsNextFromSequence(firstManager, nextManager);
		var original = (await receiver.ReceiveAsync(1, CancellationToken.None))[0];
		await receiver.RejectAsync(original, "retry", true, CancellationToken.None);
		A.CallTo(() => firstManager.Backout()).MustHaveHappenedOnceExactly();
		var replacement = (await receiver.ReceiveAsync(1, CancellationToken.None))[0];
		replacement.Id.ShouldBe(original.Id); // Public identity remains the broker's MQMD identity.
		replacement.ProviderData["ibmmq.receipt_handle"].ShouldNotBe(original.ProviderData["ibmmq.receipt_handle"]);
		var error = await Should.ThrowAsync<TransportSettlementException>(() => reject
			? receiver.RejectAsync(original, "stale delivery", requeue, CancellationToken.None)
			: receiver.AcknowledgeAsync(original, CancellationToken.None));
		error.TransportName.ShouldBe("IbmMq");
		error.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		error.Retryability.ShouldBe(SettlementRetryability.Permanent);
		A.CallTo(() => nextManager.Commit()).MustNotHaveHappened();
		A.CallTo(() => nextManager.Backout()).MustNotHaveHappened();
		A.CallTo(() => nextManager.AccessQueue("DEV.QUEUE.BACKOUT", A<int>._)).MustNotHaveHappened();
		A.CallTo(() => nextManager.Dispose()).MustNotHaveHappened();
		A.CallTo(() => nextQueue.Dispose()).MustNotHaveHappened();
		await receiver.AcknowledgeAsync(replacement, CancellationToken.None);
		A.CallTo(() => nextManager.Commit()).MustHaveHappenedOnceExactly();
		A.CallTo(() => firstQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => firstManager.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => nextQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => nextManager.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task KeepEqualMqmdIdentitiesAsSeparateOutstandingUnitsOfWork()
	{
		var receiver = CreateReceiver(out var provider);
		var firstManager = A.Fake<IIbmMqQueueManager>();
		var secondManager = A.Fake<IIbmMqQueueManager>();
		var firstQueue = ConfigureLifetimeQueue(firstManager);
		var secondQueue = ConfigureLifetimeQueue(secondManager);
		A.CallTo(() => provider.CreateQueueManager()).ReturnsNextFromSequence(firstManager, secondManager);
		var messages = await receiver.ReceiveAsync(2, CancellationToken.None);
		messages.Count.ShouldBe(2);
		messages[0].Id.ShouldBe(messages[1].Id);
		await receiver.AcknowledgeAsync(messages[0], CancellationToken.None);
		A.CallTo(() => firstManager.Commit()).MustHaveHappenedOnceExactly();
		A.CallTo(() => secondManager.Commit()).MustNotHaveHappened();
		await receiver.RejectAsync(messages[1], "retry", true, CancellationToken.None);
		A.CallTo(() => secondManager.Backout()).MustHaveHappenedOnceExactly();
		A.CallTo(() => firstManager.Backout()).MustNotHaveHappened();
		A.CallTo(() => firstQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => firstManager.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => secondQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => secondManager.Dispose()).MustHaveHappenedOnceExactly();
		await receiver.DisposeAsync();
		A.CallTo(() => firstManager.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => secondManager.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(1, false)]
	[InlineData(2, false)]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	public async Task RejectMissingOrMalformedReceiptWithoutUsingPublicMessageIdentity(int operation, bool malformed)
	{
		var receiver = CreateReceiver(out var provider);
		var manager = A.Fake<IIbmMqQueueManager>();
		var queue = ConfigureLifetimeQueue(manager);
		A.CallTo(() => provider.CreateQueueManager()).Returns(manager);
		var original = (await receiver.ReceiveAsync(1, CancellationToken.None))[0];
		var data = new Dictionary<string, object>();
		if (malformed)
		{
			data["ibmmq.receipt_handle"] = 123;
		}
		var unbound = new TransportReceivedMessage { Id = original.Id, ProviderData = data };
		var error = await Should.ThrowAsync<TransportSettlementException>(() => operation == 0
			? receiver.AcknowledgeAsync(unbound, CancellationToken.None)
			: receiver.RejectAsync(unbound, "unbound", operation == 1, CancellationToken.None));
		error.TransportName.ShouldBe("IbmMq");
		error.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		error.Retryability.ShouldBe(SettlementRetryability.Permanent);
		A.CallTo(() => manager.Commit()).MustNotHaveHappened();
		A.CallTo(() => manager.Backout()).MustNotHaveHappened();
		A.CallTo(() => queue.Dispose()).MustNotHaveHappened();
		await receiver.AcknowledgeAsync(original, CancellationToken.None);
		A.CallTo(() => manager.Commit()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeBothOutstandingUnitsWithEqualMqmdIdentity()
	{
		var receiver = CreateReceiver(out var provider);
		var firstManager = A.Fake<IIbmMqQueueManager>();
		var secondManager = A.Fake<IIbmMqQueueManager>();
		var firstQueue = ConfigureLifetimeQueue(firstManager);
		var secondQueue = ConfigureLifetimeQueue(secondManager);
		A.CallTo(() => provider.CreateQueueManager()).ReturnsNextFromSequence(firstManager, secondManager);
		var messages = await receiver.ReceiveAsync(2, CancellationToken.None);
		messages[0].Id.ShouldBe(messages[1].Id);
		messages[0].ProviderData["ibmmq.receipt_handle"].ShouldNotBe(messages[1].ProviderData["ibmmq.receipt_handle"]);
		await receiver.DisposeAsync();
		A.CallTo(() => firstManager.Backout()).MustHaveHappenedOnceExactly();
		A.CallTo(() => secondManager.Backout()).MustHaveHappenedOnceExactly();
		A.CallTo(() => firstQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => secondQueue.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => firstManager.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => secondManager.Dispose()).MustHaveHappenedOnceExactly();
	}

	private static TransportReceivedMessage CreateReceiptMessage(string receiptHandle) => new()
	{
		Id = MessageId,
		ProviderData = new Dictionary<string, object> { ["ibmmq.receipt_handle"] = receiptHandle },
	};

	private static IIbmMqQueue ConfigureLifetimeQueue(IIbmMqQueueManager manager)
	{
		var queue = A.Fake<IIbmMqQueue>();
		A.CallTo(() => manager.AccessQueue(TestSource, A<int>._)).Returns(queue);
		A.CallTo(() => queue.Get(A<MQMessage>._, A<MQGetMessageOptions>._))
			.Invokes((MQMessage message, MQGetMessageOptions options) =>
			{
				message.MessageId = Enumerable.Repeat((byte)7, 24).ToArray();
				(options.Options & MQC.MQGMO_SYNCPOINT).ShouldBe(MQC.MQGMO_SYNCPOINT);
			});
		return queue;
	}

	private static void AssertUnknownPermanent(TransportSettlementException error, Exception cause)
	{
		error.TransportName.ShouldBe("IbmMq");
		error.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		error.Retryability.ShouldBe(SettlementRetryability.Permanent);
		error.InnerException.ShouldBeSameAs(cause);
	}

	private static IbmMqTransportReceiver CreateReceiver(
		out IIbmMqConnectionProvider connectionProvider,
		string? backoutQueueName = null)
	{
		connectionProvider = A.Fake<IIbmMqConnectionProvider>();
		return new IbmMqTransportReceiver(
			connectionProvider,
			TestSource,
			new IbmMqReceiveTuningOptions { BackoutQueueName = backoutQueueName },
			NullLogger<IbmMqTransportReceiver>.Instance);
	}

	private static IIbmMqQueueManager FailingQueueManager()
	{
		var queueManager = A.Fake<IIbmMqQueueManager>();
		A.CallTo(() => queueManager.Commit())
			.Throws(() => new MQException(MQC.MQCC_FAILED, MQC.MQRC_CONNECTION_BROKEN));
		A.CallTo(() => queueManager.Backout())
			.Throws(() => new MQException(MQC.MQCC_FAILED, MQC.MQRC_CONNECTION_BROKEN));
		return queueManager;
	}

	/// <summary>
	/// Puts a unit of work into the receiver's outstanding map without driving a real broker receive.
	/// <c>UnitOfWork</c> is a private nested record, so it is constructed reflectively; the map itself is
	/// reached the same way.
	/// </summary>
	private static void PlaceOutstandingUnitOfWork(
		IbmMqTransportReceiver receiver,
		string receiptHandle,
		IIbmMqQueueManager queueManager,
		IIbmMqQueue? queue = null)
	{
		var outstandingField = typeof(IbmMqTransportReceiver)
			.GetField("_outstanding", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("The _outstanding field was renamed; this lock needs updating.");

		var unitOfWorkType = typeof(IbmMqTransportReceiver)
			.GetNestedType("UnitOfWork", BindingFlags.NonPublic)
			?? throw new InvalidOperationException("The UnitOfWork type was renamed; this lock needs updating.");

		// The message is retained on the unit of work because rejecting without requeue has to PUT it to the
		// backout queue, and a message got under syncpoint cannot be re-read to recover its content.
		var unitOfWork = Activator.CreateInstance(
			unitOfWorkType,
			queueManager,
			queue ?? A.Fake<IIbmMqQueue>(),
			new MQMessage())!;

		var map = (IDictionary)outstandingField.GetValue(receiver)!;
		// Synthetic tests name the receipt explicitly; production assigns an independent GUID per delivery.
		map[receiptHandle] = unitOfWork;
	}
}
