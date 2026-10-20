// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>Runs the reusable settlement kit against the actual receiver and local native-call observations.</summary>
/// <remarks>These fixtures do not establish live broker acceptance or broker receipt expiry.</remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class RabbitMqReceiverSettlementConformanceShould
{
	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task RefuseUnknownReceiptAndStillSettleAnUnrelatedReceivedDelivery(MessageAction action)
	{
		await using var fixture = new ReceiverFixture();
		await fixture.VerifyUnknownAsync(action);
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task RefuseRetiredLocalReceiptWithoutRepeatingNativeSettlement(MessageAction action)
	{
		await using var fixture = new ReceiverFixture();
		await fixture.VerifyRetiredAsync(action);
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge, false)]
	[InlineData(MessageAction.Reject, false)]
	[InlineData(MessageAction.Requeue, false)]
	[InlineData(MessageAction.Acknowledge, true)]
	[InlineData(MessageAction.Reject, true)]
	[InlineData(MessageAction.Requeue, true)]
	public async Task RetainExactNativeCauseAtPortableBoundary(MessageAction action, bool internalCancellation)
	{
		await using var fixture = new ReceiverFixture();
		await fixture.VerifyFaultAsync(action, internalCancellation);
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task CompletePausedSettlementDespiteOwnedCallerCancellation(MessageAction action)
	{
		await using var fixture = new ReceiverFixture();
		await fixture.VerifyCallerCancellationAsync(action);
	}

	[Theory]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task GivePausedAcknowledgementExclusiveOwnershipAgainstOpposingSettlement(MessageAction opposing)
	{
		await using var fixture = new ReceiverFixture();
		await fixture.VerifyCompetitionAsync(opposing);
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task KeepBatchPeerIndependentAndRefuseRetryOfConsumedFailureReceipt(MessageAction failedAction)
	{
		await using var fixture = new ReceiverFixture();
		await fixture.VerifyBatchAsync(failedAction);
	}

	[Fact]
	public async Task RefusePublishedReceiptMetadataThatDisagreesWithFixtureIssuedNativeTag()
	{
		await using var fixture = new ReceiverFixture(corruptPublishedTag: true);
		var error = await Should.ThrowAsync<InvalidOperationException>(() => fixture.VerifyFaultAsync(MessageAction.Acknowledge, false));
		error.Message.ShouldBe("Published delivery tag does not match the independently issued native receipt.");
		fixture.AssertNoNativeCalls();
	}

	[Fact]
	public async Task RefuseASequentialAttemptAsProofOfOverlappingClaimRefusal()
	{
		await using var fixture = new ReceiverFixture(serializeOpposing: true);
		var error = await Should.ThrowAsync<InvalidOperationException>(() => fixture.VerifyCompetitionAsync(MessageAction.Requeue));
		error.Message.ShouldBe("Fixture stage timed out: observe competing invocation.");
		error.InnerException.ShouldBeOfType<TimeoutException>();
	}

	private sealed class ReceiverFixture : TransportReceiverConformanceTests, IAsyncDisposable
	{
		private const string QueueName = "settlement-conformance";
		private static readonly TimeSpan StageTimeout = TimeSpan.FromSeconds(5);
		private readonly IChannel _channel = A.Fake<IChannel>();
		private readonly Queue<BasicGetResult> _deliveries = new();
		private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _secondInvocation = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private ulong _nextTag = 42;
		private int _publicInvocations;
		private CancellationToken _nativeToken;

		private readonly bool _corruptPublishedTag;
		private readonly bool _serializeOpposing;

		public ReceiverFixture(bool corruptPublishedTag = false, bool serializeOpposing = false)
		{
			_corruptPublishedTag = corruptPublishedTag;
			_serializeOpposing = serializeOpposing;
			A.CallTo(() => _channel.BasicGetAsync(QueueName, false, A<CancellationToken>._))
				.ReturnsLazily(() => Task.FromResult<BasicGetResult?>(_deliveries.TryDequeue(out var delivery) ? delivery : null));
		}

		public ValueTask DisposeAsync() => _channel.DisposeAsync();

		protected override Task<ITransportReceiver> CreateReceiverAsync()
		{
			ITransportReceiver actual = new RabbitMqTransportReceiver(_channel, QueueName, QueueName, NullLogger<RabbitMqTransportReceiver>.Instance);
			return Task.FromResult<ITransportReceiver>(new InvocationObserver(actual, () =>
			{
				if (Interlocked.Increment(ref _publicInvocations) == 2)
				{
					_secondInvocation.TrySetResult();
				}
			}, _corruptPublishedTag, _serializeOpposing));
		}

		protected override Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
		{
			foreach (var message in messages)
			{
				// Public IDs may repeat. Independent native delivery tags must remain independently settleable.
				_deliveries.Enqueue(new BasicGetResult(_nextTag++, false, "", QueueName, 0,
					new global::RabbitMQ.Client.BasicProperties { MessageId = "same-public-id" }, message.Body));
			}
			return Task.CompletedTask;
		}

		public Task VerifyUnknownAsync(MessageAction action) => VerifySettlementRefusal(action,
			_ => Task.FromResult(new TransportReceivedMessage
			{
				Id = "same-public-id",
				ProviderData = new Dictionary<string, object> { ["rabbitmq.receipt_handle"] = "rabbitmq:unknown" },
			}), TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Permanent,
			(_, _) => ObserveNoNativeEffectAsync(),
			(_, message) => ObserveSingleEffectAsync(message, MessageAction.Acknowledge));

		public Task VerifyRetiredAsync(MessageAction action) => VerifyRepeatedSettlement(action, PermanentRefusal(),
			(_, message) => ObserveSingleEffectAsync(message, action),
			(_, message) => ObserveSingleEffectAsync(message, action));

		public Task VerifyFaultAsync(MessageAction action, bool internalCancellation) => VerifyNativeSettlementFailure(action,
			(_, message) => ArrangeFailureAsync(message, action, internalCancellation),
			TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Permanent,
			(_, message) => ObserveSingleEffectAsync(message, action));

		public Task VerifyCallerCancellationAsync(MessageAction action) => VerifySettlementCancellation(action, SettlementExpectation.Completed,
			(_, message) => ArrangePauseAsync(message, action), () => _entered.Task, ReleaseAsync,
			(_, message) =>
			{
				_nativeToken.CanBeCanceled.ShouldBeTrue();
				_nativeToken.IsCancellationRequested.ShouldBeFalse();
				return ObserveSingleEffectAsync(message, action);
			}, StageTimeout);

		public Task VerifyCompetitionAsync(MessageAction opposing) => VerifyCompetingSettlements(MessageAction.Acknowledge, opposing,
			SettlementExpectation.Completed, PermanentRefusal(),
			(_, message) => ArrangePauseAsync(message, MessageAction.Acknowledge), () => _entered.Task,
			() => _secondInvocation.Task, ReleaseAsync,
			(_, message) => ObserveSingleEffectAsync(message, MessageAction.Acknowledge), StageTimeout);

		public Task VerifyBatchAsync(MessageAction failedAction) => VerifySettlementBatchRetry(failedAction, MessageAction.Acknowledge,
			(_, message) => ArrangeFailureAsync(message, failedAction, false), PermanentRefusal(), PermanentRefusal(),
			(_, message) => ObserveSingleEffectAsync(message, failedAction),
			(_, messages) =>
			{
				messages[0].Id.ShouldBe(messages[1].Id);
				AssertPublishedTag(messages[0], 42);
				AssertPublishedTag(messages[1], 43);
				AssertNativeEffect(42, failedAction);
				AssertNativeEffect(43, MessageAction.Acknowledge);
				NativeSettlementCount().ShouldBe(2);
				return Task.CompletedTask;
			}, StageTimeout);

		private Task<Exception> ArrangeFailureAsync(TransportReceivedMessage message, MessageAction action, bool internalCancellation)
		{
			Exception cause = internalCancellation ? new OperationCanceledException("Native settlement timeout") : new IOException("Native settlement response lost");
			AssertPublishedTag(message, 42);
			const ulong tag = 42;
			if (action == MessageAction.Acknowledge)
			{
				A.CallTo(() => _channel.BasicAckAsync(tag, false, A<CancellationToken>._)).ReturnsLazily(() => new ValueTask(Task.FromException(cause)));
			}
			else
			{
				A.CallTo(() => _channel.BasicNackAsync(tag, false, action == MessageAction.Requeue, A<CancellationToken>._))
					.ReturnsLazily(() => new ValueTask(Task.FromException(cause)));
			}
			return Task.FromResult(cause);
		}

		private Task ArrangePauseAsync(TransportReceivedMessage message, MessageAction action)
		{
			ValueTask Pause(CancellationToken token)
			{
				_nativeToken = token;
				_entered.TrySetResult();
				return new ValueTask(_release.Task);
			}
			AssertPublishedTag(message, 42);
			const ulong tag = 42;
			if (action == MessageAction.Acknowledge)
			{
				A.CallTo(() => _channel.BasicAckAsync(tag, false, A<CancellationToken>._))
					.ReturnsLazily((ulong _, bool _, CancellationToken token) => Pause(token));
			}
			else
			{
				A.CallTo(() => _channel.BasicNackAsync(tag, false, action == MessageAction.Requeue, A<CancellationToken>._))
					.ReturnsLazily((ulong _, bool _, bool _, CancellationToken token) => Pause(token));
			}
			return Task.CompletedTask;
		}

		private Task ReleaseAsync()
		{
			_release.TrySetResult();
			return Task.CompletedTask;
		}

		private Task ObserveNoNativeEffectAsync()
		{
			NativeSettlementCount().ShouldBe(0);
			return Task.CompletedTask;
		}

		private Task ObserveSingleEffectAsync(TransportReceivedMessage message, MessageAction action)
		{
			AssertPublishedTag(message, 42);
			AssertNativeEffect(42, action);
			NativeSettlementCount().ShouldBe(1);
			return Task.CompletedTask;
		}

		private void AssertNativeEffect(ulong tag, MessageAction action)
		{
			if (action == MessageAction.Acknowledge)
			{
				A.CallTo(() => _channel.BasicAckAsync(tag, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
				A.CallTo(() => _channel.BasicNackAsync(tag, A<bool>._, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
			}
			else
			{
				A.CallTo(() => _channel.BasicNackAsync(tag, false, action == MessageAction.Requeue, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
				A.CallTo(() => _channel.BasicAckAsync(tag, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
				A.CallTo(() => _channel.BasicNackAsync(tag, false, action != MessageAction.Requeue, A<CancellationToken>._)).MustNotHaveHappened();
			}
		}

		private int NativeSettlementCount() => Fake.GetCalls(_channel)
			.Count(call => call.Method.Name is nameof(IChannel.BasicAckAsync) or nameof(IChannel.BasicNackAsync));
		public void AssertNoNativeCalls() => NativeSettlementCount().ShouldBe(0);

		private static void AssertPublishedTag(TransportReceivedMessage message, ulong expectedNativeTag)
		{
			if (!message.ProviderData.TryGetValue("rabbitmq.delivery_tag", out var value)
				|| value is not ulong publishedTag || publishedTag != expectedNativeTag)
			{
				throw new InvalidOperationException("Published delivery tag does not match the independently issued native receipt.");
			}
		}
		private static SettlementExpectation PermanentRefusal() =>
			SettlementExpectation.Refused(TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Permanent);
	}

	private sealed class InvocationObserver(ITransportReceiver inner, Action invoked, bool corruptPublishedTag, bool serializeOpposing) : ITransportReceiver
	{
		private readonly TaskCompletionSource _firstFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public string Source => inner.Source;
		public async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
		{
			var messages = await inner.ReceiveAsync(maxMessages, cancellationToken);
			if (corruptPublishedTag)
			{
				foreach (var message in messages)
				{
					message.ProviderData["rabbitmq.delivery_tag"] = 999ul;
				}
			}
			return messages;
		}
		public async Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
		{
			try
			{
				await inner.AcknowledgeAsync(message, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				_firstFinished.TrySetResult();
				invoked();
			}
		}
		public async Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
		{
			try
			{
				if (serializeOpposing)
				{
					await _firstFinished.Task.ConfigureAwait(false);
				}
				await inner.RejectAsync(message, reason, requeue, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				invoked();
			}
		}
		public object? GetService(Type serviceType) => inner.GetService(serviceType);
		public ValueTask DisposeAsync() => inner.DisposeAsync();
	}
}
