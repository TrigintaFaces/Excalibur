// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Transport.GooglePubSub;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

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
	/// <summary>
	/// The default maximum inbound-payload length (10 MiB) applied when a consumer does not configure
	/// one. Matches Pub/Sub's own maximum message size so a legitimately-sized message is never rejected.
	/// </summary>
	private const int DefaultMaxPayloadBytes = 10 * 1024 * 1024;

	private readonly ISubscriberClientSeam _subscriber;
	private readonly int? _maxPayloadBytes;
	private readonly bool _hasDeadLetterPolicy;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubTransportSubscriber"/> class
	/// with an existing <see cref="SubscriberClient"/>.
	/// </summary>
	/// <param name="subscriber">The Pub/Sub subscriber client.</param>
	/// <param name="source">The subscription name this subscriber reads from.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="maxPayloadBytes">
	/// The maximum inbound payload length, in bytes, enforced before the body is materialized;
	/// <see langword="null"/> opts out of the size limit. Defaults to 10 MiB (Pub/Sub's message ceiling).
	/// </param>
	/// <param name="hasDeadLetterPolicy">
	/// <see langword="true"/> when a native dead-letter topic is configured for the subscription; governs
	/// how an oversized poison payload is settled (dead-letter/Nack vs drop/Ack). Defaults to
	/// <see langword="false"/>.
	/// </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Adapter is stored in _subscriber field and lives for the subscriber's lifetime.")]
	public PubSubTransportSubscriber(
		SubscriberClient subscriber,
		string source,
		ILogger<PubSubTransportSubscriber> logger,
		int? maxPayloadBytes = DefaultMaxPayloadBytes,
		bool hasDeadLetterPolicy = false)
		: this(CreateAdapter(subscriber), source, logger, maxPayloadBytes, hasDeadLetterPolicy)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubTransportSubscriber"/>
	/// class using a pre-built adapter. Used by tests to substitute the SDK via
	/// the <see cref="ISubscriberClientSeam"/> seam.
	/// </summary>
	internal PubSubTransportSubscriber(
		ISubscriberClientSeam subscriber,
		string source,
		ILogger<PubSubTransportSubscriber> logger,
		int? maxPayloadBytes = DefaultMaxPayloadBytes,
		bool hasDeadLetterPolicy = false)
	{
		_subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_maxPayloadBytes = maxPayloadBytes;
		_hasDeadLetterPolicy = hasDeadLetterPolicy;
	}

	private static ISubscriberClientSeam CreateAdapter(SubscriberClient subscriber)
	{
		ArgumentNullException.ThrowIfNull(subscriber);
		return new SubscriberClientAdapter(subscriber);
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
			TransportReceivedMessage received;
			try
			{
				received = ConvertToReceivedMessage(pubsubMessage);
			}
			catch (PayloadTooLargeException ex)
			{
				// Oversized poison message: settle via the single shared decision before its body is read.
				// With a dead-letter policy -> Nack (Pub/Sub routes it to the DLQ after the configured
				// delivery attempts, preserving a diagnostic copy). Without one -> Ack to DROP it, so a
				// permanent Nack with nowhere to dead-letter can't loop forever and wedge the subscription.
				LogPayloadTooLargeRejected(Source, pubsubMessage.Data.Length, ex);
				return PoisonPayloadSettlement.ShouldDeadLetter(_hasDeadLetterPolicy)
					? SubscriberClient.Reply.Nack
					: SubscriberClient.Reply.Ack;
			}

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
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
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
		if (serviceType == typeof(ISubscriberClientSeam))
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

	private TransportReceivedMessage ConvertToReceivedMessage(PubsubMessage pubsubMessage)
	{
		// Defense-in-depth DoS guard: reject an oversized payload BEFORE materializing the body
		// (pubsubMessage.Data.Memory below). The raw wire length is the ByteString length — no
		// deserialization needed. Fail-closed: throws PayloadTooLargeException, which the subscribe
		// loop catches to reject the poison message; never truncated or silently passed.
		PayloadSizeGuard.EnsureWithinLimit(pubsubMessage.Data.Length, _maxPayloadBytes);

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
		var correlationId = attributes.TryGetValue(OutboxHeaderNames.CorrelationId, out var cid) ? cid : null;
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

	[LoggerMessage(GooglePubSubEventId.TransportSubscriberPayloadTooLarge, LogLevel.Warning,
		"Pub/Sub transport subscriber: rejected an oversized inbound payload ({PayloadBytes} bytes) from {Source} before materialization (dead-lettered if configured).")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);
}
