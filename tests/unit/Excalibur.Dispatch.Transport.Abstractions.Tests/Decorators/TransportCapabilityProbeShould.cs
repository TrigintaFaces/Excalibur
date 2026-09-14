// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

/// <summary>
/// The capability probe on the three transport contracts, at the leaf and through a decorator.
/// </summary>
/// <remarks>
/// <para>
/// Two halves that only work together. A leaf that answers for nothing makes the probe useless: an
/// implementation is asked whether it provides something it demonstrably does provide and says no. A
/// decorator that forwards without checking itself first is worse than useless once the leaf answers,
/// because the probe then reaches past the decorator and hands the caller the transport underneath it —
/// the caller asked for a subscriber, got the one behind the reconnection and dead-letter wrappers, and
/// nothing tells them the wrappers are no longer in the path.
/// </para>
/// <para>
/// The stubs are real classes rather than fakes on purpose: a fake supplies its own implementation of
/// every interface member, including the default one, so it would report on the fake instead of on the
/// contract under test.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportCapabilityProbeShould
{
	[Fact]
	public void AnswerForItsOwnContractFromABareSubscriber()
	{
		ITransportSubscriber subscriber = new StubSubscriber();

		subscriber.GetService(typeof(ITransportSubscriber)).ShouldBeSameAs(subscriber);
	}

	[Fact]
	public void AnswerForItsOwnContractFromABareSender()
	{
		ITransportSender sender = new StubSender();

		sender.GetService(typeof(ITransportSender)).ShouldBeSameAs(sender);
	}

	[Fact]
	public void AnswerForItsOwnContractFromABareReceiver()
	{
		ITransportReceiver receiver = new StubReceiver();

		receiver.GetService(typeof(ITransportReceiver)).ShouldBeSameAs(receiver);
	}

	[Fact]
	public void DeclineAContractABareSubscriberDoesNotImplement()
	{
		ITransportSubscriber subscriber = new StubSubscriber();

		subscriber.GetService(typeof(IDeadLetterQueueManager)).ShouldBeNull();
	}

	[Fact]
	public void AnswerWithTheOutermostDecoratorRatherThanTheSubscriberBeneathIt()
	{
		var inner = new StubSubscriber();
		ITransportSubscriber decorator = new PassThroughSubscriber(inner);

		decorator.GetService(typeof(ITransportSubscriber)).ShouldBeSameAs(
			decorator,
			"the caller asked for the subscriber it is holding, and the one underneath the decorator no "
			+ "longer has the decorator's behaviour in its path");
	}

	[Fact]
	public void AnswerWithTheOutermostDecoratorRatherThanTheSenderBeneathIt()
	{
		var inner = new StubSender();
		ITransportSender decorator = new PassThroughSender(inner);

		decorator.GetService(typeof(ITransportSender)).ShouldBeSameAs(decorator);
	}

	[Fact]
	public void AnswerWithTheOutermostDecoratorRatherThanTheReceiverBeneathIt()
	{
		var inner = new StubReceiver();
		ITransportReceiver decorator = new PassThroughReceiver(inner);

		decorator.GetService(typeof(ITransportReceiver)).ShouldBeSameAs(decorator);
	}

	[Fact]
	public void ReachThroughADecoratorForACapabilityOnlyTheInnerTransportProvides()
	{
		var inner = new NativeHandleSubscriber();
		ITransportSubscriber decorator = new PassThroughSubscriber(inner);

		decorator.GetService(typeof(INativeHandle)).ShouldBeSameAs(
			inner,
			"a decorator that answered only for itself would hide every capability of the transport it "
			+ "wraps, which is the reason the probe exists rather than a cast");
	}

	private interface INativeHandle;

	private class StubSubscriber : ITransportSubscriber
	{
		public string Source => "stub";

		public Task SubscribeAsync(
			Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
			CancellationToken cancellationToken) => Task.CompletedTask;

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed class NativeHandleSubscriber : StubSubscriber, INativeHandle;

	private sealed class PassThroughSubscriber(ITransportSubscriber inner) : DelegatingTransportSubscriber(inner);

	private sealed class StubSender : ITransportSender
	{
		public string Destination => "stub";

		public Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken) =>
			Task.FromResult(SendResult.Success("id"));

		public Task<BatchSendResult> SendBatchAsync(
			IReadOnlyList<TransportMessage> messages,
			CancellationToken cancellationToken) => Task.FromResult(new BatchSendResult());

		public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed class PassThroughSender(ITransportSender inner) : DelegatingTransportSender(inner);

	private sealed class StubReceiver : ITransportReceiver
	{
		public string Source => "stub";

		public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(
			int maxMessages,
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<TransportReceivedMessage>>([]);

		public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task RejectAsync(
			TransportReceivedMessage message,
			string? reason,
			bool requeue,
			CancellationToken cancellationToken) => Task.CompletedTask;

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed class PassThroughReceiver(ITransportReceiver inner) : DelegatingTransportReceiver(inner);
}
