// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>Checks native forwarding and uncertainty with independently issued fixture keys.</summary>
/// <remarks>Does not establish broker durability, receipt expiry, or server idempotence.</remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class PubSubReceiverSettlementConformanceShould
{
	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public Task CompleteAfterCallerCancellationAtNativeEntry(MessageAction action) =>
		new Fixture().CancellationAsync(action);

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public Task ForwardRepeatedSettlementToTheSameIssuedNativeKey(MessageAction action) =>
		new Fixture().RepeatedAsync(action);

	[Fact]
	public Task EnterOpposingNativeSettlementBeforeReleasingFirst() => new Fixture().CompetitionAsync();

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectNoOpOrSerializedSecondNativeOperation(bool serialized)
	{
		var error = await Should.ThrowAsync<InvalidOperationException>(() => new Fixture(serialized ? 2 : 1).CompetitionAsync());
		error.Message.ShouldContain("competing settlement effects");
		error.InnerException.ShouldBeOfType<Shouldly.ShouldAssertException>();
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public Task SettleHealthyBatchPeerAndRetryOnlyTheFailedIssuedKey(MessageAction action) =>
		new Fixture().BatchAsync(action);

	private sealed class Fixture(int mutant = 0) : TransportReceiverConformanceTests
	{
		private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
		private static readonly string[] ExpectedCompetitionTrace = ["first-entry", "second-entry", "release-first"];
		private readonly object _gate = new();
		private readonly List<(string Key, MessageAction Action)> _calls = [];
		private readonly List<string> _trace = [];
		private readonly TaskCompletionSource _firstEntry = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _secondInvocation = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly Queue<string> _pending = new();
		private bool _pause;
		private Exception? _failure;
		private CancellationToken _nativeToken;
		private int _nextKey;

		protected override Task<ITransportReceiver> CreateReceiverAsync()
		{
			var client = A.Fake<ISubscriberApiClientSeam>();
			A.CallTo(() => client.PullAsync(A<PullRequest>._, A<CancellationToken>._)).ReturnsLazily((PullRequest request, CancellationToken _) =>
			{
				var response = new PullResponse();
				while (_pending.Count > 0 && response.ReceivedMessages.Count < request.MaxMessages)
				{
					response.ReceivedMessages.Add(new ReceivedMessage { AckId = _pending.Dequeue(), Message = new PubsubMessage { MessageId = "same-public-id", Data = ByteString.CopyFromUtf8("payload") } });
				}
				return Task.FromResult(response);
			});
			A.CallTo(() => client.AcknowledgeAsync(A<string>._, A<IEnumerable<string>>._, A<CancellationToken>._)).ReturnsLazily((string source, IEnumerable<string> keys, CancellationToken token) =>
			{
				source.ShouldBe("subscription");
				return NativeAsync(keys.Single(), MessageAction.Acknowledge, token);
			});
			A.CallTo(() => client.ModifyAckDeadlineAsync(A<string>._, A<IEnumerable<string>>._, A<int>._, A<CancellationToken>._)).ReturnsLazily((string source, IEnumerable<string> keys, int seconds, CancellationToken token) =>
			{
				source.ShouldBe("subscription");
				seconds.ShouldBe(0);
				return NativeAsync(keys.Single(), MessageAction.Requeue, token);
			});
			return Task.FromResult<ITransportReceiver>(new PubSubTransportReceiver(client, "subscription", NullLogger<PubSubTransportReceiver>.Instance));
		}


		protected override Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
		{
			foreach (var unused in messages)
			{
				_pending.Enqueue("receipt-" + ++_nextKey);
			}
			return Task.CompletedTask;
		}

		private async Task NativeAsync(string key, MessageAction action, CancellationToken token)
		{
			bool first;
			lock (_gate)
			{
				first = _calls.Count == 0;
				if (_pause && !first)
				{
					_secondInvocation.TrySetResult();
					if (mutant == 1)
					{
						return;
					}
				}
				if (!_pause || first || mutant != 2)
				{
					_calls.Add((key, action));
					_trace.Add(first ? "first-entry" : "second-entry");
				}
				if (_pause && first)
				{
					_nativeToken = token;
					_firstEntry.TrySetResult();
				}
			}
			if (_pause && (first || mutant == 2))
			{
				await _release.Task.ConfigureAwait(false);
				if (!first)
				{
					lock (_gate)
					{
						_calls.Add((key, action));
						_trace.Add("second-entry");
					}
				}
			}
			if (key == "receipt-1" && _failure is { } failure)
			{
				_failure = null;
				throw failure;
			}
		}

		private Task ReleaseAsync()
		{
			lock (_gate)
			{
				if (!_release.Task.IsCompleted)
				{
					_trace.Add("release-first");
					_release.TrySetResult();
				}
			}
			return Task.CompletedTask;
		}

		private Task ObserveAsync(params (string Key, MessageAction Action)[] expected)
		{
			lock (_gate) { _calls.ToArray().ShouldBe(expected); }
			return Task.CompletedTask;
		}

		public Task RepeatedAsync(MessageAction action) => VerifyRepeatedSettlement(action, SettlementExpectation.Completed,
			(_, _) => ObserveAsync(("receipt-1", action == MessageAction.Reject ? MessageAction.Acknowledge : action)),
			(_, _) => ObserveAsync(("receipt-1", action == MessageAction.Reject ? MessageAction.Acknowledge : action), ("receipt-1", action == MessageAction.Reject ? MessageAction.Acknowledge : action)));

		public Task CancellationAsync(MessageAction action) => VerifySettlementCancellation(action, SettlementExpectation.Completed,
			(_, _) => { _pause = true; return Task.CompletedTask; }, () => _firstEntry.Task, ReleaseAsync,
			(_, _) =>
			{
				_nativeToken.CanBeCanceled.ShouldBeTrue();
				_nativeToken.IsCancellationRequested.ShouldBeFalse();
				return ObserveAsync(("receipt-1", action == MessageAction.Reject ? MessageAction.Acknowledge : action));
			}, Timeout);

		public Task CompetitionAsync() => VerifyCompetingSettlements(MessageAction.Acknowledge, MessageAction.Requeue,
			SettlementExpectation.Completed, SettlementExpectation.Completed,
			(_, _) => { _pause = true; return Task.CompletedTask; }, () => _firstEntry.Task,
			() => _secondInvocation.Task, ReleaseAsync,
			(_, _) =>
			{
				lock (_gate)
				{
					_trace.ToArray().ShouldBe(ExpectedCompetitionTrace);
				}
				return ObserveAsync(("receipt-1", MessageAction.Acknowledge), ("receipt-1", MessageAction.Requeue));
			}, Timeout);

		public Task BatchAsync(MessageAction action) => VerifySettlementBatchRetry(action, MessageAction.Acknowledge,
			(_, _) => { Exception cause = new IOException("Native response lost"); _failure = cause; return Task.FromResult(cause); },
			SettlementExpectation.Refused(TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Unspecified),
			SettlementExpectation.Completed,
			(_, _) => ObserveAsync(("receipt-1", action == MessageAction.Reject ? MessageAction.Acknowledge : action)),
			(_, _) => ObserveAsync(("receipt-1", action == MessageAction.Reject ? MessageAction.Acknowledge : action), ("receipt-2", MessageAction.Acknowledge), ("receipt-1", action == MessageAction.Reject ? MessageAction.Acknowledge : action)), Timeout);
	}
}

