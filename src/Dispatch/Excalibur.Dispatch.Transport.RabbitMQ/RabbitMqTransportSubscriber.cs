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
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public async Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		var consumer = new AsyncEventingBasicConsumer(_channel);

		consumer.ReceivedAsync += async (_, args) =>
		{
			var received = ConvertToReceivedMessage(args);
			LogMessageReceived(received.Id, Source);

			try
			{
				var action = await handler(received, cancellationToken).ConfigureAwait(false);

				switch (action)
				{
					case MessageAction.Acknowledge:
						await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken)
							.ConfigureAwait(false);
						LogMessageAcknowledged(received.Id, Source);
						break;

					case MessageAction.Reject:
						await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken)
							.ConfigureAwait(false);
						LogMessageRejected(received.Id, Source);
						break;

					case MessageAction.Requeue:
						await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, cancellationToken)
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
					await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, cancellationToken)
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
		catch (OperationCanceledException)
		{
			// Expected on cancellation -- fall through to stop
		}
		finally
		{
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

	private TransportReceivedMessage ConvertToReceivedMessage(BasicDeliverEventArgs args)
	{
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
			DeliveryCount = args.Redelivered ? 2 : 1,
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

	[LoggerMessage(RabbitMqEventId.TransportSubscriberDisposed, LogLevel.Debug,
		"RabbitMQ transport subscriber disposed for {Source}")]
	private partial void LogDisposed(string source);
}
