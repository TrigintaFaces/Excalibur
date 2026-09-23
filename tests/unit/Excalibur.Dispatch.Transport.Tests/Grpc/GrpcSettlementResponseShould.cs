// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Locks the distinction between a successful settlement RPC and a successful settlement, on both the pull
/// <see cref="GrpcTransportReceiver"/> and the push <see cref="GrpcTransportSubscriber"/>.
/// </summary>
/// <remarks>
/// The settlement RPC answers with <c>GrpcAcknowledgeResponse.IsSuccess</c>. Both surfaces awaited that
/// call and discarded its response, so a server that completed the RPC while REFUSING the settlement was
/// indistinguishable from one that performed it: the receiver returned normally and the subscriber logged
/// the message acknowledged / rejected / requeued. The message was in fact still held by the server,
/// undelivered, with nothing having observed the failure.
/// <para>
/// <b>Non-vacuous.</b> Every case is paired: the SAFETY arm drives <c>IsSuccess=false</c> and requires the
/// refusal to surface (receiver throws; subscriber logs the refusal and NOT a terminal disposition), and
/// the LIVENESS arm drives <c>IsSuccess=true</c> through the identical path and requires it to still
/// succeed. An implementation that failed every settlement would pass the safety arms and go RED on the
/// liveness arms; the pre-fix discard passes the liveness arms and goes RED on every safety arm.
/// </para>
/// <para>
/// Uses the <c>internal</c> <see cref="CallInvoker"/> injection seam on both types so the settlement RPC
/// and the server's answer to it are driven without a live gRPC server.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcSettlementResponseShould
{
	// ---- Pull surface: GrpcTransportReceiver. The caller is reachable, so a refusal must throw. ----

	[Theory]
	[InlineData("acknowledge")]
	[InlineData("reject")]
	[InlineData("requeue")]
	public async Task Receiver_ThrowOnRefusedSettlement(string action)
	{
		var invoker = new SettlementCallInvoker(settlementAccepted: false);
		await using var receiver = new GrpcTransportReceiver(invoker, ReceiverOptions(), NullLogger<GrpcTransportReceiver>.Instance);
		var message = new TransportReceivedMessage { Id = "msg-1" };

		// Bound to the contract type, not its base. InvalidOperationException would also pass here, and
		// that is exactly the gap: handler code frequently sits in the same try block, so a caller
		// catching the base cannot tell a refused settlement from an unrelated invalid operation.
		var thrown = await Should.ThrowAsync<TransportSettlementException>(
			() => Settle(receiver, message, action),
			$"a server that refuses the {action} has NOT settled the message; completing normally reports it settled when it is not");

		// Source compatibility: a consumer already catching the base type keeps working.
		thrown.ShouldBeAssignableTo<InvalidOperationException>();

		// The discriminator the caller acts on. The server still owns the message, so it is coming back.
		thrown.RedeliveryExpectation.ShouldBe(
			TransportRedeliveryExpectation.Expected,
			"the server refused the settlement and still holds the message, so the handler's work may run again");
		thrown.TransportName.ShouldBe("Grpc");

		// The server refused without saying why, so the transport cannot tell a transient refusal from a
		// permanent one. Saying Unspecified is honest; guessing Retryable would loop a retrying caller.
		thrown.Retryability.ShouldBe(SettlementRetryability.Unspecified);

		thrown.Message.Contains("msg-1", StringComparison.Ordinal)
			.ShouldBeTrue("an operator must be told which message is unsettled");
		thrown.Message.Contains("not accepted", StringComparison.Ordinal)
			.ShouldBeTrue("the failure must name the settlement refusal, not a transport fault");
		invoker.CapturedSettlement.ShouldNotBeNull("the settlement RPC must still be sent before its answer is read");
		invoker.CapturedSettlement!.Action.ShouldBe(action, "the refusal must be reported for the action that was actually requested");
	}

	[Theory]
	[InlineData("acknowledge")]
	[InlineData("reject")]
	[InlineData("requeue")]
	public async Task Receiver_CompleteOnAcceptedSettlement(string action)
	{
		var invoker = new SettlementCallInvoker(settlementAccepted: true);
		await using var receiver = new GrpcTransportReceiver(invoker, ReceiverOptions(), NullLogger<GrpcTransportReceiver>.Instance);
		var message = new TransportReceivedMessage { Id = "msg-1" };

		await Settle(receiver, message, action);

		invoker.CapturedSettlement.ShouldNotBeNull("an accepted settlement must still issue the RPC");
		invoker.CapturedSettlement!.Action.ShouldBe(action, "the accepted path must carry the requested action unchanged");
	}

	// ---- Push surface: GrpcTransportSubscriber. No caller to throw to, so a refusal must be reported. ----

	[Theory]
	[InlineData(MessageAction.Acknowledge, "acknowledge", GrpcTransportEventId.SubscriberMessageAcknowledged)]
	[InlineData(MessageAction.Reject, "reject", GrpcTransportEventId.SubscriberMessageRejected)]
	[InlineData(MessageAction.Requeue, "requeue", GrpcTransportEventId.SubscriberMessageRequeued)]
	public async Task Subscriber_ReportRefusedSettlement_AndNotLogTheDisposition(
		MessageAction action, string expectedWireAction, int dispositionEventId)
	{
		var logger = new RecordingLogger<GrpcTransportSubscriber>();
		var invoker = new SettlementCallInvoker(settlementAccepted: false, streamedMessage: StreamedMessage());
		await using var subscriber = new GrpcTransportSubscriber(invoker, SubscriberOptions(), logger);

		await subscriber.SubscribeAsync((_, _) => Task.FromResult(action), CancellationToken.None);

		logger.EventIds.ShouldContain(
			GrpcTransportEventId.SubscriberSettlementRejected,
			$"a refused {expectedWireAction} must be observable; the pre-fix loop discarded the response and reported success");
		logger.EventIds.ShouldNotContain(
			dispositionEventId,
			$"the loop must NOT log the message {expectedWireAction}d when the server refused to settle it");
		invoker.CapturedSettlement!.Action.ShouldBe(expectedWireAction, "the settlement RPC must still carry the handler's action");
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge, GrpcTransportEventId.SubscriberMessageAcknowledged)]
	[InlineData(MessageAction.Reject, GrpcTransportEventId.SubscriberMessageRejected)]
	[InlineData(MessageAction.Requeue, GrpcTransportEventId.SubscriberMessageRequeued)]
	public async Task Subscriber_LogTheDisposition_OnAcceptedSettlement(MessageAction action, int dispositionEventId)
	{
		var logger = new RecordingLogger<GrpcTransportSubscriber>();
		var invoker = new SettlementCallInvoker(settlementAccepted: true, streamedMessage: StreamedMessage());
		await using var subscriber = new GrpcTransportSubscriber(invoker, SubscriberOptions(), logger);

		await subscriber.SubscribeAsync((_, _) => Task.FromResult(action), CancellationToken.None);

		logger.EventIds.ShouldContain(
			dispositionEventId,
			"an accepted settlement must still report the terminal disposition — honoring refusals must not silence success");
		logger.EventIds.ShouldNotContain(
			GrpcTransportEventId.SubscriberSettlementRejected,
			"an accepted settlement must never be reported as refused");
	}

	/// <summary>
	/// A refusal must not tear down the subscription: the business handler already ran, so the loop has to
	/// keep draining the stream rather than abandon it or re-run the work.
	/// </summary>
	[Fact]
	public async Task Subscriber_ContinueTheStream_AfterARefusedSettlement()
	{
		var logger = new RecordingLogger<GrpcTransportSubscriber>();
		var invoker = new SettlementCallInvoker(settlementAccepted: false, streamedMessage: StreamedMessage());
		await using var subscriber = new GrpcTransportSubscriber(invoker, SubscriberOptions(), logger);
		var handlerInvocations = 0;

		await subscriber.SubscribeAsync(
			(_, _) =>
			{
				handlerInvocations++;
				return Task.FromResult(MessageAction.Acknowledge);
			},
			CancellationToken.None);

		handlerInvocations.ShouldBe(1, "a refused settlement must NOT blindly re-execute the business handler");
		logger.EventIds.ShouldContain(GrpcTransportEventId.SubscriberStreamEnded, "the subscription must drain the stream, not abort on a refusal");
	}

	private static Task Settle(GrpcTransportReceiver receiver, TransportReceivedMessage message, string action) =>
		action switch
		{
			"acknowledge" => receiver.AcknowledgeAsync(message, CancellationToken.None),
			"reject" => receiver.RejectAsync(message, "because", requeue: false, CancellationToken.None),
			"requeue" => receiver.RejectAsync(message, "because", requeue: true, CancellationToken.None),
			_ => throw new ArgumentOutOfRangeException(nameof(action)),
		};

	private static GrpcReceivedMessage StreamedMessage() =>
		new() { Id = "msg-1", Body = Convert.ToBase64String([1, 2, 3]), Source = "svc" };

	private static IOptions<GrpcTransportOptions> ReceiverOptions() =>
		Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
		{
			Destination = "svc",
			ReceiveMethodPath = "/svc/Receive",
			DeadlineSeconds = 10,
		});

	private static IOptions<GrpcTransportOptions> SubscriberOptions() =>
		Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
		{
			Destination = "svc",
			SubscribeMethodPath = "/svc/Subscribe",
		});

	/// <summary>
	/// A fake <see cref="CallInvoker"/> whose settlement RPC completes successfully at the TRANSPORT level
	/// while answering with the <c>IsSuccess</c> under test — the exact shape the defect could not tell
	/// apart. Streams at most one message for Subscribe.
	/// </summary>
	private sealed class SettlementCallInvoker(bool settlementAccepted, GrpcReceivedMessage? streamedMessage = null) : CallInvoker
	{
		public GrpcAcknowledgeRequest? CapturedSettlement { get; private set; }

		public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
			new(
				new SingleItemStreamReader<TResponse>((TResponse)(object)streamedMessage!),
				Task.FromResult(new global::Grpc.Core.Metadata()),
				() => Status.DefaultSuccess,
				() => new global::Grpc.Core.Metadata(),
				() => { });

		public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
		{
			if (request is GrpcAcknowledgeRequest ack)
			{
				CapturedSettlement = ack;
			}

			var response = (TResponse)(object)new GrpcAcknowledgeResponse { IsSuccess = settlementAccepted };
			return new AsyncUnaryCall<TResponse>(
				Task.FromResult(response),
				Task.FromResult(new global::Grpc.Core.Metadata()),
				() => Status.DefaultSuccess,
				() => new global::Grpc.Core.Metadata(),
				() => { });
		}

		public override TResponse BlockingUnaryCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
			throw new NotSupportedException();

		public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options) =>
			throw new NotSupportedException();

		public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options) =>
			throw new NotSupportedException();
	}

	/// <summary>An <see cref="IAsyncStreamReader{T}"/> that yields exactly one item, then completes.</summary>
	private sealed class SingleItemStreamReader<T>(T item) : IAsyncStreamReader<T>
	{
		private bool _read;

		public T Current { get; private set; } = default!;

		public Task<bool> MoveNext(CancellationToken cancellationToken)
		{
			if (_read)
			{
				return Task.FromResult(false);
			}

			_read = true;
			Current = item;
			return Task.FromResult(true);
		}
	}

	/// <summary>
	/// Records the event ids written by the subject. The subscriber's push loop has no caller to return a
	/// settlement outcome to, so its diagnostic events ARE the observable behavior under test.
	/// </summary>
	private sealed class RecordingLogger<T> : ILogger<T>
	{
		private readonly List<int> _eventIds = [];

		public IReadOnlyList<int> EventIds => _eventIds;

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			_eventIds.Add(eventId.Id);
	}
}
