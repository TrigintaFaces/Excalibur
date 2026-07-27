// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of <see cref="ITransportSubscriber"/>.
/// Uses RabbitMQ.Client v7's <see cref="AsyncEventingBasicConsumer"/> with <c>BasicConsumeAsync</c>
/// for native push-based message delivery.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="AsyncEventingBasicConsumer"/> provides a push-based consumption model where RabbitMQ
/// delivers messages to registered event handlers. This maps directly to the <see cref="ITransportSubscriber"/>
/// pattern -- the handler callback is invoked for each received message.
/// </para>
/// <para>
/// Message settlement is determined by the <see cref="MessageAction"/> returned from the handler:
/// <list type="bullet">
/// <item><see cref="MessageAction.Acknowledge"/> calls <c>BasicAckAsync</c>.</item>
/// <item><see cref="MessageAction.Reject"/> calls <c>BasicNackAsync(requeue: false)</c>.</item>
/// <item><see cref="MessageAction.Requeue"/> calls <c>BasicNackAsync(requeue: true)</c>.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class RabbitMqTransportSubscriber : ITransportSubscriber
{
	// CA2213: DI-injected channel is owned by the container.
	[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
		Justification = "RabbitMQ channel is injected via DI and owned by the container.")]
	private readonly IChannel _channel;

	private readonly string _queueName;
	private readonly ushort _prefetchCount;
	private readonly bool _prefetchGlobal;
	private readonly int? _maxPayloadBytes;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqTransportSubscriber"/> class.
	/// </summary>
	/// <param name="channel">The RabbitMQ channel.</param>
	/// <param name="source">The source identifier (queue name).</param>
	/// <param name="queueName">The queue name to consume from.</param>
	/// <param name="logger">The logger instance.</param>
	public RabbitMqTransportSubscriber(
		IChannel channel,
		string source,
		string queueName,
		ILogger<RabbitMqTransportSubscriber> logger)
		: this(channel, source, queueName, logger, prefetchCount: 0, prefetchGlobal: false)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqTransportSubscriber"/> class.
	/// </summary>
	/// <param name="channel">The RabbitMQ channel.</param>
	/// <param name="source">The source identifier (queue name).</param>
	/// <param name="queueName">The queue name to consume from.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="prefetchCount">Optional QoS prefetch count applied before subscription.</param>
	/// <param name="prefetchGlobal">Whether QoS is applied globally to the channel.</param>
	/// <param name="maxPayloadBytes">
	/// The maximum inbound payload length, in bytes, enforced before the body is materialized;
	/// <see langword="null"/> opts out of the size limit.
	/// </param>
	public RabbitMqTransportSubscriber(
		IChannel channel,
		string source,
		string queueName,
		ILogger<RabbitMqTransportSubscriber> logger,
		ushort prefetchCount,
		bool prefetchGlobal,
		int? maxPayloadBytes = PayloadSizeGuard.DefaultMaxPayloadBytes)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_prefetchCount = prefetchCount;
		_prefetchGlobal = prefetchGlobal;
		_maxPayloadBytes = maxPayloadBytes;
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public async Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		if (_prefetchCount > 0)
		{
			await _channel.BasicQosAsync(
				prefetchSize: 0,
				prefetchCount: _prefetchCount,
				global: _prefetchGlobal,
				cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		// Use a separate CTS for in-flight message processing so that handlers can
		// complete ack/nack even after the subscription token is cancelled.
		using var messageProcessingCts = new CancellationTokenSource();

		var consumer = new AsyncEventingBasicConsumer(_channel);

		consumer.ReceivedAsync += async (_, args) =>
		{
			TransportReceivedMessage received;
			try
			{
				received = ConvertToReceivedMessage(args);
			}
			catch (PayloadTooLargeException ex)
			{
				// Oversized poison message: reject to the DLX (no requeue) before it can loop.
				LogPayloadTooLargeRejected(Source, args.Body.Length, ex);
				await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, CancellationToken.None)
					.ConfigureAwait(false);
				return;
			}

			LogMessageReceived(received.Id, Source);

			try
			{
				var action = await handler(received, messageProcessingCts.Token).ConfigureAwait(false);

				switch (action)
				{
					case MessageAction.Acknowledge:
						await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, messageProcessingCts.Token)
							.ConfigureAwait(false);
						LogMessageAcknowledged(received.Id, Source);
						break;

					case MessageAction.Reject:
						await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, messageProcessingCts.Token)
							.ConfigureAwait(false);
						LogMessageRejected(received.Id, Source);
						break;

					case MessageAction.Requeue:
						await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, messageProcessingCts.Token)
							.ConfigureAwait(false);
						LogMessageRequeued(received.Id, Source);
						break;
				}
			}
			catch (Exception ex)
			{
				LogError(received.Id, Source, ex);
				// Nack with requeue so the message becomes visible again for retry
				try
				{
					await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, CancellationToken.None)
						.ConfigureAwait(false);
				}
				catch (Exception nackEx)
				{
					LogError(received.Id, Source, nackEx);
				}
			}
		};

		var consumerTag = await _channel.BasicConsumeAsync(
			_queueName,
			autoAck: false,
			consumer: consumer,
			cancellationToken: cancellationToken).ConfigureAwait(false);

		LogSubscriptionStarted(Source);

		try
		{
			// Block until cancellation is requested
			await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			// Expected on cancellation -- fall through to stop
		}
		finally
		{
			// Cancel in-flight message processing after a grace period
			// to allow current handlers to complete ack/nack
			await messageProcessingCts.CancelAsync().ConfigureAwait(false);

			try
			{
				await _channel.BasicCancelAsync(consumerTag, noWait: false, CancellationToken.None)
					.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				LogError("N/A", Source, ex);
			}

			LogSubscriptionStopped(Source);
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
		LogDisposed(Source);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Computes the true delivery attempt count. Quorum queues publish the count of prior (failed)
	/// deliveries in the <c>x-delivery-count</c> header (0 on the first delivery), so the attempt count is
	/// that value + 1 — this lets poison-detection thresholds (e.g. attempts &gt;= 5) actually fire. When the
	/// header is absent (classic queues), fall back to the coarse <see cref="BasicDeliverEventArgs.Redelivered"/>
	/// flag, which only distinguishes a first delivery (1) from any redelivery (2) and therefore saturates.
	/// </summary>
	private static int ComputeDeliveryCount(BasicDeliverEventArgs args)
	{
		if (args.BasicProperties.Headers is not null
			&& args.BasicProperties.Headers.TryGetValue("x-delivery-count", out var raw)
			&& TryGetInt64(raw, out var priorDeliveries)
			&& priorDeliveries >= 0)
		{
			// priorDeliveries is a long from the broker; clamp into int for the (attempts + 1) count.
			var attempts = priorDeliveries + 1;
			return attempts > int.MaxValue ? int.MaxValue : (int)attempts;
		}

		return args.Redelivered ? 2 : 1;
	}

	/// <summary>
	/// Reads a RabbitMQ header numeric value (delivered as a boxed integral type, or a UTF-8 byte string)
	/// into an <see cref="long"/>.
	/// </summary>
	private static bool TryGetInt64(object? raw, out long value)
	{
		switch (raw)
		{
			case long l: value = l; return true;
			case int i: value = i; return true;
			case short s: value = s; return true;
			case byte b: value = b; return true;
			case sbyte sb: value = sb; return true;
			case uint ui: value = ui; return true;
			case ushort us: value = us; return true;
			case byte[] bytes when long.TryParse(Encoding.UTF8.GetString(bytes), out var parsed):
				value = parsed; return true;
			case string str when long.TryParse(str, out var parsed):
				value = parsed; return true;
			default:
				value = 0; return false;
		}
	}

	private TransportReceivedMessage ConvertToReceivedMessage(BasicDeliverEventArgs args)
	{
		// Defense-in-depth DoS guard: reject an oversized payload BEFORE materializing the body
		// (args.Body.ToArray() below). Fail-closed — throws PayloadTooLargeException, which the
		// subscription loop catches to nack the poison message (no requeue); never truncated/dropped.
		PayloadSizeGuard.EnsureWithinLimit(args.Body.Length, _maxPayloadBytes);

		var receiptHandle = $"rabbitmq:{args.DeliveryTag}";

		var properties = new Dictionary<string, object>(StringComparer.Ordinal);
		if (args.BasicProperties.Headers is not null)
		{
			foreach (var header in args.BasicProperties.Headers)
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
			Id = args.BasicProperties.MessageId ?? receiptHandle,
			Body = args.Body.ToArray(),
			ContentType = args.BasicProperties.ContentType,
			MessageType = args.BasicProperties.Type,
			CorrelationId = args.BasicProperties.CorrelationId,
			Subject = properties.TryGetValue("subject", out var subj) ? subj as string : null,
			DeliveryCount = ComputeDeliveryCount(args),
			EnqueuedAt = args.BasicProperties.Timestamp.UnixTime > 0
				? DateTimeOffset.FromUnixTimeSeconds(args.BasicProperties.Timestamp.UnixTime)
				: DateTimeOffset.UtcNow,
			Source = Source,
			Properties = properties,
			ProviderData = new Dictionary<string, object>
			{
				["rabbitmq.delivery_tag"] = args.DeliveryTag,
				["rabbitmq.exchange"] = args.Exchange,
				["rabbitmq.routing_key"] = args.RoutingKey,
				["rabbitmq.receipt_handle"] = receiptHandle,
			},
		};
	}

	[LoggerMessage(RabbitMqEventId.TransportSubscriberStarted, LogLevel.Information,
		"RabbitMQ transport subscriber: subscription started for {Source}")]
	private partial void LogSubscriptionStarted(string source);

	[LoggerMessage(RabbitMqEventId.TransportSubscriberMessageReceived, LogLevel.Debug,
		"RabbitMQ transport subscriber: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(RabbitMqEventId.TransportSubscriberMessageAcknowledged, LogLevel.Debug,
		"RabbitMQ transport subscriber: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(RabbitMqEventId.TransportSubscriberMessageRejected, LogLevel.Warning,
		"RabbitMQ transport subscriber: message {MessageId} rejected from {Source}")]
	private partial void LogMessageRejected(string messageId, string source);

	[LoggerMessage(RabbitMqEventId.TransportSubscriberMessageRequeued, LogLevel.Debug,
		"RabbitMQ transport subscriber: message {MessageId} requeued from {Source}")]
	private partial void LogMessageRequeued(string messageId, string source);

	[LoggerMessage(RabbitMqEventId.TransportSubscriberError, LogLevel.Error,
		"RabbitMQ transport subscriber: error processing message {MessageId} from {Source}")]
	private partial void LogError(string messageId, string source, Exception exception);

	[LoggerMessage(RabbitMqEventId.TransportSubscriberStopped, LogLevel.Information,
		"RabbitMQ transport subscriber: subscription stopped for {Source}")]
	private partial void LogSubscriptionStopped(string source);

	[LoggerMessage(RabbitMqEventId.TransportSubscriberPayloadTooLarge, LogLevel.Warning,
		"RabbitMQ transport subscriber: rejected an oversized inbound payload ({PayloadBytes} bytes) from {Source} before materialization")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);

	[LoggerMessage(RabbitMqEventId.TransportSubscriberDisposed, LogLevel.Debug,
		"RabbitMQ transport subscriber disposed for {Source}")]
	private partial void LogDisposed(string source);
}
