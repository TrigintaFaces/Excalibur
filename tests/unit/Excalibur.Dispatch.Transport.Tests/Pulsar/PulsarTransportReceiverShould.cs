// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Exceptions;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Pulsar;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Pulsar;

/// <summary>
/// Unit tests for <see cref="PulsarTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="PulsarTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class PulsarTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "persistent://public/default/orders-topic";
	private static readonly MessageId NativeMessageId = new(1, 42, -1, -1, TestSource);
	private readonly IConsumer<byte[]> _fakeConsumer;
	private readonly PulsarTransportReceiver _sut;

	public PulsarTransportReceiverShould()
	{
		_fakeConsumer = A.Fake<IConsumer<byte[]>>();
		_sut = new PulsarTransportReceiver(
			_fakeConsumer,
			TestSource,
			NullLogger<PulsarTransportReceiver>.Instance,
			maxPayloadBytes: 1);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		await _fakeConsumer.DisposeAsync();
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_consumer_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PulsarTransportReceiver(null!, TestSource, NullLogger<PulsarTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PulsarTransportReceiver(A.Fake<IConsumer<byte[]>>(), null!, NullLogger<PulsarTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PulsarTransportReceiver(A.Fake<IConsumer<byte[]>>(), TestSource, null!));
	}

	[Fact]
	public void Return_consumer_via_GetService()
	{
		var result = _sut.GetService(typeof(IConsumer<byte[]>));
		result.ShouldBe(_fakeConsumer);
	}

	[Fact]
	public void Return_null_for_unknown_service_type()
	{
		var result = _sut.GetService(typeof(string));
		result.ShouldBeNull();
	}

	[Fact]
	public void Throw_when_GetService_type_is_null()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	// Regression for Excalibur_Dispatch-257j9n.2.3: settlement failures must cross the portable boundary.
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Translate_missing_and_wrong_type_receipts(bool reject, bool requeue)
	{
		foreach (var data in new[]
		{
			new Dictionary<string, object>(),
			new Dictionary<string, object> { ["pulsar.receipt_handle"] = 42 },
		})
		{
			var error = await Should.ThrowAsync<TransportSettlementException>(() =>
				SettleAsync(new TransportReceivedMessage { ProviderData = data }, reject, requeue));
			AssertClassification(error, SettlementRetryability.Permanent);
		}
		AssertNoNativeSettlement();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Fail_portably_for_unknown_receipt_instead_of_reporting_success(bool reject, bool requeue)
	{
		var message = new TransportReceivedMessage
		{
			ProviderData = new Dictionary<string, object> { ["pulsar.receipt_handle"] = "unknown" },
		};
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, requeue));
		AssertClassification(error, SettlementRetryability.Permanent);
		AssertNoNativeSettlement();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Settle_healthy_receipt_and_preserve_caller_token(bool reject, bool requeue)
	{
		var message = await ReceiveMessageAsync();
		using var caller = new CancellationTokenSource();
		await SettleAsync(message, reject, requeue, caller.Token);
		if (reject && requeue)
		{
			A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(
				A<IEnumerable<MessageId>>.That.Matches(ids => ids.SequenceEqual(new[] { NativeMessageId })), caller.Token))
				.MustHaveHappenedOnceExactly();
			A.CallTo(() => _fakeConsumer.Acknowledge(A<MessageId>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
		else
		{
			A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, caller.Token)).MustHaveHappenedOnceExactly();
			A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(A<IEnumerable<MessageId>>._, A<CancellationToken>._))
				.MustNotHaveHappened();
		}
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Reject_reused_receipt_without_repeating_native_settlement(bool reject, bool requeue)
	{
		var message = await ReceiveMessageAsync();
		await SettleAsync(message, reject, requeue);
		AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, requeue)),
			SettlementRetryability.Permanent);
		AssertNativeSettlementCount(reject, requeue, 1);
	}

	[Theory]
	[InlineData(false, false, false)]
	[InlineData(true, false, false)]
	[InlineData(true, true, false)]
	[InlineData(false, false, true)]
	[InlineData(true, false, true)]
	[InlineData(true, true, true)]
	public async Task Translate_native_failure_and_retain_receipt_until_success(bool reject, bool requeue, bool internalCancellation)
	{
		var message = await ReceiveMessageAsync();
		Exception cause = internalCancellation ? new OperationCanceledException("Provider timeout") : new ConsumerFaultedException();
		var calls = 0;
		if (reject && requeue)
		{
			A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(A<IEnumerable<MessageId>>._, A<CancellationToken>._))
				.ReturnsLazily(() => calls++ == 0 ? new ValueTask(Task.FromException(cause)) : ValueTask.CompletedTask);
		}
		else
		{
			A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._))
				.ReturnsLazily(() => calls++ == 0 ? new ValueTask(Task.FromException(cause)) : ValueTask.CompletedTask);
		}
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, requeue));
		AssertClassification(error, SettlementRetryability.Unspecified);
		error.InnerException.ShouldBeSameAs(cause);
		await SettleAsync(message, reject, requeue);
		AssertNativeSettlementCount(reject, requeue, 2);
		AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, requeue)),
			SettlementRetryability.Permanent);
		AssertNativeSettlementCount(reject, requeue, 2);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Preserve_already_portable_failure_identity_and_unsettled_receipt(bool reject, bool requeue)
	{
		var message = await ReceiveMessageAsync();
		var cause = new TransportSettlementException("Already classified provider boundary")
		{
			TransportName = "Pulsar",
			RedeliveryExpectation = TransportRedeliveryExpectation.Unspecified,
			Retryability = SettlementRetryability.Unspecified,
		};
		var calls = 0;
		if (reject && requeue)
		{
			A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(A<IEnumerable<MessageId>>._, A<CancellationToken>._))
				.ReturnsLazily(() => calls++ == 0 ? new ValueTask(Task.FromException(cause)) : ValueTask.CompletedTask);
		}
		else
		{
			A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._))
				.ReturnsLazily(() => calls++ == 0 ? new ValueTask(Task.FromException(cause)) : ValueTask.CompletedTask);
		}
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, requeue));
		error.ShouldBeSameAs(cause);
		await SettleAsync(message, reject, requeue);
		AssertNativeSettlementCount(reject, requeue, 2);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Preserve_caller_cancellation_and_unsettled_receipt(bool reject, bool requeue)
	{
		var message = await ReceiveMessageAsync();
		using var caller = new CancellationTokenSource();
		if (reject && requeue)
		{
			A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(A<IEnumerable<MessageId>>._, caller.Token))
				.ReturnsLazily(() =>
				{
					caller.Cancel();
					return new ValueTask(Task.FromCanceled(caller.Token));
				});
		}
		else
		{
			A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, caller.Token))
				.ReturnsLazily(() =>
				{
					caller.Cancel();
					return new ValueTask(Task.FromCanceled(caller.Token));
				});
		}
		var error = await Should.ThrowAsync<OperationCanceledException>(() => SettleAsync(message, reject, requeue, caller.Token));
		error.CancellationToken.ShouldBe(caller.Token);
		await SettleAsync(message, reject, requeue);
		AssertNativeSettlementCount(reject, requeue, 2);
	}

	// Cancellation attribution requires both a requested caller token and that exact token on the exception.
	// This is a conservative boundary convention, not a guarantee about SDK linked-token provenance.
	[Theory]
	[InlineData(false, false, "different-token")]
	[InlineData(true, false, "different-token")]
	[InlineData(true, true, "different-token")]
	[InlineData(false, false, "tokenless")]
	[InlineData(true, false, "tokenless")]
	[InlineData(true, true, "tokenless")]
	[InlineData(false, false, "unrequested-caller-token")]
	[InlineData(true, false, "unrequested-caller-token")]
	[InlineData(true, true, "unrequested-caller-token")]
	public async Task Translate_cancellation_without_exact_requested_caller_attribution(bool reject, bool requeue, string scenario)
	{
		var message = await ReceiveMessageAsync();
		using var caller = new CancellationTokenSource();
		using var providerTimeout = new CancellationTokenSource();
		providerTimeout.Cancel();
		var errorToken = scenario switch
		{
			"different-token" => providerTimeout.Token,
			"tokenless" => CancellationToken.None,
			_ => caller.Token,
		};
		var cause = new OperationCanceledException("Provider cancellation", errorToken);
		var calls = 0;
		ValueTask InvokeNative()
		{
			if (calls++ != 0)
			{
				return ValueTask.CompletedTask;
			}
			if (scenario != "unrequested-caller-token")
			{
				caller.Cancel();
			}
			return new ValueTask(Task.FromException(cause));
		}
		if (reject && requeue)
		{
			A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(A<IEnumerable<MessageId>>._, A<CancellationToken>._))
				.ReturnsLazily(InvokeNative);
		}
		else
		{
			A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._))
				.ReturnsLazily(InvokeNative);
		}
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, requeue, caller.Token));
		AssertClassification(error, SettlementRetryability.Unspecified);
		error.InnerException.ShouldBeSameAs(cause);
		await SettleAsync(message, reject, requeue);
		AssertNativeSettlementCount(reject, requeue, 2);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Honor_pre_canceled_caller_without_native_io_and_retain_receipt(bool reject, bool requeue)
	{
		var message = await ReceiveMessageAsync();
		using var caller = new CancellationTokenSource();
		caller.Cancel();
		var cancellation = await Should.ThrowAsync<OperationCanceledException>(() => SettleAsync(message, reject, requeue, caller.Token));
		cancellation.CancellationToken.ShouldBe(caller.Token);
		AssertNoNativeSettlement();
		await SettleAsync(message, reject, requeue);
		AssertNativeSettlementCount(reject, requeue, 1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Preserve_null_message_validation(bool reject)
	{
		await Should.ThrowAsync<ArgumentNullException>(() => SettleAsync(null!, reject, false));
		AssertNoNativeSettlement();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public async Task Serialize_opposing_settlements_and_release_owner_after_native_completion(bool firstRejects, bool ownerFails)
	{
		var message = await ReceiveMessageAsync();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var nativeCalls = ConfigurePausedNativeSettlement(entered, paused);
		Task? first = null;
		Task? opposing = null;
		try
		{
			first = SettleAsync(message, firstRejects, true);
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
			opposing = SettleAsync(message, !firstRejects, true);
			nativeCalls().ShouldBe(1);
			opposing.IsCompleted.ShouldBeFalse();
			if (ownerFails)
			{
				var cause = new ConsumerFaultedException();
				paused.SetException(cause);
				var error = await Should.ThrowAsync<TransportSettlementException>(() => first.WaitAsync(TimeSpan.FromSeconds(5)));
				AssertClassification(error, SettlementRetryability.Unspecified);
				error.InnerException.ShouldBeSameAs(cause);
				await opposing.WaitAsync(TimeSpan.FromSeconds(5));
				nativeCalls().ShouldBe(2);
			}
			else
			{
				paused.SetResult();
				await first.WaitAsync(TimeSpan.FromSeconds(5));
				AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => opposing.WaitAsync(TimeSpan.FromSeconds(5))),
					SettlementRetryability.Permanent);
				nativeCalls().ShouldBe(1);
			}
		}
		finally
		{
			paused.TrySetResult();
			await ObserveCompletedSettlementAsync(first);
			await ObserveCompletedSettlementAsync(opposing);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Cancel_waiter_without_native_io_or_consuming_active_owner_receipt(bool firstRejects)
	{
		var message = await ReceiveMessageAsync();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var nativeCalls = ConfigurePausedNativeSettlement(entered, paused);
		using var waiterCancellation = new CancellationTokenSource();
		Task? first = null;
		Task? waiter = null;
		try
		{
			first = SettleAsync(message, firstRejects, true);
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
			waiter = SettleAsync(message, !firstRejects, true, waiterCancellation.Token);
			nativeCalls().ShouldBe(1);
			waiterCancellation.Cancel();
			var cancellation = await Should.ThrowAsync<OperationCanceledException>(() => waiter.WaitAsync(TimeSpan.FromSeconds(5)));
			cancellation.CancellationToken.ShouldBe(waiterCancellation.Token);
			nativeCalls().ShouldBe(1);
			first.IsCompleted.ShouldBeFalse();
			paused.SetException(new ConsumerFaultedException());
			AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => first.WaitAsync(TimeSpan.FromSeconds(5))),
				SettlementRetryability.Unspecified);
			await SettleAsync(message, !firstRejects, true).WaitAsync(TimeSpan.FromSeconds(5));
			nativeCalls().ShouldBe(2);
		}
		finally
		{
			paused.TrySetResult();
			await ObserveCompletedSettlementAsync(first);
			await ObserveCompletedSettlementAsync(waiter);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Share_settlement_ownership_for_duplicate_receive_of_same_message_id(bool firstRejects)
	{
		var message = await ReceiveMessageAsync();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var nativeCalls = ConfigurePausedNativeSettlement(entered, paused);
		Task? first = null;
		Task? duplicateSettlement = null;
		try
		{
			first = SettleAsync(message, firstRejects, true);
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
			var duplicate = await ReceiveMessageAsync();
			duplicate.ProviderData["pulsar.receipt_handle"].ShouldBe(message.ProviderData["pulsar.receipt_handle"]);
			duplicateSettlement = SettleAsync(duplicate, !firstRejects, true);
			nativeCalls().ShouldBe(1);
			duplicateSettlement.IsCompleted.ShouldBeFalse();
			paused.SetResult();
			await first.WaitAsync(TimeSpan.FromSeconds(5));
			AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => duplicateSettlement.WaitAsync(TimeSpan.FromSeconds(5))),
				SettlementRetryability.Permanent);
			nativeCalls().ShouldBe(1);
			// A receive after successful removal is a new receipt entry and remains independently usable.
			var replacement = await ReceiveMessageAsync();
			await SettleAsync(replacement, !firstRejects, true).WaitAsync(TimeSpan.FromSeconds(5));
			nativeCalls().ShouldBe(2);
		}
		finally
		{
			paused.TrySetResult();
			await ObserveCompletedSettlementAsync(first);
			await ObserveCompletedSettlementAsync(duplicateSettlement);
		}
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public async Task Serialize_oversized_duplicate_acknowledgement_with_active_public_settlement(bool firstRejects, bool ownerFails)
	{
		var message = await ReceiveMessageAsync();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var nativeCalls = ConfigurePausedNativeSettlement(entered, paused);
		Task? first = null;
		Task<IReadOnlyList<TransportReceivedMessage>>? poisonReceive = null;
		try
		{
			first = SettleAsync(message, firstRejects, true);
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
			ConfigureNativeDelivery(new byte[2]);
			poisonReceive = _sut.ReceiveAsync(1, CancellationToken.None);
			nativeCalls().ShouldBe(1);
			poisonReceive.IsCompleted.ShouldBeFalse();
			if (ownerFails)
			{
				paused.SetException(new ConsumerFaultedException());
				AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => first.WaitAsync(TimeSpan.FromSeconds(5))),
					SettlementRetryability.Unspecified);
			}
			else
			{
				paused.SetResult();
				await first.WaitAsync(TimeSpan.FromSeconds(5));
			}
			(await poisonReceive.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeEmpty();
			nativeCalls().ShouldBe(ownerFails ? 2 : 1);
		}
		finally
		{
			paused.TrySetResult();
			await ObserveCompletedSettlementAsync(first);
			await ObserveCompletedSettlementAsync(poisonReceive);
		}
	}

	[Fact]
	public async Task Surface_oversized_acknowledgement_failure_and_allow_later_valid_duplicate_retry()
	{
		ConfigureNativeDelivery(new byte[2]);
		var cause = new ConsumerFaultedException();
		var calls = 0;
		A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._))
			.ReturnsLazily(() => calls++ == 0 ? new ValueTask(Task.FromException(cause)) : ValueTask.CompletedTask);
		var error = await Should.ThrowAsync<TransportSettlementException>(() => _sut.ReceiveAsync(1, CancellationToken.None));
		AssertClassification(error, SettlementRetryability.Unspecified);
		error.InnerException.ShouldBeSameAs(cause);
		var duplicate = await ReceiveMessageAsync();
		await _sut.AcknowledgeAsync(duplicate, CancellationToken.None);
		A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Surface_poison_provider_cancellation_when_caller_cancels_during_native_io(bool hasProviderToken)
	{
		ConfigureNativeDelivery(new byte[2]);
		using var caller = new CancellationTokenSource();
		using var providerTimeout = new CancellationTokenSource();
		providerTimeout.Cancel();
		var cause = new OperationCanceledException("Poison provider timeout", hasProviderToken ? providerTimeout.Token : CancellationToken.None);
		var calls = 0;
		A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (calls++ != 0)
				{
					return ValueTask.CompletedTask;
				}
				caller.Cancel();
				return new ValueTask(Task.FromException(cause));
			});
		var error = await Should.ThrowAsync<TransportSettlementException>(() => _sut.ReceiveAsync(1, caller.Token));
		AssertClassification(error, SettlementRetryability.Unspecified);
		error.InnerException.ShouldBeSameAs(cause);
		var duplicate = await ReceiveMessageAsync();
		await _sut.AcknowledgeAsync(duplicate, CancellationToken.None);
		A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Refuse_old_delivery_after_same_native_id_is_received_again(bool reject, bool requeue)
	{
		var original = await ReceiveMessageAsync();
		await _sut.RejectAsync(original, "retry delivery", true, CancellationToken.None);
		A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(
			A<IEnumerable<MessageId>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		var replacement = await ReceiveMessageAsync();
		replacement.ProviderData["pulsar.receipt_handle"].ShouldBe(original.ProviderData["pulsar.receipt_handle"]);

		// A broker identity can recur; an old envelope must not settle the new delivery's lifetime.
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(original, reject, requeue));
		AssertClassification(error, SettlementRetryability.Permanent);
		A.CallTo(() => _fakeConsumer.Acknowledge(A<MessageId>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(
			A<IEnumerable<MessageId>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

		await _sut.AcknowledgeAsync(replacement, CancellationToken.None);
		A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(
			A<IEnumerable<MessageId>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(false, false, false)]
	[InlineData(true, false, false)]
	[InlineData(true, true, false)]
	[InlineData(false, false, true)]
	[InlineData(true, false, true)]
	[InlineData(true, true, true)]
	public async Task Refuse_receipt_handle_without_original_lifetime_capability(bool reject, bool requeue, bool wrongType)
	{
		var original = await ReceiveMessageAsync();
		var copied = original.ProviderData.ToDictionary(pair => pair.Key, pair => pair.Value);
		copied.Remove("pulsar.receipt_entry");
		if (wrongType)
		{
			copied["pulsar.receipt_entry"] = "fabricated lifetime";
		}
		var unbound = new TransportReceivedMessage { Id = original.Id, ProviderData = copied };
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(unbound, reject, requeue));
		AssertClassification(error, SettlementRetryability.Permanent);
		AssertNoNativeSettlement();

		// A rejected unbound envelope must leave the original lifetime usable.
		await _sut.AcknowledgeAsync(original, CancellationToken.None);
		A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	private Func<int> ConfigurePausedNativeSettlement(TaskCompletionSource entered, TaskCompletionSource paused)
	{
		var calls = 0;
		ValueTask InvokeNative()
		{
			if (Interlocked.Increment(ref calls) == 1)
			{
				entered.TrySetResult();
				return new ValueTask(paused.Task);
			}
			return ValueTask.CompletedTask;
		}
		A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._)).ReturnsLazily(InvokeNative);
		A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(A<IEnumerable<MessageId>>._, A<CancellationToken>._))
			.ReturnsLazily(InvokeNative);
		return () => Volatile.Read(ref calls);
	}

	private static async Task ObserveCompletedSettlementAsync(Task? settlement)
	{
		if (settlement is not null)
		{
			await settlement.ContinueWith(static completed => { _ = completed.Exception; }, CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).WaitAsync(TimeSpan.FromSeconds(5));
		}
	}

	private async Task<TransportReceivedMessage> ReceiveMessageAsync()
	{
		ConfigureNativeDelivery(Array.Empty<byte>());
		return (await _sut.ReceiveAsync(1, CancellationToken.None))[0];
	}

	private void ConfigureNativeDelivery(byte[] payload)
	{
		var delivery = A.Fake<IMessage<byte[]>>();
		A.CallTo(() => delivery.MessageId).Returns(NativeMessageId);
		A.CallTo(() => delivery.Value()).Returns(payload);
		A.CallTo(() => delivery.Properties).Returns(new Dictionary<string, string>());
		A.CallTo(() => _fakeConsumer.Receive(A<CancellationToken>._)).Returns(new ValueTask<IMessage<byte[]>>(delivery));
	}

	private Task SettleAsync(TransportReceivedMessage message, bool reject, bool requeue, CancellationToken cancellationToken = default) =>
		reject ? _sut.RejectAsync(message, "test", requeue, cancellationToken) : _sut.AcknowledgeAsync(message, cancellationToken);

	private void AssertNoNativeSettlement()
	{
		A.CallTo(() => _fakeConsumer.Acknowledge(A<MessageId>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(A<IEnumerable<MessageId>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private void AssertNativeSettlementCount(bool reject, bool requeue, int count)
	{
		if (reject && requeue)
		{
			A.CallTo(() => _fakeConsumer.RedeliverUnacknowledgedMessages(A<IEnumerable<MessageId>>._, A<CancellationToken>._))
				.MustHaveHappened(count, Times.Exactly);
		}
		else
		{
			A.CallTo(() => _fakeConsumer.Acknowledge(NativeMessageId, A<CancellationToken>._)).MustHaveHappened(count, Times.Exactly);
		}
	}

	private static void AssertClassification(TransportSettlementException error, SettlementRetryability retryability)
	{
		error.TransportName.ShouldBe("Pulsar");
		error.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		error.Retryability.ShouldBe(retryability);
	}
}
