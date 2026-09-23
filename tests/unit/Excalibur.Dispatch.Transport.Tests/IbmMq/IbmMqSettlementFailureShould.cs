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
			() => receiver.AcknowledgeAsync(new TransportReceivedMessage { Id = MessageId }, CancellationToken.None));

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
	/// PRECISION. A commit the queue manager DEFINITELY refused reports <c>Expected</c>, not
	/// <c>Unspecified</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the discriminator for the arm above. A queue manager that refuses a request can only do so
	/// by not performing it, so the syncpoint is intact and the message is certainly still owned by the
	/// broker — that is knowable, and reporting <c>Unspecified</c> for it would throw away information the
	/// caller can act on.
	/// </para>
	/// <para>
	/// Without this arm, "always answer <c>Unspecified</c>" would satisfy the arm above while making the
	/// discriminator meaningless. The reason code is the only thing separating the two cases.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ReportADefiniteCommitRefusalAsExpectedRatherThanUnknown()
	{
		var receiver = CreateReceiver(out _);
		var queueManager = A.Fake<IIbmMqQueueManager>();
		A.CallTo(() => queueManager.Commit())
			.Throws(() => new MQException(MQC.MQCC_FAILED, MQC.MQRC_UNKNOWN_OBJECT_NAME));
		PlaceOutstandingUnitOfWork(receiver, MessageId, queueManager);

		var thrown = await Should.ThrowAsync<TransportSettlementException>(
			() => receiver.AcknowledgeAsync(new TransportReceivedMessage { Id = MessageId }, CancellationToken.None));

		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Expected);
	}

	/// <summary>
	/// PRECISION. A failed BACKOUT reports <c>Expected</c> whatever the reason code.
	/// </summary>
	/// <remarks>
	/// Unlike a commit, both reachable outcomes of a failed backout return the message: either the backout
	/// happened, or the connection died and the queue manager rolled the syncpoint back anyway. There is no
	/// interleaving in which the message is consumed, so the uncertainty that applies to a commit does not
	/// apply here and reporting <c>Unspecified</c> would understate what is known.
	/// </remarks>
	[Fact]
	public async Task ReportAFailedBackoutAsExpectedEvenWhenTheConnectionBroke()
	{
		var receiver = CreateReceiver(out _);
		PlaceOutstandingUnitOfWork(receiver, MessageId, FailingQueueManager());

		var thrown = await Should.ThrowAsync<TransportSettlementException>(
			() => receiver.RejectAsync(
				new TransportReceivedMessage { Id = MessageId }, "retry", requeue: true, CancellationToken.None));

		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Expected);
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
			() => receiver.RejectAsync(new TransportReceivedMessage { Id = MessageId }, "poison", requeue: false, CancellationToken.None));

		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Expected);
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
			() => receiver.RejectAsync(new TransportReceivedMessage { Id = MessageId }, "poison", requeue: false, CancellationToken.None));

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
			new TransportReceivedMessage { Id = MessageId }, "retry", requeue: true, CancellationToken.None);

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
				new TransportReceivedMessage { Id = "never-received" }, CancellationToken.None));

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

		await receiver.AcknowledgeAsync(new TransportReceivedMessage { Id = MessageId }, CancellationToken.None);

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
		string messageId,
		IIbmMqQueueManager queueManager)
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
			A.Fake<IIbmMqQueue>(),
			new MQMessage())!;

		var map = (IDictionary)outstandingField.GetValue(receiver)!;
		map[messageId] = unitOfWork;
	}
}
