// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Api.Gax;
using Google.Api.Gax.Grpc;
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
	private readonly SubscriberServiceApiClient _client;
	private readonly int _maxMessages;
	private readonly TimeSpan _requestTimeout;
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
	public PubSubTransportReceiver(
		SubscriberServiceApiClient client,
		string subscriptionName,
		ILogger<PubSubTransportReceiver> logger,
		int maxMessages = 10,
		TimeSpan requestTimeout = default)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		Source = subscriptionName ?? throw new ArgumentNullException(nameof(subscriptionName));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_maxMessages = maxMessages > 0 ? maxMessages : 10;
		_requestTimeout = requestTimeout;
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

			var response = await _client.PullAsync(request, CreateCallSettings(cancellationToken))
				.ConfigureAwait(false);

			if (response.ReceivedMessages.Count == 0)
			{
				return [];
			}

			var messages = new List<TransportReceivedMessage>(response.ReceivedMessages.Count);
			foreach (var receivedMessage in response.ReceivedMessages)
			{
				var received = ConvertToReceivedMessage(receivedMessage);
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
			var request = new AcknowledgeRequest
			{
				Subscription = Source,
				AckIds = { ackId },
			};

			await _client.AcknowledgeAsync(request, CreateCallSettings(cancellationToken))
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
			if (requeue)
			{
				// Set ack deadline to 0 so message becomes available for redelivery immediately
				var request = new ModifyAckDeadlineRequest
				{
					Subscription = Source,
					AckIds = { ackId },
					AckDeadlineSeconds = 0,
				};

				await _client.ModifyAckDeadlineAsync(request, CreateCallSettings(cancellationToken))
					.ConfigureAwait(false);
				LogMessageRejectedRequeue(message.Id, Source, reason ?? "no reason");
			}
			else
			{
				// Acknowledge the message to remove it; DLQ routing is handled by the decorator or Pub/Sub dead letter policy
				var request = new AcknowledgeRequest
				{
					Subscription = Source,
					AckIds = { ackId },
				};

				await _client.AcknowledgeAsync(request, CreateCallSettings(cancellationToken))
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
		if (serviceType == typeof(SubscriberServiceApiClient))
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

	private CallSettings CreateCallSettings(CancellationToken cancellationToken)
	{
		var callSettings = CallSettings.FromCancellationToken(cancellationToken);
		if (_requestTimeout > TimeSpan.Zero)
		{
			callSettings = callSettings.WithExpiration(Expiration.FromTimeout(_requestTimeout));
		}

		return callSettings;
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
}
