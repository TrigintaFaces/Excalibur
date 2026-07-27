// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// gRPC implementation of <see cref="ITransportSubscriber"/>.
/// Uses gRPC server streaming for push-based message delivery.
/// </summary>
/// <remarks>
/// Opens a server streaming call to the gRPC server, which pushes messages to the client.
/// Each received message is dispatched to the handler callback. The subscription runs until
/// cancellation is requested or the server closes the stream.
/// </remarks>
internal sealed partial class GrpcTransportSubscriber : ITransportSubscriber
{
	private readonly GrpcChannel? _channel;
	private readonly CallInvoker _invoker;
	private readonly GrpcTransportOptions _options;
	private readonly int? _maxPayloadBytes;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="GrpcTransportSubscriber"/> class.
	/// </summary>
	/// <param name="channel">The gRPC channel.</param>
	/// <param name="options">The transport options.</param>
	/// <param name="logger">The logger instance.</param>
	public GrpcTransportSubscriber(
		GrpcChannel channel,
		IOptions<GrpcTransportOptions> options,
		ILogger<GrpcTransportSubscriber> logger)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_maxPayloadBytes = _options.MaxPayloadBytes;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_invoker = _channel.CreateCallInvoker();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="GrpcTransportSubscriber"/> class with an explicit
	/// <see cref="CallInvoker"/> (the gRPC injection seam) instead of a channel. Used to substitute a fake
	/// invoker under test so subscribe/settlement RPCs can be observed without a live server. There is no
	/// owned channel on this path, so <see cref="GetService(Type)"/> returns <see langword="null"/> for
	/// <see cref="GrpcChannel"/> and disposal has no channel to release.
	/// </summary>
	/// <param name="invoker">The gRPC call invoker that issues subscribe and settlement RPCs.</param>
	/// <param name="options">The transport options.</param>
	/// <param name="logger">The logger instance.</param>
	internal GrpcTransportSubscriber(
		CallInvoker invoker,
		IOptions<GrpcTransportOptions> options,
		ILogger<GrpcTransportSubscriber> logger)
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
	public async Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		var request = new GrpcSubscribeRequest { Source = Source };
		var method = GrpcMethodDescriptors.CreateSubscribeMethod(_options.SubscribeMethodPath);
		var callOptions = new CallOptions(cancellationToken: cancellationToken);

		using var call = _invoker.AsyncServerStreamingCall(method, null, callOptions, request);

		LogSubscriptionStarted(Source);

		try
		{
			while (await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
			{
				var grpcMessage = call.ResponseStream.Current;

				TransportReceivedMessage received;
				try
				{
					received = MapToReceivedMessage(grpcMessage);
				}
				catch (PayloadTooLargeException ex)
				{
					// Oversized poison message: drop it BEFORE its body is materialized and continue the
					// stream, mirroring the subscriber's log-and-continue error branch (no requeue loop).
					LogPayloadTooLargeRejected(Source, ex.ActualBytes, ex);
					continue;
				}

				LogMessageReceived(received.Id, Source);

				try
				{
					var action = await handler(received, cancellationToken).ConfigureAwait(false);

					switch (action)
					{
						case MessageAction.Acknowledge:
							await SettleAsync(received.Id, "acknowledge", reason: null, cancellationToken).ConfigureAwait(false);
							LogMessageAcknowledged(received.Id, Source);
							break;
						case MessageAction.Reject:
							await SettleAsync(received.Id, "reject", reason: null, cancellationToken).ConfigureAwait(false);
							LogMessageRejected(received.Id, Source);
							break;
						case MessageAction.Requeue:
							await SettleAsync(received.Id, "requeue", reason: null, cancellationToken).ConfigureAwait(false);
							LogMessageRequeued(received.Id, Source);
							break;
					}
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					LogError(received.Id, Source, ex);
				}
			}

			LogStreamEnded(Source);
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
		{
			// Expected on cancellation
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			// Expected on cancellation
		}
		finally
		{
			LogSubscriptionStopped(Source);
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

	/// <summary>
	/// Sends the settlement decision (acknowledge / reject / requeue) back to the server via the
	/// Acknowledge RPC, so a Reject/Requeue is actually honored (redelivered or dead-lettered) rather
	/// than silently dropped -- an un-settled message would otherwise be lost.
	/// </summary>
	private async Task SettleAsync(string messageId, string action, string? reason, CancellationToken cancellationToken)
	{
		var request = new GrpcAcknowledgeRequest
		{
			MessageId = messageId,
			Action = action,
			Reason = reason,
		};

		var method = GrpcMethodDescriptors.CreateAcknowledgeMethod(
			_options.SubscribeMethodPath.Replace("Subscribe", "Acknowledge", StringComparison.Ordinal));

		// Settlement must complete even during shutdown to prevent redelivery/loss; use a dedicated
		// timeout rather than the caller's cancellation token (mirrors the receiver's ack/reject path).
		using var settleCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var callOptions = new CallOptions(cancellationToken: settleCts.Token);

		_ = await _invoker.AsyncUnaryCall(method, null, callOptions, request).ConfigureAwait(false);
	}

	private TransportReceivedMessage MapToReceivedMessage(GrpcReceivedMessage grpcMessage)
	{
		// Defense-in-depth DoS guard: reject an oversized payload BEFORE materializing the body
		// (Convert.FromBase64String below). The limit is measured against the DECODED byte length —
		// computed arithmetically from the Base64 string, with no decoded allocation — because Base64
		// inflates the wire string by ~33%, so measuring the raw character length would enforce the
		// wrong limit. Fail-closed: throws PayloadTooLargeException, caught by the subscribe loop to
		// drop the poison message; never truncated, never silently passed through.
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

	[LoggerMessage(GrpcTransportEventId.SubscriberStarted, LogLevel.Information,
		"gRPC transport subscriber: subscription started for {Source}")]
	private partial void LogSubscriptionStarted(string source);

	[LoggerMessage(GrpcTransportEventId.SubscriberMessageReceived, LogLevel.Debug,
		"gRPC transport subscriber: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(GrpcTransportEventId.SubscriberMessageAcknowledged, LogLevel.Debug,
		"gRPC transport subscriber: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(GrpcTransportEventId.SubscriberMessageRejected, LogLevel.Warning,
		"gRPC transport subscriber: message {MessageId} rejected from {Source}")]
	private partial void LogMessageRejected(string messageId, string source);

	[LoggerMessage(GrpcTransportEventId.SubscriberMessageRequeued, LogLevel.Debug,
		"gRPC transport subscriber: message {MessageId} requeued from {Source}")]
	private partial void LogMessageRequeued(string messageId, string source);

	[LoggerMessage(GrpcTransportEventId.SubscriberError, LogLevel.Error,
		"gRPC transport subscriber: error processing message {MessageId} from {Source}")]
	private partial void LogError(string messageId, string source, Exception exception);

	[LoggerMessage(GrpcTransportEventId.SubscriberStopped, LogLevel.Information,
		"gRPC transport subscriber: subscription stopped for {Source}")]
	private partial void LogSubscriptionStopped(string source);

	[LoggerMessage(GrpcTransportEventId.SubscriberStreamEnded, LogLevel.Information,
		"gRPC transport subscriber: server stream ended for {Source}")]
	private partial void LogStreamEnded(string source);

	[LoggerMessage(GrpcTransportEventId.SubscriberDisposed, LogLevel.Debug,
		"gRPC transport subscriber disposed for {Source}")]
	private partial void LogDisposed(string source);

	[LoggerMessage(GrpcTransportEventId.SubscriberPayloadTooLarge, LogLevel.Warning,
		"gRPC transport subscriber: dropped an oversized inbound payload ({PayloadBytes} bytes) from {Source} before materialization")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);
}
