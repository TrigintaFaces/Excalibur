// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Google Cloud Pub/Sub implementation of <see cref="ITransportSubscriber"/>.
/// Uses <see cref="SubscriberClient"/> for native push-based message delivery via streaming pull.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="SubscriberClient"/> provides a push-based consumption model where Pub/Sub
/// delivers messages to a registered handler callback. This maps directly to the
/// <see cref="ITransportSubscriber"/> pattern.
/// </para>
/// <para>
/// Message settlement is determined by the <see cref="MessageAction"/> returned from the handler:
/// <list type="bullet">
/// <item><see cref="MessageAction.Acknowledge"/> returns <see cref="SubscriberClient.Reply.Ack"/>.</item>
/// <item><see cref="MessageAction.Reject"/> returns <see cref="SubscriberClient.Reply.Nack"/>.</item>
/// <item><see cref="MessageAction.Requeue"/> returns <see cref="SubscriberClient.Reply.Nack"/>.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class PubSubTransportSubscriber : ITransportSubscriber
{
	private readonly SubscriberClient _subscriber;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubTransportSubscriber"/> class.
	/// </summary>
	/// <param name="subscriber">The Pub/Sub subscriber client.</param>
	/// <param name="source">The subscription name this subscriber reads from.</param>
	/// <param name="logger">The logger instance.</param>
	public PubSubTransportSubscriber(
		SubscriberClient subscriber,
		string source,
		ILogger<PubSubTransportSubscriber> logger)
	{
		_subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
		Source = source ?? throw new ArgumentNullException(nameof(source));
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

		await _subscriber.StartAsync(async (pubsubMessage, ct) =>
		{
			var received = ConvertToReceivedMessage(pubsubMessage);
			LogMessageReceived(received.Id, Source);

			try
			{
				var action = await handler(received, ct).ConfigureAwait(false);

				switch (action)
				{
					case MessageAction.Acknowledge:
						LogMessageAcknowledged(received.Id, Source);
						return SubscriberClient.Reply.Ack;

					case MessageAction.Reject:
						LogMessageRejected(received.Id, Source);
						return SubscriberClient.Reply.Nack;

					case MessageAction.Requeue:
						LogMessageRequeued(received.Id, Source);
						return SubscriberClient.Reply.Nack;

					default:
						return SubscriberClient.Reply.Nack;
				}
			}
			catch (Exception ex)
			{
				LogError(received.Id, Source, ex);
				return SubscriberClient.Reply.Nack;
			}
		}).ConfigureAwait(false);

		LogSubscriptionStarted(Source);

		try
		{
			// Block until cancellation is requested
			await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected on cancellation - fall through to stop
		}
		finally
		{
			try
			{
				await _subscriber.StopAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				// StopAsync throws InvalidOperationException if subscriber never started - safe to ignore
			}

			LogSubscriptionStopped(Source);
		}
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(SubscriberClient))
		{
			return _subscriber;
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

	private static TransportReceivedMessage ConvertToReceivedMessage(PubsubMessage pubsubMessage)
	{
		var attributes = pubsubMessage.Attributes;
		var properties = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var attr in attributes)
		{
			properties[attr.Key] = attr.Value;
		}

		var messageId = !string.IsNullOrWhiteSpace(pubsubMessage.MessageId)
			? pubsubMessage.MessageId
			: attributes.TryGetValue("message-id", out var mid) ? mid : Guid.NewGuid().ToString("N");

		var contentType = attributes.TryGetValue("content-type", out var ct) ? ct : null;
		var correlationId = attributes.TryGetValue("correlation-id", out var cid) ? cid : null;
		var messageType = attributes.TryGetValue("message-type", out var mt) ? mt : null;
		var subject = attributes.TryGetValue("subject", out var subj) ? subj : null;
		var orderingKey = string.IsNullOrWhiteSpace(pubsubMessage.OrderingKey) ? null : pubsubMessage.OrderingKey;

		var enqueuedAt = pubsubMessage.PublishTime is not null
			? pubsubMessage.PublishTime.ToDateTimeOffset()
			: DateTimeOffset.UtcNow;

		return new TransportReceivedMessage
		{
			Id = messageId,
			Body = pubsubMessage.Data.Memory,
			ContentType = contentType,
			MessageType = messageType,
			CorrelationId = correlationId,
			Subject = subject,
			EnqueuedAt = enqueuedAt,
			Source = null, // Subscriber does not have per-message ack_id
			MessageGroupId = orderingKey,
			PartitionKey = orderingKey,
			Properties = properties,
			ProviderData = new Dictionary<string, object>
			{
				["pubsub.message_id"] = pubsubMessage.MessageId,
				["pubsub.publish_time"] = pubsubMessage.PublishTime is not null
					? pubsubMessage.PublishTime.ToDateTimeOffset()
					: DBNull.Value,
			},
		};
	}

	[LoggerMessage(GooglePubSubEventId.TransportSubscriberStarted, LogLevel.Information,
		"Pub/Sub transport subscriber: subscription started for {Source}")]
	private partial void LogSubscriptionStarted(string source);

	[LoggerMessage(GooglePubSubEventId.TransportSubscriberMessageReceived, LogLevel.Debug,
		"Pub/Sub transport subscriber: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(GooglePubSubEventId.TransportSubscriberMessageAcknowledged, LogLevel.Debug,
		"Pub/Sub transport subscriber: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(GooglePubSubEventId.TransportSubscriberMessageRejected, LogLevel.Warning,
		"Pub/Sub transport subscriber: message {MessageId} rejected from {Source}")]
	private partial void LogMessageRejected(string messageId, string source);

	[LoggerMessage(GooglePubSubEventId.TransportSubscriberMessageRequeued, LogLevel.Debug,
		"Pub/Sub transport subscriber: message {MessageId} requeued from {Source}")]
	private partial void LogMessageRequeued(string messageId, string source);

	[LoggerMessage(GooglePubSubEventId.TransportSubscriberError, LogLevel.Error,
		"Pub/Sub transport subscriber: error processing message {MessageId} from {Source}")]
	private partial void LogError(string messageId, string source, Exception exception);

	[LoggerMessage(GooglePubSubEventId.TransportSubscriberStopped, LogLevel.Information,
		"Pub/Sub transport subscriber: subscription stopped for {Source}")]
	private partial void LogSubscriptionStopped(string source);

	[LoggerMessage(GooglePubSubEventId.TransportSubscriberDisposed, LogLevel.Debug,
		"Pub/Sub transport subscriber disposed for {Source}")]
	private partial void LogDisposed(string source);
}
