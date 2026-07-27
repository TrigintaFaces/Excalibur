// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	private readonly GrpcChannel _channel;
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

			await _invoker.AsyncUnaryCall(method, null, callOptions, request)
				.ConfigureAwait(false);

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

			await _invoker.AsyncUnaryCall(method, null, callOptions, request)
				.ConfigureAwait(false);

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
		_channel.Dispose();
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
