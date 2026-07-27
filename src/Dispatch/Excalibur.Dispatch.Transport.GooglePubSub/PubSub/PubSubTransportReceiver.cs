// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.GooglePubSub;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Google Cloud Pub/Sub implementation of <see cref="ITransportReceiver"/>.
/// Uses <see cref="SubscriberServiceApiClient"/> for native message consumption via <c>PullAsync</c>.
/// </summary>
/// <remarks>
/// Acknowledgment uses the ack ID stored in
/// <see cref="TransportReceivedMessage.ProviderData"/> as <c>"pubsub.ack_id"</c>.
/// </remarks>
internal sealed partial class PubSubTransportReceiver : ITransportReceiver
{
	/// <summary>
	/// The default maximum inbound-payload length (10 MiB) applied when a consumer does not configure
	/// one. Matches Pub/Sub's own maximum message size so a legitimately-sized message is never rejected.
	/// </summary>
	private const int DefaultMaxPayloadBytes = 10 * 1024 * 1024;

	private readonly ISubscriberApiClientSeam _client;
	private readonly int _maxMessages;
	private readonly TimeSpan _requestTimeout;
	private readonly int? _maxPayloadBytes;
	private readonly bool _hasDeadLetterPolicy;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubTransportReceiver"/> class.
	/// </summary>
	/// <param name="client">The Pub/Sub subscriber service API client.</param>
	/// <param name="subscriptionName">The fully qualified subscription name.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="maxMessages">Maximum messages per pull request.</param>
	/// <param name="requestTimeout">Optional request timeout.</param>
	/// <param name="maxPayloadBytes">
	/// The maximum inbound payload length, in bytes, enforced before the body is materialized;
	/// <see langword="null"/> opts out of the size limit. Defaults to 10 MiB (Pub/Sub's message ceiling).
	/// </param>
	/// <param name="hasDeadLetterPolicy">
	/// <see langword="true"/> when a native dead-letter topic is configured for the subscription; governs
	/// how an oversized poison payload is settled (dead-letter/Nack vs drop/Ack). Defaults to
	/// <see langword="false"/>.
	/// </param>
	public PubSubTransportReceiver(
		SubscriberServiceApiClient client,
		string subscriptionName,
		ILogger<PubSubTransportReceiver> logger,
		int maxMessages = 10,
		TimeSpan requestTimeout = default,
		int? maxPayloadBytes = DefaultMaxPayloadBytes,
		bool hasDeadLetterPolicy = false)
		: this(new SubscriberApiClientAdapter(client ?? throw new ArgumentNullException(nameof(client))),
			subscriptionName, logger, maxMessages, requestTimeout, maxPayloadBytes, hasDeadLetterPolicy)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubTransportReceiver"/> class
	/// using a pre-built subscriber adapter. Used by tests to substitute the
	/// SDK via the <see cref="ISubscriberApiClientSeam"/> seam.
	/// </summary>
	internal PubSubTransportReceiver(
		ISubscriberApiClientSeam client,
		string subscriptionName,
		ILogger<PubSubTransportReceiver> logger,
		int maxMessages = 10,
		TimeSpan requestTimeout = default,
		int? maxPayloadBytes = DefaultMaxPayloadBytes,
		bool hasDeadLetterPolicy = false)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		Source = subscriptionName ?? throw new ArgumentNullException(nameof(subscriptionName));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_maxMessages = maxMessages > 0 ? maxMessages : 10;
		_requestTimeout = requestTimeout;
		_maxPayloadBytes = maxPayloadBytes;
		_hasDeadLetterPolicy = hasDeadLetterPolicy;
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		try
		{
			var requested = maxMessages <= 0 ? 1 : maxMessages;
			var maxToPull = Math.Min(requested, _maxMessages);

			var request = new PullRequest
			{
				Subscription = Source,
				MaxMessages = maxToPull,
			};

			var response = await _client.PullAsync(request, cancellationToken)
				.ConfigureAwait(false);

			if (response.ReceivedMessages.Count == 0)
			{
				return [];
			}

			var messages = new List<TransportReceivedMessage>(response.ReceivedMessages.Count);
			foreach (var receivedMessage in response.ReceivedMessages)
			{
				TransportReceivedMessage received;
				try
				{
					received = ConvertToReceivedMessage(receivedMessage);
				}
				catch (PayloadTooLargeException ex)
				{
					// Poison-message guard: an oversized payload can never be processed. Settle via the
					// single shared decision (identical to the streaming subscriber's surface):
					//  - dead-letter policy declared -> NACK (ack deadline 0). A DLQ only routes a message
					//    that exhausts its delivery attempts (nack / deadline-expiry), never an acked one,
					//    so nacking lets it dead-letter after MaxDeliveryAttempts (diagnostic copy kept).
					//  - no dead-letter policy -> ACK to DROP it. A permanent nack with no DLQ to catch the
					//    message would redeliver forever and wedge the subscription; dropping the
					//    unprocessable payload is the fail-safe (liveness over an un-routable poison message).
					// Continue the batch either way so one poison message never aborts the whole pull.
					LogPayloadTooLargeRejected(Source, receivedMessage.Message.Data.Length, ex);
					using var settleCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
					if (PoisonPayloadSettlement.ShouldDeadLetter(_hasDeadLetterPolicy))
					{
						await _client.ModifyAckDeadlineAsync(Source, [receivedMessage.AckId], 0, settleCts.Token)
							.ConfigureAwait(false);
					}
					else
					{
						await _client.AcknowledgeAsync(Source, [receivedMessage.AckId], settleCts.Token)
							.ConfigureAwait(false);
					}

					continue;
				}

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

		var ackId = GetAckId(message);

		try
		{
			// Ack must complete even during shutdown to prevent redelivery;
			// use dedicated timeout instead of caller's cancellation token
			using var ackCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await _client.AcknowledgeAsync(Source, [ackId], ackCts.Token)
				.ConfigureAwait(false);
			LogMessageAcknowledged(message.Id, Source);
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

		var ackId = GetAckId(message);

		try
		{
			// Reject must complete even during shutdown to prevent redelivery;
			// use dedicated timeout instead of caller's cancellation token
			using var rejectCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			if (requeue)
			{
				// Set ack deadline to 0 so message becomes available for redelivery immediately
				await _client.ModifyAckDeadlineAsync(Source, [ackId], 0, rejectCts.Token)
					.ConfigureAwait(false);
				LogMessageRejectedRequeue(message.Id, Source, reason ?? "no reason");
			}
			else
			{
				// Acknowledge the message to remove it; DLQ routing is handled by the decorator or Pub/Sub dead letter policy
				await _client.AcknowledgeAsync(Source, [ackId], rejectCts.Token)
					.ConfigureAwait(false);
				LogMessageRejected(message.Id, Source, reason ?? "no reason");
			}
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
		if (serviceType == typeof(ISubscriberApiClientSeam))
		{
			return _client;
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

	private TransportReceivedMessage ConvertToReceivedMessage(ReceivedMessage receivedMessage)
	{
		var pubsubMessage = receivedMessage.Message;

		// Defense-in-depth DoS guard: reject an oversized payload BEFORE materializing the body
		// (pubsubMessage.Data.Memory below). The raw wire length is the ByteString length — no
		// deserialization needed. Fail-closed: throws PayloadTooLargeException, which the receive
		// loop catches to drop the poison message; never truncated or silently passed.
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
			DeliveryCount = receivedMessage.DeliveryAttempt,
			EnqueuedAt = enqueuedAt,
			Source = Source,
			MessageGroupId = orderingKey,
			PartitionKey = orderingKey,
			Properties = properties,
			ProviderData = new Dictionary<string, object>
			{
				["pubsub.ack_id"] = receivedMessage.AckId,
				["pubsub.message_id"] = pubsubMessage.MessageId,
			},
		};
	}

	private static string GetAckId(TransportReceivedMessage message)
	{
		if (message.ProviderData.TryGetValue("pubsub.ack_id", out var ackId) && ackId is string ackIdStr)
		{
			return ackIdStr;
		}

		throw new InvalidOperationException("Message does not contain a Pub/Sub ack ID in ProviderData.");
	}

	[LoggerMessage(GooglePubSubEventId.TransportReceiverMessageReceived, LogLevel.Debug,
		"Pub/Sub transport receiver: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(GooglePubSubEventId.TransportReceiverReceiveError, LogLevel.Error,
		"Pub/Sub transport receiver: failed to receive messages from {Source}")]
	private partial void LogReceiveError(string source, Exception exception);

	[LoggerMessage(GooglePubSubEventId.TransportReceiverMessageAcknowledged, LogLevel.Debug,
		"Pub/Sub transport receiver: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(GooglePubSubEventId.TransportReceiverAcknowledgeError, LogLevel.Error,
		"Pub/Sub transport receiver: failed to acknowledge message {MessageId} from {Source}")]
	private partial void LogAcknowledgeError(string messageId, string source, Exception exception);

	[LoggerMessage(GooglePubSubEventId.TransportReceiverMessageRejected, LogLevel.Warning,
		"Pub/Sub transport receiver: message {MessageId} rejected from {Source}: {Reason}")]
	private partial void LogMessageRejected(string messageId, string source, string reason);

	[LoggerMessage(GooglePubSubEventId.TransportReceiverMessageRejectedRequeue, LogLevel.Debug,
		"Pub/Sub transport receiver: message {MessageId} rejected (requeue) from {Source}: {Reason}")]
	private partial void LogMessageRejectedRequeue(string messageId, string source, string reason);

	[LoggerMessage(GooglePubSubEventId.TransportReceiverRejectError, LogLevel.Error,
		"Pub/Sub transport receiver: failed to reject message {MessageId} from {Source}")]
	private partial void LogRejectError(string messageId, string source, Exception exception);

	[LoggerMessage(GooglePubSubEventId.TransportReceiverDisposed, LogLevel.Debug,
		"Pub/Sub transport receiver disposed for {Source}")]
	private partial void LogDisposed(string source);

	[LoggerMessage(GooglePubSubEventId.TransportReceiverPayloadTooLarge, LogLevel.Warning,
		"Pub/Sub transport receiver: rejected an oversized inbound payload ({PayloadBytes} bytes) from {Source} before materialization (dead-lettered if configured).")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);
}
