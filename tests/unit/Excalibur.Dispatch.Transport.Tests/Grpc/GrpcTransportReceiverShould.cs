// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "TRANSPORT")]
public sealed class GrpcTransportReceiverShould : IAsyncDisposable
{
	private readonly GrpcChannel _channel;
	private readonly IOptions<GrpcTransportOptions> _options;

	public GrpcTransportReceiverShould()
	{
		_channel = GrpcChannel.ForAddress("https://localhost:5001");
		_options = Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			Destination = "test-destination",
			DeadlineSeconds = 10,
		});
	}

	[Fact]
	public void ThrowWhenChannelIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportReceiver((global::Grpc.Net.Client.GrpcChannel)null!, _options, NullLogger<GrpcTransportReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenInvokerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportReceiver((global::Grpc.Core.CallInvoker)null!, _options, NullLogger<GrpcTransportReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportReceiver(_channel, null!, NullLogger<GrpcTransportReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportReceiver(_channel, _options, null!));
	}

	[Fact]
	public void ExposeSourceFromOptions()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act & Assert
		receiver.Source.ShouldBe("test-destination");
	}

	[Fact]
	public void ReturnGrpcChannelFromGetService()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act
		var service = receiver.GetService(typeof(GrpcChannel));

		// Assert
		service.ShouldBeSameAs(_channel);
	}

	[Fact]
	public void ReturnNullFromGetServiceForUnknownType()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act
		var service = receiver.GetService(typeof(string));

		// Assert
		service.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenGetServiceTypeIsNull()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => receiver.GetService(null!));
	}

	[Fact]
	public async Task DisposeAsyncIdempotently()
	{
		// Arrange
		var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var receiver = new GrpcTransportReceiver(channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act - dispose twice should not throw
		await receiver.DisposeAsync();
		await receiver.DisposeAsync();
	}

	[Fact]
	public async Task ThrowWhenAcknowledgeMessageIsNull()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			receiver.AcknowledgeAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenRejectMessageIsNull()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			receiver.RejectAsync(null!, "reason", false, CancellationToken.None));
	}


	[Theory]
	[InlineData("acknowledge", false)]
	[InlineData("reject", false)]
	[InlineData("requeue", false)]
	[InlineData("acknowledge", true)]
	[InlineData("reject", true)]
	[InlineData("requeue", true)]
	public async Task WrapRpcDeadlineFailureWithPortableUncertainty(string action, bool cancelCaller)
	{
		using var caller = new CancellationTokenSource();
		if (cancelCaller) caller.Cancel();
		var native = new RpcException(new Status(StatusCode.DeadlineExceeded, "settlement response lost"));
		var invoker = new SettlementInvoker(native);
		await using var receiver = new GrpcTransportReceiver(invoker, _options, NullLogger<GrpcTransportReceiver>.Instance);

		var thrown = await Should.ThrowAsync<TransportSettlementException>(() => Settle(receiver, action, caller.Token));

		thrown.TransportName.ShouldBe("Grpc");
		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.Retryability.ShouldBe(SettlementRetryability.Unspecified);
		thrown.InnerException.ShouldBeSameAs(native);
		invoker.Request.ShouldNotBeNull();
		invoker.Request!.Action.ShouldBe(action);
	}

	[Theory]
	[InlineData("acknowledge", false)]
	[InlineData("reject", false)]
	[InlineData("requeue", false)]
	[InlineData("acknowledge", true)]
	[InlineData("reject", true)]
	[InlineData("requeue", true)]
	public async Task WrapInternalCancellationIndependentlyOfCallerState(string action, bool cancelCaller)
	{
		using var caller = new CancellationTokenSource();
		if (cancelCaller) caller.Cancel();
		var native = new OperationCanceledException("independent settlement timeout");
		var invoker = new SettlementInvoker(native);
		await using var receiver = new GrpcTransportReceiver(invoker, _options, NullLogger<GrpcTransportReceiver>.Instance);

		var thrown = await Should.ThrowAsync<TransportSettlementException>(() => Settle(receiver, action, caller.Token));

		thrown.InnerException.ShouldBeSameAs(native);
		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.Retryability.ShouldBe(SettlementRetryability.Unspecified);
	}

	[Theory]
	[InlineData("acknowledge")]
	[InlineData("reject")]
	[InlineData("requeue")]
	public async Task CompleteAcceptedSettlementUsingIndependentTokenAndDeadline(string action)
	{
		using var caller = new CancellationTokenSource();
		caller.Cancel();
		var invoker = new SettlementInvoker(null);
		var options = Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
		{
			Destination = "test-destination",
			DeadlineSeconds = 30,
		});
		await using var receiver = new GrpcTransportReceiver(invoker, options, NullLogger<GrpcTransportReceiver>.Instance);

		var before = DateTime.UtcNow;
		await Settle(receiver, action, caller.Token);
		var after = DateTime.UtcNow;

		invoker.Options.CancellationToken.ShouldNotBe(caller.Token);
		invoker.Options.CancellationToken.CanBeCanceled.ShouldBeTrue();
		invoker.Options.CancellationToken.IsCancellationRequested.ShouldBeFalse();
		invoker.Options.Deadline.ShouldNotBeNull();
		invoker.Options.Deadline.GetValueOrDefault().ShouldBeInRange(before.AddSeconds(25), after.AddSeconds(35));
		invoker.Request.ShouldNotBeNull();
		invoker.Request!.MessageId.ShouldBe("settlement-msg");
		invoker.Request!.Action.ShouldBe(action);
		if (action != "acknowledge") invoker.Request!.Reason.ShouldBe("test reason");
	}

	[Theory]
	[InlineData("acknowledge")]
	[InlineData("reject")]
	[InlineData("requeue")]
	public async Task PreserveExistingPortableSettlementFailureIdentity(string action)
	{
		var sentinel = new TransportSettlementException("already classified")
		{
			TransportName = "Grpc",
			RedeliveryExpectation = TransportRedeliveryExpectation.Expected,
			Retryability = SettlementRetryability.Permanent,
		};
		var invoker = new SettlementInvoker(sentinel);
		await using var receiver = new GrpcTransportReceiver(invoker, _options, NullLogger<GrpcTransportReceiver>.Instance);

		var thrown = await Should.ThrowAsync<TransportSettlementException>(() => Settle(receiver, action, CancellationToken.None));

		thrown.ShouldBeSameAs(sentinel);
	}

	private static Task Settle(GrpcTransportReceiver receiver, string action, CancellationToken token)
	{
		var message = new TransportReceivedMessage { Id = "settlement-msg" };
		return action == "acknowledge" ? receiver.AcknowledgeAsync(message, token)
			: receiver.RejectAsync(message, "test reason", action == "requeue", token);
	}

	private sealed class SettlementInvoker(Exception? failure) : CallInvoker
	{
		public GrpcAcknowledgeRequest? Request { get; private set; }
		public CallOptions Options { get; private set; }

		public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
		{
			Request = (GrpcAcknowledgeRequest)(object)request;
			Options = options;
			var response = failure is null
				? Task.FromResult((TResponse)(object)new GrpcAcknowledgeResponse { IsSuccess = true })
				: Task.FromException<TResponse>(failure);
			return new AsyncUnaryCall<TResponse>(response, Task.FromResult(new global::Grpc.Core.Metadata()),
				() => Status.DefaultSuccess, () => new global::Grpc.Core.Metadata(), () => { });
		}

		public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
			string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
		public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
		public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
		public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
			Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
	}

	public async ValueTask DisposeAsync()
	{
		_channel.Dispose();
		await ValueTask.CompletedTask;
	}
}
