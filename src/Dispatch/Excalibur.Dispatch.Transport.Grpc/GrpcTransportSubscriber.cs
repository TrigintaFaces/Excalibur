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
	private readonly GrpcChannel _channel;
	private readonly CallInvoker _invoker;
	private readonly GrpcTransportOptions _options;
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
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_invoker = _channel.CreateCallInvoker();
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
				var received = MapToReceivedMessage(grpcMessage);
				LogMessageReceived(received.Id, Source);

				try
				{
					var action = await handler(received, cancellationToken).ConfigureAwait(false);

					switch (action)
					{
						case MessageAction.Acknowledge:
							LogMessageAcknowledged(received.Id, Source);
							break;
						case MessageAction.Reject:
							LogMessageRejected(received.Id, Source);
							break;
						case MessageAction.Requeue:
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
		catch (OperationCanceledException)
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
		_channel.Dispose();
		LogDisposed(Source);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	private static TransportReceivedMessage MapToReceivedMessage(GrpcReceivedMessage grpcMessage)
	{
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
}
