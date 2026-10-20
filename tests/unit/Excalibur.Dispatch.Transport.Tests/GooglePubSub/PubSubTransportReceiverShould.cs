// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Unit tests for <see cref="PubSubTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="PubSubTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "TRANSPORT")]
public sealed class PubSubTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "projects/test-project/subscriptions/orders-subscription";
	private static readonly string[] ExpectedAckIds = ["ack-id-1"];
	private readonly ISubscriberApiClientSeam _fakeClient;
	private readonly PubSubTransportReceiver _sut;

	public PubSubTransportReceiverShould()
	{
		_fakeClient = A.Fake<ISubscriberApiClientSeam>();
		_sut = new PubSubTransportReceiver(
			_fakeClient,
			TestSource,
			NullLogger<PubSubTransportReceiver>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_client_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportReceiver((ISubscriberApiClientSeam)null!, TestSource, NullLogger<PubSubTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportReceiver(A.Fake<ISubscriberApiClientSeam>(), null!, NullLogger<PubSubTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportReceiver(A.Fake<ISubscriberApiClientSeam>(), TestSource, null!));
	}

	[Fact]
	public void Return_client_seam_via_GetService()
	{
		var result = _sut.GetService(typeof(ISubscriberApiClientSeam));
		result.ShouldBe(_fakeClient);
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
	[InlineData("acknowledge", false, false)]
	[InlineData("reject", false, false)]
	[InlineData("requeue", false, false)]
	[InlineData("acknowledge", true, false)]
	[InlineData("reject", true, false)]
	[InlineData("requeue", true, false)]
	[InlineData("acknowledge", false, true)]
	[InlineData("reject", false, true)]
	[InlineData("requeue", false, true)]
	[InlineData("acknowledge", true, true)]
	[InlineData("reject", true, true)]
	[InlineData("requeue", true, true)]
	public async Task WrapNativeSettlementFailureWithPortableUncertainty(string action, bool cancelCaller, bool timeout)
	{
		using var caller = new CancellationTokenSource();
		if (cancelCaller) caller.Cancel();
		Exception native = timeout ? new OperationCanceledException("internal settlement timeout")
			: new global::Grpc.Core.RpcException(new global::Grpc.Core.Status(global::Grpc.Core.StatusCode.Unavailable, "response lost"));
		ArrangeFault(native);
		var message = Message();

		var thrown = await Should.ThrowAsync<TransportSettlementException>(() => Settle(message, action, caller.Token));

		thrown.TransportName.ShouldBe("GooglePubSub");
		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.Retryability.ShouldBe(SettlementRetryability.Unspecified);
		thrown.InnerException.ShouldBeSameAs(native);
		message.ProviderData["pubsub.ack_id"].ShouldBe("ack-id-1");
	}

	[Theory]
	[InlineData("acknowledge")]
	[InlineData("reject")]
	[InlineData("requeue")]
	public async Task PreservePortableSettlementFailureIdentity(string action)
	{
		var sentinel = new TransportSettlementException("already classified")
		{
			TransportName = "GooglePubSub",
			RedeliveryExpectation = TransportRedeliveryExpectation.Expected,
			Retryability = SettlementRetryability.Permanent,
		};
		ArrangeFault(sentinel);

		var thrown = await Should.ThrowAsync<TransportSettlementException>(() => Settle(Message(), action, CancellationToken.None));

		thrown.ShouldBeSameAs(sentinel);
	}

	[Theory]
	[InlineData("acknowledge", false)]
	[InlineData("reject", false)]
	[InlineData("requeue", false)]
	[InlineData("acknowledge", true)]
	[InlineData("reject", true)]
	[InlineData("requeue", true)]
	public async Task RejectInvalidAckIdWithoutNativeIo(string action, bool malformed)
	{
		var message = new TransportReceivedMessage { Id = "invalid-ack" };
		if (malformed) message.ProviderData["pubsub.ack_id"] = 123;

		var thrown = await Should.ThrowAsync<TransportSettlementException>(() => Settle(message, action, CancellationToken.None));

		thrown.TransportName.ShouldBe("GooglePubSub");
		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.Retryability.ShouldBe(SettlementRetryability.Permanent);
		A.CallTo(() => _fakeClient.AcknowledgeAsync(A<string>._, A<IEnumerable<string>>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _fakeClient.ModifyAckDeadlineAsync(A<string>._, A<IEnumerable<string>>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Theory]
	[InlineData("acknowledge")]
	[InlineData("reject")]
	[InlineData("requeue")]
	public async Task CompleteSettlementWithExactNativeArgumentsAndIndependentToken(string action)
	{
		using var caller = new CancellationTokenSource();
		caller.Cancel();
		var observed = CancellationToken.None;
		A.CallTo(() => _fakeClient.AcknowledgeAsync(A<string>._, A<IEnumerable<string>>._, A<CancellationToken>._))
			.Invokes((string _, IEnumerable<string> ids, CancellationToken token) => observed = token)
			.Returns(Task.CompletedTask);
		A.CallTo(() => _fakeClient.ModifyAckDeadlineAsync(A<string>._, A<IEnumerable<string>>._, A<int>._, A<CancellationToken>._))
			.Invokes((string _, IEnumerable<string> ids, int seconds, CancellationToken token) => observed = token)
			.Returns(Task.CompletedTask);

		await Settle(Message(), action, caller.Token);

		observed.ShouldNotBe(caller.Token);
		observed.CanBeCanceled.ShouldBeTrue();
		observed.IsCancellationRequested.ShouldBeFalse();
		if (action == "requeue")
		{
			A.CallTo(() => _fakeClient.ModifyAckDeadlineAsync(TestSource,
				A<IEnumerable<string>>.That.Matches(ids => ids.SequenceEqual(ExpectedAckIds)), 0,
				A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => _fakeClient.AcknowledgeAsync(A<string>._, A<IEnumerable<string>>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
		else
		{
			A.CallTo(() => _fakeClient.AcknowledgeAsync(TestSource,
				A<IEnumerable<string>>.That.Matches(ids => ids.SequenceEqual(ExpectedAckIds)),
				A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => _fakeClient.ModifyAckDeadlineAsync(A<string>._, A<IEnumerable<string>>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
	}

	[Theory]
	[InlineData("acknowledge")]
	[InlineData("reject")]
	public async Task PreserveNullMessageArgumentValidation(string action)
	{
		await Should.ThrowAsync<ArgumentNullException>(() => Settle(null!, action, CancellationToken.None));
	}

	private void ArrangeFault(Exception failure)
	{
		A.CallTo(() => _fakeClient.AcknowledgeAsync(A<string>._, A<IEnumerable<string>>._, A<CancellationToken>._))
			.Returns(Task.FromException(failure));
		A.CallTo(() => _fakeClient.ModifyAckDeadlineAsync(A<string>._, A<IEnumerable<string>>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromException(failure));
	}

	private Task Settle(TransportReceivedMessage message, string action, CancellationToken token) =>
		action == "acknowledge" ? _sut.AcknowledgeAsync(message, token)
			: _sut.RejectAsync(message, "test reason", action == "requeue", token);

	private static TransportReceivedMessage Message() => new()
	{
		Id = "pubsub-msg",
		ProviderData = new Dictionary<string, object> { ["pubsub.ack_id"] = "ack-id-1" },
	};
}
