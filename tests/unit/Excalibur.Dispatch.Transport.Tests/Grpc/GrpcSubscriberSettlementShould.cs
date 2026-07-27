// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Core;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Author≠impl data-loss regression lock for o3yfyw — <see cref="GrpcTransportSubscriber"/> must actually
/// SEND the settlement decision back over the Acknowledge RPC for every terminal action, not merely log it.
/// The pre-fix subscriber only <em>logged</em> Reject/Requeue/Acknowledge and never issued the settlement
/// RPC, so a Reject/Requeue was silently dropped — the message was neither redelivered nor dead-lettered
/// (data loss), violating the settlement contract every other transport honors.
/// </summary>
/// <remarks>
/// Uses the <c>internal</c> <see cref="CallInvoker"/> injection seam (a fake invoker) so the subscribe
/// stream + settlement RPC are observed without a live server. The fake yields exactly one message, the
/// handler returns the action under test, and the fake captures the Acknowledge unary request.
/// <para>
/// <b>RED-on-mutant:</b> the pre-fix log-only impl never calls <c>AsyncUnaryCall</c> ⇒
/// <see cref="CapturedSettlement"/> stays null ⇒ every case goes RED. GREEN on the fix (a settlement RPC
/// with the matching <c>Action</c> is sent).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Api")]
public sealed class GrpcSubscriberSettlementShould
{
	[Theory]
	[InlineData(MessageAction.Reject, "reject")]
	[InlineData(MessageAction.Requeue, "requeue")]
	[InlineData(MessageAction.Acknowledge, "acknowledge")]
	public async Task SendTheSettlementRpc_WithTheMatchingAction(MessageAction action, string expectedWireAction)
	{
		var message = new GrpcReceivedMessage { Id = "msg-1", Body = Convert.ToBase64String([1, 2, 3]) };
		var invoker = new CapturingCallInvoker(message);
		var options = Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
		{
			Destination = "svc",
			SubscribeMethodPath = "/svc/Subscribe",
		});

		await using var subscriber = new GrpcTransportSubscriber(invoker, options, NullLogger<GrpcTransportSubscriber>.Instance);

		await subscriber.SubscribeAsync((_, _) => Task.FromResult(action), CancellationToken.None);

		invoker.CapturedSettlement.ShouldNotBeNull(
			$"a {action} handler result MUST send the Acknowledge RPC — the pre-fix log-only impl dropped it (message lost, never redelivered/dead-lettered)");
		invoker.CapturedSettlement!.Action.ShouldBe(expectedWireAction,
			"the settlement RPC must carry the action the handler returned");
		invoker.CapturedSettlement.MessageId.ShouldBe("msg-1",
			"the settlement must reference the message being settled");
	}

	/// <summary>
	/// A fake <see cref="CallInvoker"/> that streams exactly one message for Subscribe and captures the
	/// Acknowledge (settlement) unary request so the test can assert it was actually sent.
	/// </summary>
	private sealed class CapturingCallInvoker(GrpcReceivedMessage message) : CallInvoker
	{
		public GrpcAcknowledgeRequest? CapturedSettlement { get; private set; }

		public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
		{
			var reader = new SingleItemStreamReader<TResponse>((TResponse)(object)message);
			return new AsyncServerStreamingCall<TResponse>(
				reader,
				Task.FromResult(new global::Grpc.Core.Metadata()),
				() => Status.DefaultSuccess,
				() => new global::Grpc.Core.Metadata(),
				() => { });
		}

		public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
		{
			if (request is GrpcAcknowledgeRequest ack)
			{
				CapturedSettlement = ack;
			}

			var response = (TResponse)(object)new GrpcAcknowledgeResponse { IsSuccess = true };
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
}
