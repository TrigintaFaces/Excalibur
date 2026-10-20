// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Unit tests for <see cref="RabbitMqTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="RabbitMqTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class RabbitMqTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "orders-queue";
	private const string TestQueueName = "orders-queue";
	private readonly IChannel _fakeChannel;
	private readonly RabbitMqTransportReceiver _sut;

	public RabbitMqTransportReceiverShould()
	{
		_fakeChannel = A.Fake<IChannel>();
		_sut = new RabbitMqTransportReceiver(
			_fakeChannel,
			TestSource,
			TestQueueName,
			NullLogger<RabbitMqTransportReceiver>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		_fakeChannel.Dispose();
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_channel_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportReceiver(null!, TestSource, TestQueueName, NullLogger<RabbitMqTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportReceiver(A.Fake<IChannel>(), null!, TestQueueName, NullLogger<RabbitMqTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportReceiver(A.Fake<IChannel>(), TestSource, TestQueueName, null!));
	}

	[Fact]
	public void Return_channel_via_GetService()
	{
		var result = _sut.GetService(typeof(IChannel));
		result.ShouldBe(_fakeChannel);
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

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Settle_with_native_parameters_despite_caller_cancellation(bool reject, bool requeue)
	{
		var message = await ReceiveMessageAsync();
		using var caller = new CancellationTokenSource();
		caller.Cancel();
		await SettleAsync(message, reject, requeue, caller.Token);
		if (reject)
		{
			A.CallTo(() => _fakeChannel.BasicNackAsync(42, false, requeue,
				A<CancellationToken>.That.Matches(token => token.CanBeCanceled && !token.IsCancellationRequested && token != caller.Token)))
				.MustHaveHappenedOnceExactly();
		}
		else
		{
			A.CallTo(() => _fakeChannel.BasicAckAsync(42, false,
				A<CancellationToken>.That.Matches(token => token.CanBeCanceled && !token.IsCancellationRequested && token != caller.Token)))
				.MustHaveHappenedOnceExactly();
		}
	}

	// Regression for Excalibur_Dispatch-257j9n.2.3: native errors escaped the portable boundary.
	[Theory]
	[InlineData(false, false, false)]
	[InlineData(true, false, false)]
	[InlineData(true, true, false)]
	[InlineData(false, false, true)]
	[InlineData(true, false, true)]
	[InlineData(true, true, true)]
	public async Task Translate_native_failure_and_consume_receipt(bool reject, bool requeue, bool canceled)
	{
		var message = await ReceiveMessageAsync();
		Exception cause = canceled ? new OperationCanceledException("Internal timeout") : new InvalidOperationException("Channel failed");
		if (reject)
		{
			A.CallTo(() => _fakeChannel.BasicNackAsync(42, false, requeue, A<CancellationToken>._)).Returns(new ValueTask(Task.FromException(cause)));
		}
		else
		{
			A.CallTo(() => _fakeChannel.BasicAckAsync(42, false, A<CancellationToken>._)).Returns(new ValueTask(Task.FromException(cause)));
		}
		var error = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, requeue));
		AssertClassification(error);
		error.InnerException.ShouldBeSameAs(cause);
		var repeated = await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, requeue));
		AssertClassification(repeated);
		if (reject)
		{
			A.CallTo(() => _fakeChannel.BasicNackAsync(42, false, requeue, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		}
		else
		{
			A.CallTo(() => _fakeChannel.BasicAckAsync(42, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		}
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Translate_missing_malformed_unknown_and_reused_receipts(bool reject, bool requeue)
	{
		foreach (var data in new[]
		{
			new Dictionary<string, object>(),
			new Dictionary<string, object> { ["rabbitmq.receipt_handle"] = 42 },
			new Dictionary<string, object> { ["rabbitmq.receipt_handle"] = "unknown" },
		})
		{
			var error = await Should.ThrowAsync<TransportSettlementException>(() =>
				SettleAsync(new TransportReceivedMessage { ProviderData = data }, reject, requeue));
			AssertClassification(error);
		}
		var message = await ReceiveMessageAsync();
		await SettleAsync(message, reject, requeue);
		AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, reject, requeue)));
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Allow_only_one_concurrent_settlement_owner(bool reject, bool requeue)
	{
		var message = await ReceiveMessageAsync();
		var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		if (reject)
		{
			A.CallTo(() => _fakeChannel.BasicNackAsync(42, false, requeue, A<CancellationToken>._)).Returns(new ValueTask(pending.Task));
		}
		else
		{
			A.CallTo(() => _fakeChannel.BasicAckAsync(42, false, A<CancellationToken>._)).Returns(new ValueTask(pending.Task));
		}
		var first = SettleAsync(message, reject, requeue);
		try
		{
			AssertClassification(await Should.ThrowAsync<TransportSettlementException>(() => SettleAsync(message, !reject, true)));
			if (reject)
			{
				A.CallTo(() => _fakeChannel.BasicAckAsync(42, false, A<CancellationToken>._)).MustNotHaveHappened();
			}
			else
			{
				A.CallTo(() => _fakeChannel.BasicNackAsync(42, false, true, A<CancellationToken>._)).MustNotHaveHappened();
			}
		}
		finally
		{
			pending.SetResult();
			await first;
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Preserve_null_message_validation(bool reject)
	{
		await Should.ThrowAsync<ArgumentNullException>(() => SettleAsync(null!, reject, false));
	}

	private async Task<TransportReceivedMessage> ReceiveMessageAsync()
	{
		var delivery = new BasicGetResult(42, false, "", TestQueueName, 0, new global::RabbitMQ.Client.BasicProperties { MessageId = "settlement" }, ReadOnlyMemory<byte>.Empty);
		A.CallTo(() => _fakeChannel.BasicGetAsync(TestQueueName, false, A<CancellationToken>._)).Returns(delivery);
		return (await _sut.ReceiveAsync(1, CancellationToken.None))[0];
	}

	private Task SettleAsync(TransportReceivedMessage message, bool reject, bool requeue, CancellationToken cancellationToken = default) =>
		reject ? _sut.RejectAsync(message, "test", requeue, cancellationToken) : _sut.AcknowledgeAsync(message, cancellationToken);

	private static void AssertClassification(TransportSettlementException error)
	{
		error.TransportName.ShouldBe("RabbitMQ");
		error.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		error.Retryability.ShouldBe(SettlementRetryability.Permanent);
	}
}
