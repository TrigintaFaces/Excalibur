// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// gRPC implementation of <see cref="ITransportReceiver"/>.
/// Uses gRPC unary calls for pull-based message consumption.
/// </summary>
internal sealed partial class GrpcTransportReceiver : ITransportReceiver
{
	private readonly GrpcChannel? _channel;
	private readonly CallInvoker _invoker;
	private readonly GrpcTransportOptions _options;
	private readonly int? _maxPayloadBytes;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="GrpcTransportReceiver"/> class.
	/// </summary>
	/// <param name="channel">The gRPC channel.</param>
	/// <param name="options">The transport options.</param>
	/// <param name="logger">The logger instance.</param>
	public GrpcTransportReceiver(
		GrpcChannel channel,
		IOptions<GrpcTransportOptions> options,
		ILogger<GrpcTransportReceiver> logger)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_maxPayloadBytes = _options.MaxPayloadBytes;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_invoker = _channel.CreateCallInvoker();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="GrpcTransportReceiver"/> class with an explicit
	/// <see cref="CallInvoker"/> (the gRPC injection seam) instead of a channel. Used to substitute a fake
	/// invoker under test so receive and settlement RPCs — including the server's acknowledge response —
	/// can be observed without a live server. There is no owned channel on this path, so
	/// <see cref="GetService(Type)"/> returns <see langword="null"/> for <see cref="GrpcChannel"/> and
	/// disposal has no channel to release.
	/// </summary>
	/// <param name="invoker">The gRPC call invoker that issues receive and settlement RPCs.</param>
	/// <param name="options">The transport options.</param>
	/// <param name="logger">The logger instance.</param>
	internal GrpcTransportReceiver(
		CallInvoker invoker,
		IOptions<GrpcTransportOptions> options,
		ILogger<GrpcTransportReceiver> logger)
	{
		_invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_maxPayloadBytes = _options.MaxPayloadBytes;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_channel = null;
	}

	/// <inheritdoc />
	public string Source => _options.Destination;

	/// <inheritdoc />
	public async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		try
		{
			var request = new GrpcReceiveRequest
			{
				Source = Source,
				MaxMessages = maxMessages,
			};

			var method = GrpcMethodDescriptors.CreateReceiveMethod(_options.ReceiveMethodPath);
			var callOptions = CreateCallOptions(cancellationToken);

			var response = await _invoker.AsyncUnaryCall(method, null, callOptions, request)
				.ConfigureAwait(false);

			var result = new List<TransportReceivedMessage>(response.Messages.Count);
			foreach (var grpcMessage in response.Messages)
			{
				try
				{
					result.Add(MapToReceivedMessage(grpcMessage));
				}
				catch (PayloadTooLargeException ex)
				{
					// Poison-message guard: an oversized payload can never be processed. This pull-based
					// unary receive has no per-message nack (ack/reject are surfaced to the caller by id),
					// so the offending message is dropped BEFORE its body is materialized — never
					// truncated, never surfaced — mirroring the transport's no-requeue rejection semantics.
					LogPayloadTooLargeRejected(Source, ex.ActualBytes, ex);
				}
			}

			LogMessagesReceived(Source, result.Count);
			return result;
		}
		catch (RpcException ex)
		{
			LogReceiveFailed(Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		try
		{
			var request = new GrpcAcknowledgeRequest
			{
				MessageId = message.Id,
				Action = "acknowledge",
			};

			var method = GrpcMethodDescriptors.CreateAcknowledgeMethod(
				_options.ReceiveMethodPath.Replace("Receive", "Acknowledge", StringComparison.Ordinal));
			// Ack must complete even during shutdown to prevent redelivery;
			// use dedicated timeout instead of caller's cancellation token
			using var ackCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			var callOptions = CreateCallOptions(ackCts.Token);

			var response = await _invoker.AsyncUnaryCall(method, null, callOptions, request)
				.ConfigureAwait(false);

			if (!GrpcSettlement.IsAccepted(response))
			{
				var rejection = SettlementRejected(message.Id, "acknowledge");
				LogAcknowledgeFailed(message.Id, Source, rejection);
				throw rejection;
			}

			LogMessageAcknowledged(message.Id, Source);
		}
		catch (RpcException ex)
		{
			LogAcknowledgeFailed(message.Id, Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		try
		{
			var request = new GrpcAcknowledgeRequest
			{
				MessageId = message.Id,
				Action = requeue ? "requeue" : "reject",
				Reason = reason,
			};

			var method = GrpcMethodDescriptors.CreateAcknowledgeMethod(
				_options.ReceiveMethodPath.Replace("Receive", "Acknowledge", StringComparison.Ordinal));
			// Reject must complete even during shutdown to prevent redelivery;
			// use dedicated timeout instead of caller's cancellation token
			using var rejectCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			var callOptions = CreateCallOptions(rejectCts.Token);

			var response = await _invoker.AsyncUnaryCall(method, null, callOptions, request)
				.ConfigureAwait(false);

			if (!GrpcSettlement.IsAccepted(response))
			{
				var rejection = SettlementRejected(message.Id, requeue ? "requeue" : "reject");
				LogRejectFailed(message.Id, Source, rejection);
				throw rejection;
			}

			LogMessageRejected(message.Id, Source, reason ?? "no reason");
		}
		catch (RpcException ex)
		{
			LogRejectFailed(message.Id, Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(GrpcChannel))
		{
			return _channel;
		}

		return null;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		_channel?.Dispose();
		LogDisposed(Source);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	private TransportReceivedMessage MapToReceivedMessage(GrpcReceivedMessage grpcMessage)
	{
		// Defense-in-depth DoS guard: reject an oversized payload BEFORE materializing the body
		// (Convert.FromBase64String below). The limit is measured against the DECODED byte length —
		// computed arithmetically from the Base64 string, with no decoded allocation — because Base64
		// inflates the wire string by ~33%, so measuring the raw character length would enforce the
		// wrong limit. Fail-closed: throws PayloadTooLargeException, caught by the receive loop to drop
		// the poison message; it never truncates or silently passes an oversized body through.
		PayloadSizeGuard.EnsureBase64WithinLimit(grpcMessage.Body, _maxPayloadBytes);

		var properties = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var (key, value) in grpcMessage.Properties)
		{
			properties[key] = value;
		}

		var providerData = new Dictionary<string, object>();
		foreach (var (key, value) in grpcMessage.ProviderData)
		{
			providerData[key] = value;
		}

		return new TransportReceivedMessage
		{
			Id = grpcMessage.Id,
			Body = Convert.FromBase64String(grpcMessage.Body),
			ContentType = grpcMessage.ContentType,
			MessageType = grpcMessage.MessageType,
			CorrelationId = grpcMessage.CorrelationId,
			Subject = grpcMessage.Subject,
			DeliveryCount = grpcMessage.DeliveryCount,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Source = grpcMessage.Source,
			Properties = properties,
			ProviderData = providerData,
		};
	}

	/// <summary>
	/// Builds the exception raised when the server completed the settlement RPC but reported that it did
	/// NOT accept the settlement. A SUCCESSFUL RPC IS NOT A SUCCESSFUL SETTLEMENT: the two are separate
	/// outcomes and only the transport half was ever checked here. Awaiting the call and discarding its
	/// <c>IsSuccess</c> made a refused acknowledge indistinguishable from an accepted one, so the caller
	/// logged the message settled and moved on while the broker still held it undelivered — the message
	/// is then redelivered on the broker's own timeout with nothing having observed the failure. This
	/// surface returns <see cref="Task"/> and so has no result channel; throwing is the only way to say
	/// the settlement did not happen, and it mirrors the send path, which throws on a rejected send.
	/// </summary>
	/// <remarks>
	/// The type is <see cref="TransportSettlementException"/> rather than a bare
	/// <see cref="InvalidOperationException"/> so that a caller can tell a refused settlement from an
	/// unrelated invalid operation raised by handler code in the same <c>try</c> block. It derives from
	/// <see cref="InvalidOperationException"/>, so an existing handler catching that keeps working.
	/// </remarks>
	private TransportSettlementException SettlementRejected(string messageId, string action) =>
		new($"The gRPC transport completed the {action} RPC for message {messageId} on {Source}, but the "
			+ $"server reported the settlement was not accepted. The message has NOT been settled and "
			+ $"remains owned by the server.")
		{
			TransportName = "Grpc",

			// The server still owns the message, so it is expected to deliver it again. That is the whole
			// reason the refusal is worth reporting: the caller's handler may run a second time.
			RedeliveryExpectation = TransportRedeliveryExpectation.Expected,

			// The server reported a refusal without saying why, so this transport cannot tell a transient
			// refusal from a permanent one. Saying so is honest; guessing Retryable would loop a caller.
			Retryability = SettlementRetryability.Unspecified,
		};

	private CallOptions CreateCallOptions(CancellationToken cancellationToken) =>
		new(
			deadline: DateTime.UtcNow.AddSeconds(_options.DeadlineSeconds),
			cancellationToken: cancellationToken);

	[LoggerMessage(GrpcTransportEventId.ReceiverMessagesReceived, LogLevel.Debug,
		"gRPC transport receiver: {Count} messages received from {Source}")]
	private partial void LogMessagesReceived(string source, int count);

	[LoggerMessage(GrpcTransportEventId.ReceiverReceiveFailed, LogLevel.Error,
		"gRPC transport receiver: failed to receive messages from {Source}")]
	private partial void LogReceiveFailed(string source, Exception exception);

	[LoggerMessage(GrpcTransportEventId.ReceiverMessageAcknowledged, LogLevel.Debug,
		"gRPC transport receiver: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(GrpcTransportEventId.ReceiverAcknowledgeFailed, LogLevel.Error,
		"gRPC transport receiver: failed to acknowledge message {MessageId} from {Source}")]
	private partial void LogAcknowledgeFailed(string messageId, string source, Exception exception);

	[LoggerMessage(GrpcTransportEventId.ReceiverMessageRejected, LogLevel.Warning,
		"gRPC transport receiver: message {MessageId} rejected from {Source}: {Reason}")]
	private partial void LogMessageRejected(string messageId, string source, string reason);

	[LoggerMessage(GrpcTransportEventId.ReceiverRejectFailed, LogLevel.Error,
		"gRPC transport receiver: failed to reject message {MessageId} from {Source}")]
	private partial void LogRejectFailed(string messageId, string source, Exception exception);

	[LoggerMessage(GrpcTransportEventId.ReceiverDisposed, LogLevel.Debug,
		"gRPC transport receiver disposed for {Source}")]
	private partial void LogDisposed(string source);

	[LoggerMessage(GrpcTransportEventId.ReceiverPayloadTooLarge, LogLevel.Warning,
		"gRPC transport receiver: dropped an oversized inbound payload ({PayloadBytes} bytes) from {Source} before materialization")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);
}
