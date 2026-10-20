// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.ServiceBus;

/// <summary>
/// Unit tests for <see cref="ServiceBusTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="ServiceBusTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class ServiceBusTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "orders-queue";
	private readonly IServiceBusReceiverSeam _fakeReceiver;
	private readonly ServiceBusTransportReceiver _sut;

	public ServiceBusTransportReceiverShould()
	{
		_fakeReceiver = A.Fake<IServiceBusReceiverSeam>();
		_sut = new ServiceBusTransportReceiver(
			_fakeReceiver,
			TestSource,
			NullLogger<ServiceBusTransportReceiver>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		await _fakeReceiver.DisposeAsync();
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_receiver_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportReceiver((IServiceBusReceiverSeam)null!, TestSource, NullLogger<ServiceBusTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportReceiver(A.Fake<IServiceBusReceiverSeam>(), null!, NullLogger<ServiceBusTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportReceiver(A.Fake<IServiceBusReceiverSeam>(), TestSource, null!));
	}

	[Fact]
	public void Return_receiver_seam_via_GetService()
	{
		var result = _sut.GetService(typeof(IServiceBusReceiverSeam));
		result.ShouldBe(_fakeReceiver);
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

	// Excalibur_Dispatch-257j9n.2.3: settlement failures must cross the portable boundary.
	// Operation 0 completes, 1 abandons, and 2 dead-letters the received native message.
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task Preserve_native_settlement_despite_canceled_caller(int operation)
	{
		var (message, native) = await ReceiveMessageAsync();
		using var caller = new CancellationTokenSource();
		caller.Cancel();
		await SettleAsync(message, operation, caller.Token);
		if (operation == 0)
		{
			A.CallTo(() => _fakeReceiver.CompleteMessageAsync(native,
				A<CancellationToken>.That.Matches(token => token.CanBeCanceled && !token.IsCancellationRequested && token != caller.Token)))
				.MustHaveHappenedOnceExactly();
		}
		else if (operation == 1)
		{
			A.CallTo(() => _fakeReceiver.AbandonMessageAsync(native,
				A<CancellationToken>.That.Matches(token => token.CanBeCanceled && !token.IsCancellationRequested && token != caller.Token)))
				.MustHaveHappenedOnceExactly();
		}
		else
		{
			A.CallTo(() => _fakeReceiver.DeadLetterMessageAsync(native, "Rejected", "test reason",
				A<CancellationToken>.That.Matches(token => token.CanBeCanceled && !token.IsCancellationRequested && token != caller.Token)))
				.MustHaveHappenedOnceExactly();
		}
		AssertNativeCallCount(operation);
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(1, false)]
	[InlineData(2, false)]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	public async Task Translate_asynchronous_native_failure_without_retrying_consumed_receipt(int operation, bool canceled)
	{
		var (message, native) = await ReceiveMessageAsync();
		Exception cause = canceled
			? new OperationCanceledException("Internal timeout")
			: new ServiceBusException("Native settlement failed", ServiceBusFailureReason.ServiceCommunicationProblem, TestSource, null);
		ConfigureNativeResult(native, operation, Task.FromException(cause));
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, operation));
		AssertClassification(error);
		error.InnerException.ShouldBeSameAs(cause);
		AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, operation)));
		AssertNativeCallCount(operation);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task Translate_missing_wrong_unknown_and_reused_receipts(int operation)
	{
		foreach (var data in new[]
		{
			new Dictionary<string, object>(),
			new Dictionary<string, object> { ["azure.lock_token"] = 42 },
			new Dictionary<string, object> { ["azure.lock_token"] = "unknown" },
		})
		{
			AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() =>
				SettleAsync(new TransportReceivedMessage { ProviderData = data }, operation)));
		}
		AssertNoNativeSettlement();
		var (message, _) = await ReceiveMessageAsync();
		await SettleAsync(message, operation);
		AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, operation)));
		AssertNativeCallCount(operation);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task Allow_only_one_pending_settlement_owner(int operation)
	{
		var (message, native) = await ReceiveMessageAsync();
		var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		ConfigureNativeResult(native, operation, pending.Task);
		var first = SettleAsync(message, operation);
		try
		{
			AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, operation == 0 ? 1 : 0)));
			AssertNativeCallCount(operation);
		}
		finally
		{
			pending.SetResult();
			await first;
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task Preserve_null_message_validation(int operation)
	{
		await Should.ThrowAsync<ArgumentNullException>(() => SettleAsync(null!, operation));
		AssertNoNativeSettlement();
	}

	[Theory]
	[InlineData(0, 10_000, 1)]
	[InlineData(1, 10_000, 1)]
	[InlineData(2, 10_000, 1)]
	[InlineData(0, 10_001, 1)]
	[InlineData(1, 10_001, 1)]
	[InlineData(2, 10_001, 1)]
	[InlineData(0, 10_001, 2)]
	[InlineData(1, 10_001, 2)]
	[InlineData(2, 10_001, 2)]
	public async Task RetainEveryReceivedNativeReceiptAcrossCacheWarningThreshold(int operation, int totalMessages, int finalBatchSize)
	{
		// Receive every receipt through the native seam, using valid batches of at most 1,000.
		// The two-message final batch crosses from 9,999 to 10,001 within a single receive call.
		var native = Enumerable.Range(0, totalMessages).Select(index => ServiceBusModelFactory.ServiceBusReceivedMessage(
			body: BinaryData.FromBytes(Array.Empty<byte>()), messageId: $"retained-{index}", lockTokenGuid: Guid.NewGuid())).ToArray();
		var next = 0;
		A.CallTo(() => _fakeReceiver.ReceiveMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily((int requested, CancellationToken _) =>
			{
				requested.ShouldBeInRange(1, 1_000);
				var batch = native.Skip(next).Take(requested).ToArray();
				next += batch.Length;
				return Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>(batch);
			});
		TransportReceivedMessage? earliest = null;
		var prefixCount = totalMessages - finalBatchSize;
		while (next < prefixCount)
		{
			var requested = Math.Min(1_000, prefixCount - next);
			var received = await _sut.ReceiveAsync(requested, CancellationToken.None);
			received.Count.ShouldBe(requested);
			earliest ??= received[0];
		}
		var finalBatch = await _sut.ReceiveAsync(finalBatchSize, CancellationToken.None);
		finalBatch.Count.ShouldBe(finalBatchSize);
		foreach (var receipt in finalBatch)
		{
			await SettleAsync(receipt, operation);
			AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(receipt, operation)));
		}

		// Keeping the newest receipt must not evict an earlier receipt that still owns native settlement.
		earliest.ShouldNotBeNull();
		await _sut.AcknowledgeAsync(earliest, CancellationToken.None);
		A.CallTo(() => _fakeReceiver.CompleteMessageAsync(native[0], A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		foreach (var overflowNative in native.Skip(prefixCount))
		{
			if (operation == 0)
			{
				A.CallTo(() => _fakeReceiver.CompleteMessageAsync(overflowNative, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
				A.CallTo(() => _fakeReceiver.AbandonMessageAsync(overflowNative, A<CancellationToken>._)).MustNotHaveHappened();
				A.CallTo(() => _fakeReceiver.DeadLetterMessageAsync(overflowNative, A<string?>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
			}
			else if (operation == 1)
			{
				A.CallTo(() => _fakeReceiver.AbandonMessageAsync(overflowNative, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
				A.CallTo(() => _fakeReceiver.CompleteMessageAsync(overflowNative, A<CancellationToken>._)).MustNotHaveHappened();
				A.CallTo(() => _fakeReceiver.DeadLetterMessageAsync(overflowNative, A<string?>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
			}
			else
			{
				A.CallTo(() => _fakeReceiver.DeadLetterMessageAsync(overflowNative, "Rejected", "test reason", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
				A.CallTo(() => _fakeReceiver.CompleteMessageAsync(overflowNative, A<CancellationToken>._)).MustNotHaveHappened();
				A.CallTo(() => _fakeReceiver.AbandonMessageAsync(overflowNative, A<CancellationToken>._)).MustNotHaveHappened();
		}
		}
	}

	private async Task<(TransportReceivedMessage Message, ServiceBusReceivedMessage Native)> ReceiveMessageAsync()
	{
		var native = ServiceBusModelFactory.ServiceBusReceivedMessage(
			body: BinaryData.FromBytes(Array.Empty<byte>()), messageId: "settlement", lockTokenGuid: Guid.NewGuid());
		A.CallTo(() => _fakeReceiver.ReceiveMessagesAsync(1, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>(new[] { native }));
		return ((await _sut.ReceiveAsync(1, CancellationToken.None))[0], native);
	}

	private Task SettleAsync(TransportReceivedMessage message, int operation, CancellationToken cancellationToken = default) =>
		operation == 0 ? _sut.AcknowledgeAsync(message, cancellationToken) : _sut.RejectAsync(message, "test reason", operation == 1, cancellationToken);

	private void ConfigureNativeResult(ServiceBusReceivedMessage native, int operation, Task result)
	{
		if (operation == 0)
		{
			A.CallTo(() => _fakeReceiver.CompleteMessageAsync(native, A<CancellationToken>._)).Returns(result);
		}
		else if (operation == 1)
		{
			A.CallTo(() => _fakeReceiver.AbandonMessageAsync(native, A<CancellationToken>._)).Returns(result);
		}
		else
		{
			A.CallTo(() => _fakeReceiver.DeadLetterMessageAsync(native, "Rejected", "test reason", A<CancellationToken>._)).Returns(result);
		}
	}

	private void AssertNativeCallCount(int operation)
	{
		if (operation == 0)
		{
			A.CallTo(() => _fakeReceiver.CompleteMessageAsync(A<ServiceBusReceivedMessage>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => _fakeReceiver.AbandonMessageAsync(A<ServiceBusReceivedMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
			A.CallTo(() => _fakeReceiver.DeadLetterMessageAsync(A<ServiceBusReceivedMessage>._, A<string?>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
		else if (operation == 1)
		{
			A.CallTo(() => _fakeReceiver.AbandonMessageAsync(A<ServiceBusReceivedMessage>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => _fakeReceiver.CompleteMessageAsync(A<ServiceBusReceivedMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
			A.CallTo(() => _fakeReceiver.DeadLetterMessageAsync(A<ServiceBusReceivedMessage>._, A<string?>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
		else
		{
			A.CallTo(() => _fakeReceiver.DeadLetterMessageAsync(A<ServiceBusReceivedMessage>._, A<string?>._, A<string?>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => _fakeReceiver.CompleteMessageAsync(A<ServiceBusReceivedMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
			A.CallTo(() => _fakeReceiver.AbandonMessageAsync(A<ServiceBusReceivedMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
	}

	private void AssertNoNativeSettlement()
	{
		A.CallTo(() => _fakeReceiver.CompleteMessageAsync(A<ServiceBusReceivedMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _fakeReceiver.AbandonMessageAsync(A<ServiceBusReceivedMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _fakeReceiver.DeadLetterMessageAsync(A<ServiceBusReceivedMessage>._, A<string?>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	private static void AssertClassification(TransportSettlementException error)
	{
		error.TransportName.ShouldBe("AzureServiceBus");
		error.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		error.Retryability.ShouldBe(SettlementRetryability.Permanent);
	}
}
