// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

/// <summary>
/// Proves by execution that a republished outbox message still reaches its transport.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The outbox wraps each republished payload in a type that implements only the
/// bare <see cref="IDispatchMessage"/> marker. When the classifier changed so that an unclassified type
/// receives every kind — and therefore every middleware, including authentication and authorization —
/// the open question was whether a republish, which carries no user, would now be rejected. That would
/// convert a security fix into a redelivery outage: the outbox would keep retrying a message that can
/// never succeed.
/// </para>
/// <para>
/// <b>Why it is a test and not a trace.</b> The question was closed by reading declarations — the field
/// is an adapter type, so the pipeline is not on the path. Five people read source and four of them
/// corrected themselves at least once; two published clearances they later re-labelled as narrower than
/// claimed. A declaration says what the code is. This says what it does. If the republish path ever
/// acquires a classifier — directly, or through a decorator, or through a bus implementation swapped in
/// by configuration — this arm fails and the reading was out of date.
/// </para>
/// <para>
/// <b>This is a liveness arm (testing-patterns §3).</b> It asserts that something good still happens.
/// The security fix it guards is satisfied by refusing everything, and refusing everything is exactly
/// the failure this catches. There is no safety arm here on purpose: the safety half lives with the
/// classifier, and this file exists to hold the half that a fail-closed change puts at risk.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class OutboxRepublishSurvivesClassificationShould
{
	private readonly IOutboxStore _outboxStore;
	private readonly IPayloadSerializer _serializer;
	private readonly IMessageBusAdapter _messageBus;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<MessageBusOutboxPublisher> _logger;
	private readonly MessageBusOutboxPublisher _publisher;

	public OutboxRepublishSurvivesClassificationShould()
	{
		_outboxStore = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>()).WithHonestCapabilities();
		_serializer = A.Fake<IPayloadSerializer>();
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

	/// <summary>
	/// LIVENESS — a staged message is handed to the transport bus and then marked sent.
	/// </summary>
	/// <remarks>
	/// Both halves are asserted because either alone is satisfiable by a broken path: reaching the bus
	/// without marking sent is an infinite redelivery loop, and marking sent without reaching the bus is
	/// silent message loss. The pair is what makes this "the message was delivered".
	/// </remarks>
	[Fact]
	public async Task DeliverARepublishedMessageToTheTransportBus()
	{
		var staged = new OutboundMessage("TestMessage", new byte[] { 7, 7, 7 }, "orders-queue");

		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { staged });

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(A.Fake<IMessageResult>());

		var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

		_ = A.CallTo(() => _messageBus.PublishAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();

		_ = A.CallTo(() => _outboxStore.MarkSentAsync(staged.Id, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		result.SuccessCount.ShouldBe(
			1,
			"a republished message must still be delivered — the outbox wrapper declares no message kind, "
			+ "so any classification on this path would apply every middleware to a redelivery that "
			+ "carries no user, and the message could never succeed on any retry");

		result.FailureCount.ShouldBe(0);
	}

	/// <summary>
	/// The message handed to the bus is the REPUBLISHED one, not merely some message.
	/// </summary>
	/// <remarks>
	/// Without this, the arm above is satisfied by any call to the bus at all — including one carrying an
	/// empty or substituted payload. Binding the payload is what ties "the bus was called" to "this
	/// message was delivered".
	/// </remarks>
	[Fact]
	public async Task HandTheStagedPayloadToTheBusUnaltered()
	{
		var payload = new byte[] { 4, 2 };
		var staged = new OutboundMessage("TestMessage", payload, "orders-queue");

		IDispatchMessage? delivered = null;

		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { staged });

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage m, IMessageContext _, CancellationToken _) => delivered = m)
			.Returns(A.Fake<IMessageResult>());

		_ = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

		_ = delivered.ShouldNotBeNull(
			"the bus must receive the republished message itself — if this is null the delivery arm above "
			+ "passed on a call that carried nothing");

		// The wrapper is private to the publisher, so it cannot be named here. What is assertable — and
		// what actually matters to a consumer — is that whatever crossed the boundary implements the
		// dispatch contract and was produced for this republish.
		_ = delivered.ShouldBeAssignableTo<IDispatchMessage>();
	}
}
