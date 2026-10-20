// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.IbmMq;
using IBM.WMQ;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.IbmMq;

/// <summary>Runs shared settlement controls against independently issued local queue-manager units of work.</summary>
/// <remarks>Synchronous native boundaries are paused with signals, not sleeps. Broker acceptance is not certified.</remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class IbmMqReceiverSettlementConformanceShould
{
	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task CompleteNativeAttemptWhenCallerCancelsAfterClaim(MessageAction action)
	{
		using var fixture = new Fixture();
		await fixture.CancellationAsync(action);
	}

	[Theory]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task RefuseOpposingContenderWhileAcknowledgementOwnsNativeAttempt(MessageAction action)
	{
		using var fixture = new Fixture();
		await fixture.CompetitionAsync(action);
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task KeepHealthyBatchUnitIndependentAndRefuseConsumedFailureRetry(MessageAction action)
	{
		using var fixture = new Fixture();
		await fixture.BatchAsync(action);
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task RefuseImmediateRepeatWithoutNativeEffect(MessageAction action)
	{
		using var fixture = new Fixture();
		await fixture.RepeatedAsync(action);
	}

	private sealed class Fixture : TransportReceiverConformanceTests, IDisposable
	{
		private const string Source = "KIT.INPUT";
		private const string BackoutQueue = "KIT.BACKOUT";
		private readonly IIbmMqConnectionProvider _provider = A.Fake<IIbmMqConnectionProvider>();
		private readonly Queue<IIbmMqQueueManager> _available = new();
		private readonly List<Unit> _issued = [];
		private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _secondInvocation = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly ManualResetEventSlim _release = new();
		private int _invocations;

		public Fixture() => A.CallTo(() => _provider.CreateQueueManager()).ReturnsLazily(() => _available.Dequeue());
		public void Dispose() => _release.Dispose();
		protected override Task<ITransportReceiver> CreateReceiverAsync() => Task.FromResult<ITransportReceiver>(new InvocationObserver(
			new IbmMqTransportReceiver(_provider, Source, new IbmMqReceiveTuningOptions { BackoutQueueName = BackoutQueue }, NullLogger<IbmMqTransportReceiver>.Instance),
			() => Interlocked.Increment(ref _invocations) == 2,
			() => _secondInvocation.TrySetResult()));
		protected override Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
		{
			foreach (var ignored in messages)
			{
				var manager = A.Fake<IIbmMqQueueManager>();
				var input = A.Fake<IIbmMqQueue>();
				var backout = A.Fake<IIbmMqQueue>();
				var unit = new Unit(manager, input, backout);
				A.CallTo(() => manager.AccessQueue(Source, A<int>._)).Returns(input);
				A.CallTo(() => manager.AccessQueue(BackoutQueue, A<int>._)).Returns(backout);
				A.CallTo(() => input.Get(A<MQMessage>._, A<MQGetMessageOptions>._)).Invokes((MQMessage message, MQGetMessageOptions options) =>
				{
					(options.Options & MQC.MQGMO_SYNCPOINT).ShouldBe(MQC.MQGMO_SYNCPOINT);
					message.MessageId = Enumerable.Repeat((byte)7, 24).ToArray();
					unit.Message = message;
				});
				_issued.Add(unit);
				_available.Enqueue(manager);
			}
			return Task.CompletedTask;
		}

		public Task CancellationAsync(MessageAction action) => VerifySettlementCancellation(action, SettlementExpectation.Completed,
			(_, message) => PauseAsync(message, action), () => _entered.Task, ReleaseAsync,
			(_, message) => ObserveAsync(message, action), TimeSpan.FromSeconds(5));
		public Task CompetitionAsync(MessageAction opposing) => VerifyCompetingSettlements(MessageAction.Acknowledge, opposing,
			SettlementExpectation.Completed, Refusal(), (_, message) => PauseAsync(message, MessageAction.Acknowledge),
			() => _entered.Task, () => _secondInvocation.Task, ReleaseAsync,
			(_, message) => ObserveAsync(message, MessageAction.Acknowledge), TimeSpan.FromSeconds(5));
		public Task RepeatedAsync(MessageAction action) => VerifyRepeatedSettlement(action, Refusal(),
			(_, message) => ObserveAsync(message, action), (_, message) => ObserveAsync(message, action));
		public Task BatchAsync(MessageAction action) => VerifySettlementBatchRetry(action, MessageAction.Acknowledge,
			(_, message) =>
			{
				AssertReceipt(message);
				Exception cause = new IOException("Native unit-of-work response lost");
				if (action == MessageAction.Requeue) A.CallTo(() => _issued[0].Manager.Backout()).Throws(cause);
				else A.CallTo(() => _issued[0].Manager.Commit()).Throws(cause);
				return Task.FromResult(cause);
			}, Refusal(), Refusal(), (_, message) =>
			{
				AssertReceipt(message);
				AssertEffect(0, action, failed: true);
				NativeCalls(_issued[1]).ShouldBe(0);
				AssertClosed(0, action);
				return Task.CompletedTask;
			}, (_, messages) =>
			{
				AssertReceipt(messages[0]);
				AssertReceipt(messages[1]);
				messages[0].Id.ShouldBe(messages[1].Id);
				messages[0].ProviderData["ibmmq.receipt_handle"].ShouldNotBe(messages[1].ProviderData["ibmmq.receipt_handle"]);
				AssertEffect(0, action, failed: true);
				AssertEffect(1, MessageAction.Acknowledge, failed: false);
				AssertClosed(0, action);
				AssertClosed(1, MessageAction.Acknowledge);
				return Task.CompletedTask;
			}, TimeSpan.FromSeconds(5));

		private Task PauseAsync(TransportReceivedMessage message, MessageAction action)
		{
			AssertReceipt(message);
			void Pause()
			{
				_entered.TrySetResult();
				if (!_release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Fixture native pause was not released.");
			}
			if (action == MessageAction.Requeue) A.CallTo(() => _issued[0].Manager.Backout()).Invokes(Pause);
			else A.CallTo(() => _issued[0].Manager.Commit()).Invokes(Pause);
			return Task.CompletedTask;
		}
		private Task ReleaseAsync() { _release.Set(); return Task.CompletedTask; }
		private static SettlementExpectation Refusal() => SettlementExpectation.Refused(TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Permanent);
		private static void AssertReceipt(TransportReceivedMessage message)
		{
			message.Id.ShouldBe(Convert.ToHexString(Enumerable.Repeat((byte)7, 24).ToArray()));
			Guid.TryParseExact(message.ProviderData["ibmmq.receipt_handle"].ShouldBeOfType<string>(), "N", out _).ShouldBeTrue();
		}
		private Task ObserveAsync(TransportReceivedMessage message, MessageAction action)
		{
			AssertReceipt(message);
			AssertEffect(0, action, failed: false);
			AssertClosed(0, action);
			return Task.CompletedTask;
		}
		private void AssertEffect(int index, MessageAction action, bool failed)
		{
			var unit = _issued[index];
			A.CallTo(() => unit.Manager.Commit()).MustHaveHappened(action == MessageAction.Requeue ? 0 : 1, Times.Exactly);
			A.CallTo(() => unit.Manager.Backout()).MustHaveHappened(action == MessageAction.Requeue || (failed && action == MessageAction.Reject) ? 1 : 0, Times.Exactly);
			A.CallTo(() => unit.Input.Get(A<MQMessage>._, A<MQGetMessageOptions>._)).MustHaveHappenedOnceExactly();
			if (action == MessageAction.Reject)
				A.CallTo(() => unit.Backout.Put(unit.Message!, A<MQPutMessageOptions>.That.Matches(options => (options.Options & MQC.MQPMO_SYNCPOINT) != 0))).MustHaveHappenedOnceExactly();
			else A.CallTo(() => unit.Backout.Put(A<MQMessage>._, A<MQPutMessageOptions>._)).MustNotHaveHappened();
			Fake.GetCalls(unit.Backout).Count(call => call.Method.Name == "Put").ShouldBe(action == MessageAction.Reject ? 1 : 0);
		}
		private void AssertClosed(int index, MessageAction action)
		{
			var unit = _issued[index];
			A.CallTo(() => unit.Manager.Dispose()).MustHaveHappenedOnceExactly();
			A.CallTo(() => unit.Input.Dispose()).MustHaveHappenedOnceExactly();
			A.CallTo(() => unit.Backout.Dispose()).MustHaveHappened(action == MessageAction.Reject ? 1 : 0, Times.Exactly);
		}
		private static int NativeCalls(Unit unit) => Fake.GetCalls(unit.Manager).Count(call => call.Method.Name is "Commit" or "Backout");
		private sealed class Unit(IIbmMqQueueManager manager, IIbmMqQueue input, IIbmMqQueue backout)
		{
			public IIbmMqQueueManager Manager { get; } = manager;
			public IIbmMqQueue Input { get; } = input;
			public IIbmMqQueue Backout { get; } = backout;
			public MQMessage? Message { get; set; }
		}
	}

	private sealed class InvocationObserver(ITransportReceiver inner, Func<bool> isSecondAttempt, Action completedSecondAttempt) : ITransportReceiver
	{
		public string Source => inner.Source;
		public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken) => inner.ReceiveAsync(maxMessages, cancellationToken);
		public async Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
		{
			var second = isSecondAttempt();
			try { await inner.AcknowledgeAsync(message, cancellationToken); }
			finally { if (second) completedSecondAttempt(); }
		}
		public async Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
		{
			var second = isSecondAttempt();
			// Signal only after the actual opposing claim has completed or refused, while the owner remains paused.
			try { await inner.RejectAsync(message, reason, requeue, cancellationToken); }
			finally { if (second) completedSecondAttempt(); }
		}
		public object? GetService(Type serviceType) => inner.GetService(serviceType);
		public ValueTask DisposeAsync() => inner.DisposeAsync();
	}
}
