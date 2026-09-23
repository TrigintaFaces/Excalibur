// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Core;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Binds <see cref="GrpcTransportSender"/>'s batch result to the batch it was asked to send.
/// </summary>
/// <remarks>
/// <para>
/// <c>SendBatchAsync</c> mapped <c>response.Results</c> — a remote server's list, of whatever length and
/// order that server chose — while computing the counts from the CALLER's list, with nothing binding the
/// two. The result was a <see cref="BatchSendResult"/> whose entries did not correspond to the inputs and
/// whose counts were arithmetic across two different bases, and it looked entirely well-formed.
/// </para>
/// <para>
/// Every arm drives the real <see cref="GrpcTransportSender"/> through a fake <see cref="CallInvoker"/>.
/// The correlation rule is NOT reimplemented here: the assertions read only the inputs the test supplied
/// and the <see cref="BatchSendResult"/> the production code returned, so reverting the fix reddens them.
/// </para>
/// <para>
/// <b>Non-vacuous.</b> The three SAFETY arms go RED against removing the length check, against stamping
/// the server's id, and against counts drawn from more than one base. The LIVENESS arm goes RED against
/// the cheapest way to pass all three — a sender that refuses every response and reports the whole batch
/// failed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcBatchResultCorrelationShould
{
	// ---- SAFETY ----

	[Fact]
	public async Task RefuseAResponseWhoseLengthDoesNotMatchTheBatch()
	{
		// One result for two messages: nothing can be attributed, and the server may have accepted one.
		var sender = CreateSender(new GrpcBatchResponse
		{
			Results = [Ok("server-generated-id")],
		});
		var messages = Batch("mine-1", "mine-2");

		var result = await sender.SendBatchAsync(messages, CancellationToken.None);

		result.TotalMessages.ShouldBe(2);
		result.SuccessCount.ShouldBe(0, "no input can be reported sent when no result can be attributed to one");
		result.FailureCount.ShouldBe(2);
		result.Results.Count.ShouldBe(2, "every INPUT gets an entry — the server's shorter list must not set the length");
		result.Results.Select(r => r.MessageId).ShouldBe(["mine-1", "mine-2"],
			"each entry must name the input it is about, not the id the server invented");
		result.Results.ShouldAllBe(r => r.Error!.IsRetryable,
			"the batch may have been partially accepted, so the caller must be allowed to retry");
	}

	[Fact]
	public async Task StampTheInputsOwnIdNotTheServers()
	{
		// The server answers with its own ids for the first and NONE for the second — both are things a
		// real server does, and the pre-fix code substituted string.Empty for the second.
		var sender = CreateSender(new GrpcBatchResponse
		{
			Results = [Ok("server-id-A"), Ok(null)],
		});
		var messages = Batch("mine-1", "mine-2");

		var result = await sender.SendBatchAsync(messages, CancellationToken.None);

		result.Results.Select(r => r.MessageId).ShouldBe(["mine-1", "mine-2"],
			"a caller needs to know which of ITS OWN messages an entry describes; the server's id can be absent entirely");
	}

	[Fact]
	public async Task DrawEveryCountFromTheSameList()
	{
		var sender = CreateSender(new GrpcBatchResponse
		{
			Results = [Ok("ignored"), Failed("BROKER_FULL", "queue is full")],
		});
		var messages = Batch("mine-1", "mine-2");

		var result = await sender.SendBatchAsync(messages, CancellationToken.None);

		result.Results.Count.ShouldBe(result.TotalMessages, "TotalMessages must describe the list that was returned");
		result.SuccessCount.ShouldBe(result.Results.Count(r => r.IsSuccess));
		result.FailureCount.ShouldBe(result.Results.Count(r => !r.IsSuccess),
			"FailureCount must be counted from the results, never arithmetic over the input count");
		result.Results[1].MessageId.ShouldBe("mine-2", "the failing entry must still name its own input");
		result.Results[1].Error!.Code.ShouldBe("BROKER_FULL", "the server's reason for THAT entry must survive");
	}

	// ---- LIVENESS ----

	[Fact]
	public async Task StillReportAWellFormedBatchAsSent()
	{
		var sender = CreateSender(new GrpcBatchResponse
		{
			Results = [Ok("server-id-A"), Ok("server-id-B")],
		});
		var messages = Batch("mine-1", "mine-2");

		var result = await sender.SendBatchAsync(messages, CancellationToken.None);

		result.SuccessCount.ShouldBe(2,
			"a matching, all-successful response must be reported sent — refusing everything satisfies every safety arm above");
		result.FailureCount.ShouldBe(0);
		result.Results.ShouldAllBe(r => r.IsSuccess);
		result.Results.Select(r => r.MessageId).ShouldBe(["mine-1", "mine-2"]);
	}

	private static GrpcTransportSender CreateSender(GrpcBatchResponse response) =>
		new(
			new BatchCallInvoker(response),
			Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
			{
				Destination = "svc",
				SendBatchMethodPath = "/svc/SendBatch",
				DeadlineSeconds = 10,
			}),
			NullLogger<GrpcTransportSender>.Instance);

	private static List<TransportMessage> Batch(params string[] ids) =>
		[.. ids.Select(id => new TransportMessage { Id = id, Body = new byte[] { 1 } })];

	private static GrpcTransportResponse Ok(string? serverId) =>
		new() { IsSuccess = true, MessageId = serverId };

	private static GrpcTransportResponse Failed(string code, string message) =>
		new() { IsSuccess = false, ErrorCode = code, ErrorMessage = message };

	/// <summary>A fake <see cref="CallInvoker"/> that answers the batch RPC with the supplied response.</summary>
	private sealed class BatchCallInvoker(GrpcBatchResponse response) : CallInvoker
	{
		public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
			new(
				Task.FromResult((TResponse)(object)response),
				Task.FromResult(new global::Grpc.Core.Metadata()),
				() => Status.DefaultSuccess,
				() => new global::Grpc.Core.Metadata(),
				() => { });

		public override TResponse BlockingUnaryCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
			throw new NotSupportedException();

		public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
			throw new NotSupportedException();

		public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options) =>
			throw new NotSupportedException();

		public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options) =>
			throw new NotSupportedException();
	}
}
