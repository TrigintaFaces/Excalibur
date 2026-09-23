// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Unit tests for <see cref="GrpcTransportAdapter"/>.
/// Sprint 697 T.33: gRPC transport test coverage.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class GrpcTransportAdapterShould : IAsyncDisposable
{
	private readonly GrpcChannel _channel;
	private readonly ITransportSender _fakeSender;
	private readonly GrpcTransportAdapter _sut;

	public GrpcTransportAdapterShould()
	{
		_channel = GrpcChannel.ForAddress("https://localhost:5001");
		_fakeSender = A.Fake<ITransportSender>();
		_sut = new GrpcTransportAdapter(
			_channel,
			_fakeSender,
			NullLogger<GrpcTransportAdapter>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync().ConfigureAwait(false);
		await _fakeSender.DisposeAsync().ConfigureAwait(false);
		_channel.Dispose();
	}

	#region Constructor Validation

	[Fact]
	public void ThrowWhenChannelIsNull()
	{
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportAdapter(null!, _fakeSender, NullLogger<GrpcTransportAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenSenderIsNull()
	{
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportAdapter(channel, null!, NullLogger<GrpcTransportAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var sender = A.Fake<ITransportSender>();
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportAdapter(channel, sender, null!));
	}

	#endregion

	#region Name and TransportType

	[Fact]
	public void HaveName_Grpc()
	{
		_sut.Name.ShouldBe("grpc");
	}

	[Fact]
	public void HaveTransportType_Grpc()
	{
		((ITransportAdapter)_sut).TransportType.ShouldBe("grpc");
	}

	#endregion

	#region Start / Stop Lifecycle

	[Fact]
	public async Task StartAsync_SetsIsRunningToTrue()
	{
		// Act
		await _sut.StartAsync(CancellationToken.None);

		// Assert
		_sut.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public async Task StopAsync_SetsIsRunningToFalse()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Act
		await _sut.StopAsync(CancellationToken.None);

		// Assert
		_sut.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void IsRunning_DefaultsToFalse()
	{
		_sut.IsRunning.ShouldBeFalse();
	}

	#endregion

	#region Health Check

	[Fact]
	public async Task CheckHealthAsync_ReturnHealthyWhenRunning()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity);

		// Act
		var result = await _sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Healthy);
		result.Description.ShouldContain("localhost:5001");
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnDegradedWhenStopped()
	{
		// Arrange
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity);

		// Act
		var result = await _sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Degraded);
	}

	[Fact]
	public async Task CheckQuickHealthAsync_ReturnHealthyWhenRunning()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Act
		var result = await _sut.CheckQuickHealthAsync(CancellationToken.None);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Healthy);
	}

	[Fact]
	public async Task CheckQuickHealthAsync_ReturnUnhealthyWhenStopped()
	{
		// Act
		var result = await _sut.CheckQuickHealthAsync(CancellationToken.None);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Unhealthy);
	}

	[Fact]
	public async Task Categories_IncludeConnectivityAndPerformance()
	{
		// Assert
		var checker = (ITransportHealthChecker)_sut;
		checker.Categories.ShouldBe(
			TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Performance);
	}

	#endregion

	#region Health Metrics

	[Fact]
	public async Task GetHealthMetricsAsync_ReturnDefaultMetrics()
	{
		// Act
		var metrics = await _sut.GetHealthMetricsAsync(CancellationToken.None);

		// Assert
		metrics.TotalChecks.ShouldBe(0);
		metrics.SuccessRate.ShouldBe(1.0);
		metrics.ConsecutiveFailures.ShouldBe(0);
	}

	#endregion

	#region ReceiveAsync Validation

	[Fact]
	public async Task ReceiveAsync_ThrowOnNullTransportMessage()
	{
		// Arrange
		var dispatcher = A.Fake<IDispatcher>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ReceiveAsync(null!, dispatcher, CancellationToken.None));
	}

	[Fact]
	public async Task ReceiveAsync_ThrowOnNullDispatcher()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ReceiveAsync(new object(), null!, CancellationToken.None));
	}

	[Fact]
	public async Task ReceiveAsync_ThrowOnNonDispatchMessage()
	{
		// Arrange
		var dispatcher = A.Fake<IDispatcher>();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.ReceiveAsync("not-a-message", dispatcher, CancellationToken.None));
	}

	#endregion

	#region SendAsync Validation

	[Fact]
	public async Task SendAsync_ThrowOnNullMessage()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.SendAsync(null!, "dest", context, CancellationToken.None));
	}

	[Fact]
	public async Task SendAsync_ThrowOnNullDestination()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.SendAsync(message, null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task SendAsync_ThrowOnNullContext()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.SendAsync(message, "dest", null!, CancellationToken.None));
	}

	/// <summary>
	/// SAFETY. A send the transport REJECTED must not complete normally.
	/// </summary>
	/// <remarks>
	/// This adapter returns <see cref="Task"/>, so it has no result channel, and its caller reads normal
	/// completion as delivery: <c>MessageBusOutboxPublisher</c> awaits it and then calls
	/// <c>MarkTransportSentAsync</c> unconditionally, inspecting nothing, with <c>catch (Exception)</c> as
	/// its only failure path. <see cref="GrpcTransportSender"/> never throws for a rejection - it returns
	/// <c>SendResult.Failure</c> both for a negative remote response and for an <c>RpcException</c>. So an
	/// adapter that discards the result records an undelivered message as Sent and drops it from retry
	/// selection. Asserting "it threw" is asserting the only signal this contract has.
	/// </remarks>
	[Fact]
	public async Task SendAsync_ThrowWhenTheSenderRejectsTheMessage()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();

		_ = A.CallTo(() => _fakeSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(Task.FromResult(SendResult.Failure(new SendError
			{
				Code = "UNAVAILABLE",
				Message = "the remote endpoint rejected the delivery",
				IsRetryable = true,
			})));

		// Act & Assert
		var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.SendAsync(new AdapterProbeMessage(), "dest", context, CancellationToken.None));

		// The outbox records ex.Message, so the remote's own code and detail have to survive into it -
		// an exception that says only "send failed" leaves an operator with nothing to act on.
		thrown.Message.ShouldContain("UNAVAILABLE");
		thrown.Message.ShouldContain("the remote endpoint rejected the delivery");
	}

	/// <summary>
	/// PRECISION, and the partner the safety arm needs. An ACCEPTED send must still complete normally.
	/// </summary>
	/// <remarks>
	/// Without this, the arm above is satisfied by an adapter that throws on every send, which would fail
	/// every delivery rather than fix the misreported one. The pair discriminates rejected from accepted;
	/// neither arm alone does.
	/// </remarks>
	[Fact]
	public async Task SendAsync_CompleteNormallyWhenTheSenderAcceptsTheMessage()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();

		_ = A.CallTo(() => _fakeSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(Task.FromResult(SendResult.Success("accepted-1")));

		// Act & Assert
		await Should.NotThrowAsync(() =>
			_sut.SendAsync(new AdapterProbeMessage(), "dest", context, CancellationToken.None));
	}

	/// <summary>
	/// A concrete message rather than a fake: the adapter serializes what it is handed with
	/// <c>JsonSerializer.SerializeToUtf8Bytes</c>, and a dynamic proxy is not a faithful subject for that.
	/// </summary>
	private sealed record AdapterProbeMessage : IDispatchMessage;

	#endregion

	#region DisposeAsync

	[Fact]
	public async Task DisposeAsync_SetsIsRunningToFalse()
	{
		// Arrange
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var sender = A.Fake<ITransportSender>();
		var adapter = new GrpcTransportAdapter(
			channel, sender, NullLogger<GrpcTransportAdapter>.Instance);

		await adapter.StartAsync(CancellationToken.None);
		adapter.IsRunning.ShouldBeTrue();

		// Act
		await adapter.DisposeAsync();

		// Assert
		adapter.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var sender = A.Fake<ITransportSender>();
		var adapter = new GrpcTransportAdapter(
			channel, sender, NullLogger<GrpcTransportAdapter>.Instance);

		// Act -- double dispose should not throw
		await adapter.DisposeAsync();
		await adapter.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_DisposesAsyncDisposableSender()
	{
		// Arrange
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var sender = A.Fake<ITransportSender>(o =>
			o.Implements<IAsyncDisposable>());
		var adapter = new GrpcTransportAdapter(
			channel, sender, NullLogger<GrpcTransportAdapter>.Instance);

		// Act
		await adapter.DisposeAsync();

		// Assert
		A.CallTo(() => ((IAsyncDisposable)sender).DisposeAsync())
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	/// <summary>
	/// SAFETY. The body carries the concrete payload, not an empty object.
	/// </summary>
	/// <remarks>
	/// The adapter receives its argument as <see cref="IDispatchMessage"/>, which declares no members.
	/// Serializing by the DECLARED type therefore wrote <c>{}</c> for every message while the
	/// <c>MessageType</c> header still named the concrete type, so a receiver deserialized a
	/// default-valued instance or rejected it for missing members -- with nothing on either side
	/// reporting a fault. This asserts the emitted BYTES, because those bytes are what the far side parses.
	/// </remarks>
	[Fact]
	public async Task SendAsync_SerializeTheConcretePayloadRatherThanTheMarkerInterface()
	{
		TransportMessage? sent = null;
		_ = A.CallTo(() => _fakeSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage m, CancellationToken _) => sent = m)
			.Returns(Task.FromResult(SendResult.Success("accepted-1")));

		var context = A.Fake<IMessageContext>();

		await _sut.SendAsync(
			new PayloadBearingMessage { Amount = 42, Label = "hunt-order" },
			"destination",
			context,
			CancellationToken.None);

		sent.ShouldNotBeNull("nothing was sent, so this arm would assert nothing about the body.");

		var body = System.Text.Json.JsonDocument.Parse(sent!.Body);

		body.RootElement.TryGetProperty(nameof(PayloadBearingMessage.Amount), out var amount)
			.ShouldBeTrue(
				$"the body carried no Amount. Serializing the declared interface emits an empty object, so "
				+ $"the payload never reaches the far side. Body was: {System.Text.Encoding.UTF8.GetString(sent.Body.Span)}");

		amount.GetInt32().ShouldBe(42);

		body.RootElement.GetProperty(nameof(PayloadBearingMessage.Label)).GetString().ShouldBe("hunt-order");
	}

	/// <summary>
	/// PRECISION. The type header still names the concrete message.
	/// </summary>
	/// <remarks>
	/// The header was already correct before the body was; this pins the pair together, since a body and a
	/// header that disagree about the message are what made the original defect silent.
	/// </remarks>
	[Fact]
	public async Task SendAsync_NameTheConcreteTypeAlongsideTheConcreteBody()
	{
		TransportMessage? sent = null;
		_ = A.CallTo(() => _fakeSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage m, CancellationToken _) => sent = m)
			.Returns(Task.FromResult(SendResult.Success("accepted-1")));

		await _sut.SendAsync(
			new PayloadBearingMessage { Amount = 7, Label = "x" },
			"destination",
			A.Fake<IMessageContext>(),
			CancellationToken.None);

		sent.ShouldNotBeNull();
		sent!.MessageType.ShouldNotBeNullOrWhiteSpace(
			"a body without a type header is as unusable to the receiver as a type header without a body.");
	}

	/// <summary>
	/// SAFETY. The caller's explicit destination survives the conversion to a transport message.
	/// </summary>
	/// <remarks>
	/// The sender builds the outbound request's destination from this property and from nowhere else --
	/// the configured sender destination is not a fallback there. Placing the argument only in Subject
	/// dropped the caller's routing value silently: both halves compiled, both were self-consistent, and
	/// the wire simply carried no destination.
	/// </remarks>
	[Fact]
	public async Task SendAsync_CarryTheDestinationWhereTheSenderReadsIt()
	{
		TransportMessage? sent = null;
		_ = A.CallTo(() => _fakeSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage m, CancellationToken _) => sent = m)
			.Returns(Task.FromResult(SendResult.Success("accepted-1")));

		await _sut.SendAsync(
			new PayloadBearingMessage { Amount = 1, Label = "x" },
			"orders",
			A.Fake<IMessageContext>(),
			CancellationToken.None);

		sent.ShouldNotBeNull();

		sent!.Properties.TryGetValue("dispatch.destination", out var routed)
			.ShouldBeTrue(
				"the sender maps the outbound destination from this property alone; without it the caller's "
				+ "explicit routing value never reaches the wire.");

		routed.ShouldBe("orders");

		sent.Subject.ShouldBe(
			"orders",
			"Subject is what the far-side subscriber matches on and must keep carrying the destination too.");
	}

	/// <summary>A message with real properties, so an empty body is detectable.</summary>
	private sealed class PayloadBearingMessage : IDispatchMessage
	{
		public int Amount { get; init; }

		public string Label { get; init; } = string.Empty;
	}
}