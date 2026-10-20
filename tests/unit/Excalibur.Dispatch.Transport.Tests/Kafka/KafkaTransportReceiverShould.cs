// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Unit tests for <see cref="KafkaTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="KafkaTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class KafkaTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "orders-topic";
	private static readonly TopicPartition Partition0 = new(TestSource, new Partition(0));
	private readonly KafkaPartitionProgress _progress = new();
	private readonly IConsumer<string, byte[]> _fakeConsumer;
	private readonly KafkaTransportReceiver _sut;

	public KafkaTransportReceiverShould()
	{
		_fakeConsumer = A.Fake<IConsumer<string, byte[]>>();
		_progress.OnPartitionsAssigned([Partition0]);
		_sut = new KafkaTransportReceiver(
			_fakeConsumer,
			TestSource,
			NullLogger<KafkaTransportReceiver>.Instance,
			progress: _progress);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		_fakeConsumer.Dispose();
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
			new KafkaTransportReceiver(null!, TestSource, NullLogger<KafkaTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KafkaTransportReceiver(A.Fake<IConsumer<string, byte[]>>(), null!, NullLogger<KafkaTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KafkaTransportReceiver(A.Fake<IConsumer<string, byte[]>>(), TestSource, null!));
	}

	[Fact]
	public void Return_consumer_via_GetService()
	{
		var result = _sut.GetService(typeof(IConsumer<string, byte[]>));
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

	// Regression for Excalibur_Dispatch-257j9n.2.3: stale or failed native settlements must not report success.
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Refuse_stale_terminal_receipt_portably(bool reject)
	{
		var message = await ReceiveMessageAsync();
		_progress.OnPartitionsRevoked([Partition0]);
		_progress.OnPartitionsAssigned([Partition0]);
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, false));
		AssertClassification(error, SettlementRetryability.Permanent);
		A.CallTo(() => _fakeConsumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Translate_commit_failure_and_keep_receipt_for_terminal_retry(bool reject)
	{
		var message = await ReceiveMessageAsync();
		var cause = new KafkaException(ErrorCode.Local_State);
		var calls = ConfigureCommitFailureThenSuccess(cause);
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, false));
		AssertClassification(error, SettlementRetryability.Unspecified);
		error.InnerException.ShouldBeSameAs(cause);
		await SettleAsync(message, reject, false);
		calls().ShouldBe(2);
		AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, false)),
			SettlementRetryability.Permanent);
		calls().ShouldBe(2);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Refuse_requeue_after_uncertain_commit_and_preserve_terminal_retry(bool reject)
	{
		var message = await ReceiveMessageAsync();
		var cause = new KafkaException(ErrorCode.Local_State);
		var calls = ConfigureCommitFailureThenSuccess(cause);
		var failedCommit = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, false));
		failedCommit.InnerException.ShouldBeSameAs(cause);
		var refusal = await Should.ThrowAsync<TransportSettlementException>(() => _sut.RejectAsync(message, "retry", true, CancellationToken.None));
		AssertClassification(refusal, SettlementRetryability.Permanent);
		A.CallTo(() => _fakeConsumer.Seek(A<TopicPartitionOffset>._)).MustNotHaveHappened();
		await SettleAsync(message, reject, false);
		calls().ShouldBe(2);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Avoid_claiming_expected_redelivery_for_stale_receipt_after_commit_uncertainty(bool reject)
	{
		var message = await ReceiveMessageAsync();
		_ = ConfigureCommitFailureThenSuccess(new KafkaException(ErrorCode.Local_State));
		AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, false)),
			SettlementRetryability.Unspecified);
		_progress.OnPartitionsRevoked([Partition0]);
		_progress.OnPartitionsAssigned([Partition0]);
		var refusal = await Should.ThrowAsync<TransportSettlementException>(() => _sut.RejectAsync(message, "retry", true, CancellationToken.None));
		AssertClassification(refusal, SettlementRetryability.Permanent);
		A.CallTo(() => _fakeConsumer.Seek(A<TopicPartitionOffset>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task Translate_seek_failure_and_keep_receipt_for_requeue_retry()
	{
		var message = await ReceiveMessageAsync();
		var cause = new KafkaException(ErrorCode.Local_State);
		var calls = 0;
		A.CallTo(() => _fakeConsumer.Seek(A<TopicPartitionOffset>._)).Invokes(() =>
		{
			if (calls++ == 0)
			{
				throw cause;
			}
		});
		var error = await Should.ThrowAsync<TransportSettlementException>(() => _sut.RejectAsync(message, "retry", true, CancellationToken.None));
		AssertClassification(error, SettlementRetryability.Unspecified);
		error.InnerException.ShouldBeSameAs(cause);
		await _sut.RejectAsync(message, "retry", true, CancellationToken.None);
		calls.ShouldBe(2);
		A.CallTo(() => _fakeConsumer.Seek(A<TopicPartitionOffset>.That.Matches(offset => offset.TopicPartition.Equals(Partition0) && offset.Offset.Value == 7)))
			.MustHaveHappenedTwiceExactly();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Translate_malformed_receipts_portably_without_native_io(bool reject, bool requeue)
	{
		foreach (var data in new[]
		{
			new Dictionary<string, object>(),
			new Dictionary<string, object> { ["kafka.receipt_handle"] = 42 },
			new Dictionary<string, object> { ["kafka.receipt_handle"] = "unknown" },
		})
		{
			var error = await Should.ThrowAsync<TransportSettlementException>(() =>
				SettleAsync(new TransportReceivedMessage { ProviderData = data }, reject, requeue));
			AssertClassification(error, SettlementRetryability.Permanent);
		}
		A.CallTo(() => _fakeConsumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustNotHaveHappened();
		A.CallTo(() => _fakeConsumer.Seek(A<TopicPartitionOffset>._)).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Preserve_already_portable_exception_identity(bool reject, bool requeue)
	{
		var message = await ReceiveMessageAsync();
		var cause = new TransportSettlementException("Already classified native boundary")
		{
			TransportName = "Kafka",
			RedeliveryExpectation = TransportRedeliveryExpectation.Unspecified,
			Retryability = SettlementRetryability.Unspecified,
		};
		if (requeue)
		{
			A.CallTo(() => _fakeConsumer.Seek(A<TopicPartitionOffset>._)).Throws(cause);
		}
		else
		{
			A.CallTo(() => _fakeConsumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).Throws(cause);
		}
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, requeue));
		error.ShouldBeSameAs(cause);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Preserve_synchronous_native_settlement_with_canceled_caller(bool reject, bool requeue)
	{
		var message = await ReceiveMessageAsync();
		using var caller = new CancellationTokenSource();
		caller.Cancel();
		await SettleAsync(message, reject, requeue, caller.Token);
		if (requeue)
		{
			A.CallTo(() => _fakeConsumer.Seek(A<TopicPartitionOffset>._)).MustHaveHappenedOnceExactly();
		}
		else
		{
			A.CallTo(() => _fakeConsumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustHaveHappenedOnceExactly();
		}
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task RefuseMissingOrWrongDeliveryCapability_WithoutReplacingTheUsableReceipt(bool reject, bool requeue)
	{
		var original = await ReceiveMessageAsync();
		foreach (var capability in new object?[] { null, 42 })
		{
			var data = new Dictionary<string, object>(original.ProviderData);
			data.Remove("kafka.receipt_entry");
			if (capability is not null)
			{
				data["kafka.receipt_entry"] = capability;
			}
			var error = await Should.ThrowAsync<TransportSettlementException>(() =>
				SettleAsync(new TransportReceivedMessage { ProviderData = data }, reject, requeue));
			AssertClassification(error, SettlementRetryability.Permanent);
		}
		A.CallTo(() => _fakeConsumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustNotHaveHappened();
		A.CallTo(() => _fakeConsumer.Seek(A<TopicPartitionOffset>._)).MustNotHaveHappened();
		await _sut.AcknowledgeAsync(original, CancellationToken.None);
		A.CallTo(() => _fakeConsumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustHaveHappenedOnceExactly();
	}

	private Func<int> ConfigureCommitFailureThenSuccess(Exception cause)
	{
		var calls = 0;
		A.CallTo(() => _fakeConsumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).Invokes(() =>
		{
			if (calls++ == 0)
			{
				throw cause;
			}
		});
		return () => calls;
	}

	private async Task<TransportReceivedMessage> ReceiveMessageAsync()
	{
		A.CallTo(() => _fakeConsumer.Consume(A<TimeSpan>._)).Returns(new Confluent.Kafka.ConsumeResult<string, byte[]>
		{
			Topic = TestSource,
			Partition = new Partition(0),
			Offset = new Offset(7),
			Message = new Message<string, byte[]> { Key = "settlement", Value = Array.Empty<byte>() },
		});
		return (await _sut.ReceiveAsync(1, CancellationToken.None))[0];
	}

	private Task SettleAsync(TransportReceivedMessage message, bool reject, bool requeue, CancellationToken cancellationToken = default) =>
		reject ? _sut.RejectAsync(message, "test", requeue, cancellationToken) : _sut.AcknowledgeAsync(message, cancellationToken);

	private static void AssertClassification(TransportSettlementException error, SettlementRetryability retryability)
	{
		error.TransportName.ShouldBe("Kafka");
		error.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		error.Retryability.ShouldBe(retryability);
	}
}
