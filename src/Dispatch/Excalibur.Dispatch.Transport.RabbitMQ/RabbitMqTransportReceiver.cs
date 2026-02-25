// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of <see cref="ITransportReceiver"/>.
/// Uses RabbitMQ.Client v7's <see cref="IChannel"/> for native message consumption via <c>BasicGetAsync</c>.
/// </summary>
/// <remarks>
/// Acknowledgment and rejection use the delivery tag stored in
/// <see cref="TransportReceivedMessage.ProviderData"/> as <c>"rabbitmq.delivery_tag"</c>.
/// </remarks>
internal sealed partial class RabbitMqTransportReceiver : ITransportReceiver
{
	// CA2213: DI-injected channel is owned by the container.
	[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
		Justification = "RabbitMQ channel is injected via DI and owned by the container.")]
	private readonly IChannel _channel;

	private readonly string _queueName;
	private readonly ILogger _logger;
	private readonly ConcurrentDictionary<string, ulong> _deliveryTagCache = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqTransportReceiver"/> class.
	/// </summary>
	/// <param name="channel">The RabbitMQ channel.</param>
	/// <param name="source">The source identifier (queue name).</param>
	/// <param name="queueName">The queue name to consume from.</param>
	/// <param name="logger">The logger instance.</param>
	public RabbitMqTransportReceiver(
		IChannel channel,
		string source,
		string queueName,
		ILogger<RabbitMqTransportReceiver> logger)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		try
		{
			var messages = new List<TransportReceivedMessage>();

			for (var i = 0; i < maxMessages && !cancellationToken.IsCancellationRequested; i++)
			{
				var result = await _channel.BasicGetAsync(_queueName, autoAck: false, cancellationToken)
					.ConfigureAwait(false);

				if (result == null)
				{
					break;
				}

				var received = ConvertToReceivedMessage(result);
				messages.Add(received);
				LogMessageReceived(received.Id, Source);
			}

			return messages;
		}
		catch (Exception ex)
		{
			LogReceiveError(Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var receiptHandle = GetReceiptHandle(message);
		try
		{
			if (_deliveryTagCache.TryRemove(receiptHandle, out var deliveryTag))
			{
				await _channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken)
					.ConfigureAwait(false);
				LogMessageAcknowledged(message.Id, Source);
			}
			else
			{
				throw new InvalidOperationException(
					$"Message with receipt handle '{receiptHandle}' not found in delivery tag cache.");
			}
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogAcknowledgeError(message.Id, Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var receiptHandle = GetReceiptHandle(message);
		try
		{
			if (_deliveryTagCache.TryRemove(receiptHandle, out var deliveryTag))
			{
				await _channel.BasicNackAsync(deliveryTag, multiple: false, requeue: requeue, cancellationToken)
					.ConfigureAwait(false);

				if (requeue)
				{
					LogMessageRejectedRequeue(message.Id, Source, reason ?? "no reason");
				}
				else
				{
					LogMessageRejected(message.Id, Source, reason ?? "no reason");
				}
			}
			else
			{
				throw new InvalidOperationException(
					$"Message with receipt handle '{receiptHandle}' not found in delivery tag cache.");
			}
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogRejectError(message.Id, Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(IChannel))
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
		_deliveryTagCache.Clear();
		LogDisposed(Source);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	private TransportReceivedMessage ConvertToReceivedMessage(BasicGetResult result)
	{
		var receiptHandle = $"rabbitmq:{result.DeliveryTag}";
		_deliveryTagCache[receiptHandle] = result.DeliveryTag;

		var properties = new Dictionary<string, object>(StringComparer.Ordinal);
		if (result.BasicProperties.Headers is not null)
		{
			foreach (var header in result.BasicProperties.Headers)
			{
				properties[header.Key] = header.Value switch
				{
					byte[] bytes => Encoding.UTF8.GetString(bytes),
					_ => header.Value?.ToString() ?? string.Empty,
				};
			}
		}

		return new TransportReceivedMessage
		{
			Id = result.BasicProperties.MessageId ?? receiptHandle,
			Body = result.Body.ToArray(),
			ContentType = result.BasicProperties.ContentType,
			MessageType = result.BasicProperties.Type,
			CorrelationId = result.BasicProperties.CorrelationId,
			Subject = properties.TryGetValue("subject", out var subj) ? subj as string : null,
			DeliveryCount = result.Redelivered ? 2 : 1,
			EnqueuedAt = result.BasicProperties.Timestamp.UnixTime > 0
				? DateTimeOffset.FromUnixTimeSeconds(result.BasicProperties.Timestamp.UnixTime)
				: DateTimeOffset.UtcNow,
			Source = Source,
			Properties = properties,
			ProviderData = new Dictionary<string, object>
			{
				["rabbitmq.delivery_tag"] = result.DeliveryTag,
				["rabbitmq.exchange"] = result.Exchange,
				["rabbitmq.routing_key"] = result.RoutingKey,
				["rabbitmq.receipt_handle"] = receiptHandle,
			},
		};
	}

	private static string GetReceiptHandle(TransportReceivedMessage message)
	{
		if (message.ProviderData.TryGetValue("rabbitmq.receipt_handle", out var handle) && handle is string handleStr)
		{
			return handleStr;
		}

		throw new InvalidOperationException("Message does not contain a RabbitMQ receipt handle in ProviderData.");
	}

	[LoggerMessage(RabbitMqEventId.TransportReceiverMessageReceived, LogLevel.Debug,
		"RabbitMQ transport receiver: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(RabbitMqEventId.TransportReceiverReceiveError, LogLevel.Error,
		"RabbitMQ transport receiver: failed to receive messages from {Source}")]
	private partial void LogReceiveError(string source, Exception exception);

	[LoggerMessage(RabbitMqEventId.TransportReceiverMessageAcknowledged, LogLevel.Debug,
		"RabbitMQ transport receiver: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(RabbitMqEventId.TransportReceiverAcknowledgeError, LogLevel.Error,
		"RabbitMQ transport receiver: failed to acknowledge message {MessageId} from {Source}")]
	private partial void LogAcknowledgeError(string messageId, string source, Exception exception);

	[LoggerMessage(RabbitMqEventId.TransportReceiverMessageRejected, LogLevel.Warning,
		"RabbitMQ transport receiver: message {MessageId} rejected from {Source}: {Reason}")]
	private partial void LogMessageRejected(string messageId, string source, string reason);

	[LoggerMessage(RabbitMqEventId.TransportReceiverMessageRejectedRequeue, LogLevel.Debug,
		"RabbitMQ transport receiver: message {MessageId} nacked (requeue) from {Source}: {Reason}")]
	private partial void LogMessageRejectedRequeue(string messageId, string source, string reason);

	[LoggerMessage(RabbitMqEventId.TransportReceiverRejectError, LogLevel.Error,
		"RabbitMQ transport receiver: failed to reject message {MessageId} from {Source}")]
	private partial void LogRejectError(string messageId, string source, Exception exception);

	[LoggerMessage(RabbitMqEventId.TransportReceiverDisposed, LogLevel.Debug,
		"RabbitMQ transport receiver disposed for {Source}")]
	private partial void LogDisposed(string source);
}
