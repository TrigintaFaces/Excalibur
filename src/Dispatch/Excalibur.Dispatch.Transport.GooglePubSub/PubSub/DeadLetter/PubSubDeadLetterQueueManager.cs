// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;
using System.Diagnostics;
using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

using Grpc.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Implements dead letter queue management for Google Pub/Sub, aligned to the
/// shared <see cref="IDeadLetterQueueManager"/> interface from Transport.Abstractions.
/// </summary>
/// <remarks>
/// Google-specific parameters (<see cref="SubscriptionName"/>, <see cref="TopicName"/>)
/// are injected via <see cref="DeadLetterOptions"/> rather than per-method parameters.
/// The <see cref="ConfigureDeadLetterPolicyAsync"/> method is a PubSub-specific extension
/// not part of the shared interface.
/// </remarks>
public sealed partial class PubSubDeadLetterQueueManager : IDeadLetterQueueManager, IDisposable
{
	private readonly SubscriberServiceApiClient _subscriberClient;
	private readonly PublisherServiceApiClient _publisherClient;
	private readonly DeadLetterOptions _options;
	private readonly ILogger<PubSubDeadLetterQueueManager> _logger;
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Transport.GooglePubSub.DeadLetter");
	private readonly SemaphoreSlim _reprocessLock = new(1, 1);

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubDeadLetterQueueManager" /> class.
	/// </summary>
	public PubSubDeadLetterQueueManager(
		SubscriberServiceApiClient subscriberClient,
		PublisherServiceApiClient publisherClient,
		IOptions<DeadLetterOptions> options,
		ILogger<PubSubDeadLetterQueueManager> logger)
	{
		_subscriberClient = subscriberClient ?? throw new ArgumentNullException(nameof(subscriberClient));
		_publisherClient = publisherClient ?? throw new ArgumentNullException(nameof(publisherClient));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Configures a dead letter policy for a subscription.
	/// This is a Google Pub/Sub-specific operation not part of the shared DLQ interface.
	/// </summary>
	/// <returns>A <see cref="DeadLetterPolicy"/> representing the configured policy.</returns>
	public async Task<DeadLetterPolicy> ConfigureDeadLetterPolicyAsync(
		SubscriptionName subscription,
		TopicName deadLetterTopic,
		int maxDeliveryAttempts,
		CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("ConfigureDeadLetterPolicy");
		_ = activity?.SetTag("subscription", subscription.ToString());
		_ = activity?.SetTag("dlq_topic", deadLetterTopic.ToString());
		_ = activity?.SetTag("max_attempts", maxDeliveryAttempts);

		try
		{
			var sub = await _subscriberClient.GetSubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);

			sub.DeadLetterPolicy = new DeadLetterPolicy
			{
				DeadLetterTopic = deadLetterTopic.ToString(),
				MaxDeliveryAttempts = maxDeliveryAttempts,
			};

			var updateRequest = new UpdateSubscriptionRequest
			{
				Subscription = sub,
				UpdateMask = new FieldMask { Paths = { "dead_letter_policy" } },
			};

			var updated = await _subscriberClient.UpdateSubscriptionAsync(updateRequest, cancellationToken).ConfigureAwait(false);

			LogConfiguredDeadLetterPolicy(subscription.ToString(), deadLetterTopic.ToString(), maxDeliveryAttempts);

			return updated.DeadLetterPolicy;
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
		{
			LogSubscriptionNotFound(subscription.ToString(), ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<string> MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("MoveToDeadLetter");
		_ = activity?.SetTag("message_id", message.Id);
		_ = activity?.SetTag("reason", reason);

		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(reason);

		var dlqTopic = _options.DeadLetterTopicName ??
					   throw new InvalidOperationException("Dead letter topic not configured. Set DeadLetterTopicName in DeadLetterOptions.");

		var dlqMessage = new PubsubMessage
		{
			Data = ByteString.CopyFrom(message.Body.Span),
			Attributes =
			{
				["dlq_reason"] = reason,
				["dlq_original_message_id"] = message.Id,
				["dlq_delivery_attempts"] = "0",
				["dlq_original_source"] = "unknown",
				["dlq_timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
			},
		};

		// Copy original properties as DLQ attributes
		foreach (var prop in message.Properties)
		{
			dlqMessage.Attributes[$"dlq_original_{prop.Key}"] = prop.Value?.ToString() ?? string.Empty;
		}

		var publishRequest = new PublishRequest { TopicAsTopicName = dlqTopic, Messages = { dlqMessage } };

		var response = await _publisherClient.PublishAsync(publishRequest, cancellationToken).ConfigureAwait(false);
		var messageId = response.MessageIds.FirstOrDefault() ?? string.Empty;

		LogMovedToDeadLetterQueue(message.Id, reason);

		if (exception != null)
		{
			LogExceptionCausedDeadLettering(exception);
		}

		return messageId;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		var deadLetterSubscription = _options.DeadLetterSubscriptionName ??
			throw new InvalidOperationException("Dead letter subscription not configured. Set DeadLetterSubscriptionName in DeadLetterOptions.");

		using var activity = _activitySource.StartActivity("GetDeadLetterMessages");
		_ = activity?.SetTag("subscription", deadLetterSubscription.ToString());
		_ = activity?.SetTag("max_messages", maxMessages);

		var pullRequest = new PullRequest { SubscriptionAsSubscriptionName = deadLetterSubscription, MaxMessages = maxMessages };

		var response = await _subscriberClient.PullAsync(pullRequest, cancellationToken).ConfigureAwait(false);
		var deadLetterMessages = new List<DeadLetterMessage>();

		foreach (var receivedMessage in response.ReceivedMessages)
		{
			try
			{
				var dlqMessage = ParseDeadLetterMessage(receivedMessage);
				deadLetterMessages.Add(dlqMessage);
			}
			catch (Exception ex)
			{
				LogFailedToParse(receivedMessage.Message.MessageId, ex);
			}
		}

		LogRetrievedDeadLetterMessages(deadLetterMessages.Count, deadLetterSubscription.ToString());

		return deadLetterMessages;
	}

	/// <inheritdoc />
	public async Task<ReprocessResult> ReprocessDeadLetterMessagesAsync(
		IEnumerable<DeadLetterMessage> messages,
		ReprocessOptions options,
		CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("ReprocessDeadLetterMessages");

		ArgumentNullException.ThrowIfNull(messages);
		ArgumentNullException.ThrowIfNull(options);

		var messageList = messages.ToList();
		_ = activity?.SetTag("message_count", messageList.Count);
		_ = activity?.SetTag("target_queue", options.TargetQueue ?? string.Empty);

		await _reprocessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var result = new ReprocessResult();
			var stopwatch = ValueStopwatch.StartNew();

			// Apply filter if provided
			if (options.MessageFilter != null)
			{
				var filtered = messageList.Where(options.MessageFilter).ToList();
				result.SkippedCount = messageList.Count - filtered.Count;
				messageList = filtered;
			}

			foreach (var dlqMessage in messageList)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					break;
				}

				try
				{
					await ReprocessSingleMessageAsync(dlqMessage, options, cancellationToken).ConfigureAwait(false);
					result.SuccessCount++;
				}
				catch (Exception ex)
				{
					result.FailureCount++;
					result.Failures.Add(new ReprocessFailure
					{
						Message = dlqMessage,
						Reason = $"Reprocessing failed: {ex.Message}",
						Exception = ex,
					});

					LogFailedToReprocessMessage(dlqMessage.OriginalMessage.Id, ex);
				}

				if (options.RetryDelay > TimeSpan.Zero)
				{
					await Task.Delay(options.RetryDelay, cancellationToken).ConfigureAwait(false);
				}
			}
			result.ProcessingTime = stopwatch.Elapsed;

			LogReprocessedMessages(result.SuccessCount, messageList.Count, (long)stopwatch.Elapsed.TotalMilliseconds,
				result.SkippedCount, result.FailureCount);

			return result;
		}
		finally
		{
			_ = _reprocessLock.Release();
		}
	}

	/// <inheritdoc />
	public async Task<DeadLetterStatistics> GetStatisticsAsync(
		CancellationToken cancellationToken)
	{
		var deadLetterSubscription = _options.DeadLetterSubscriptionName ??
			throw new InvalidOperationException("Dead letter subscription not configured. Set DeadLetterSubscriptionName in DeadLetterOptions.");

		using var activity = _activitySource.StartActivity("GetDeadLetterStatistics");
		_ = activity?.SetTag("subscription", deadLetterSubscription.ToString());

		var stats = new DeadLetterStatistics();
		var oldestMessage = DateTimeOffset.MaxValue;
		var newestMessage = DateTimeOffset.MinValue;
		var totalDeliveryAttempts = 0;
		var messageCount = 0;

		var hasMore = true;
		while (hasMore && !cancellationToken.IsCancellationRequested)
		{
			var pullRequest = new PullRequest
			{
				SubscriptionAsSubscriptionName = deadLetterSubscription,
				MaxMessages = 100,
			};

			var response = await _subscriberClient.PullAsync(pullRequest, cancellationToken).ConfigureAwait(false);

			if (response.ReceivedMessages.Count == 0)
			{
				hasMore = false;
				continue;
			}

			foreach (var msg in response.ReceivedMessages)
			{
				messageCount++;

				if (msg.Message.Attributes.TryGetValue("dlq_reason", out var reason))
				{
					stats.ReasonBreakdown[reason] = stats.ReasonBreakdown.GetValueOrDefault(reason) + 1;
				}

				if (msg.Message.Attributes.TryGetValue("dlq_original_source", out var source))
				{
					stats.SourceBreakdown[source] = stats.SourceBreakdown.GetValueOrDefault(source) + 1;
				}

				if (msg.Message.Attributes.TryGetValue("dlq_delivery_attempts", out var attempts) &&
					int.TryParse(attempts, System.Globalization.CultureInfo.InvariantCulture, out var attemptCount))
				{
					totalDeliveryAttempts += attemptCount;
				}

				if (msg.Message.Attributes.TryGetValue("dlq_timestamp", out var timestamp) &&
					DateTimeOffset.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture,
						System.Globalization.DateTimeStyles.None, out var dlqTime))
				{
					if (dlqTime < oldestMessage)
					{
						oldestMessage = dlqTime;
					}

					if (dlqTime > newestMessage)
					{
						newestMessage = dlqTime;
					}
				}
			}

			// Don't acknowledge - we're just reading for stats
		}

		stats.MessageCount = messageCount;
		stats.AverageDeliveryAttempts = messageCount > 0 ? (double)totalDeliveryAttempts / messageCount : 0;

		if (oldestMessage < DateTimeOffset.MaxValue)
		{
			stats.OldestMessageAge = DateTimeOffset.UtcNow - oldestMessage;
		}

		if (newestMessage > DateTimeOffset.MinValue)
		{
			stats.NewestMessageAge = DateTimeOffset.UtcNow - newestMessage;
		}

		return stats;
	}

	/// <inheritdoc />
	public async Task<int> PurgeDeadLetterQueueAsync(CancellationToken cancellationToken)
	{
		var deadLetterSubscription = _options.DeadLetterSubscriptionName ??
			throw new InvalidOperationException("Dead letter subscription not configured. Set DeadLetterSubscriptionName in DeadLetterOptions.");

		using var activity = _activitySource.StartActivity("PurgeDeadLetterQueue");
		_ = activity?.SetTag("subscription", deadLetterSubscription.ToString());

		var purgedCount = 0;

		// Pull and acknowledge all messages in batches to purge the subscription
		var hasMore = true;
		while (hasMore && !cancellationToken.IsCancellationRequested)
		{
			var pullRequest = new PullRequest
			{
				SubscriptionAsSubscriptionName = deadLetterSubscription,
				MaxMessages = 100,
			};

			var response = await _subscriberClient.PullAsync(pullRequest, cancellationToken).ConfigureAwait(false);

			if (response.ReceivedMessages.Count == 0)
			{
				hasMore = false;
				continue;
			}

			var ackIds = response.ReceivedMessages.Select(static m => m.AckId).ToList();

			await _subscriberClient.AcknowledgeAsync(deadLetterSubscription, ackIds, cancellationToken).ConfigureAwait(false);

			purgedCount += ackIds.Count;
		}

		LogPurgedDeadLetterQueue(purgedCount, deadLetterSubscription.ToString());

		return purgedCount;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_activitySource.Dispose();
		_reprocessLock.Dispose();
		GC.SuppressFinalize(this);
	}

	private static DeadLetterMessage ParseDeadLetterMessage(ReceivedMessage receivedMessage)
	{
		var attrs = receivedMessage.Message.Attributes;

		var reason = attrs.GetValueOrDefault("dlq_reason", "Unknown");
		var originalMessageId = attrs.GetValueOrDefault("dlq_original_message_id", string.Empty);
		var deliveryAttempts = 1;
		if (attrs.TryGetValue("dlq_delivery_attempts", out var attemptsStr) &&
			int.TryParse(attemptsStr, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
		{
			deliveryAttempts = parsed;
		}

		var originalSource = attrs.GetValueOrDefault("dlq_original_source", "Unknown");

		// Build a TransportMessage from the PubSub received message
		var cloudMessage = new TransportMessage
		{
			Id = originalMessageId,
			Body = receivedMessage.Message.Data.ToByteArray(),
		};

		// Copy original attributes back as properties
		foreach (var attr in attrs.Where(static a => a.Key.StartsWith("dlq_original_", StringComparison.Ordinal)))
		{
			var originalKey = attr.Key.Substring("dlq_original_".Length);
			cloudMessage.Properties[originalKey] = attr.Value;
		}

		// Store the PubSub ack ID for potential acknowledgment
		cloudMessage.Properties["pubsub_ack_id"] = receivedMessage.AckId;
		cloudMessage.Properties["pubsub_message_id"] = receivedMessage.Message.MessageId;

		var dlqMessage = new DeadLetterMessage
		{
			OriginalMessage = cloudMessage,
			Reason = reason,
			DeliveryAttempts = deliveryAttempts,
			OriginalSource = originalSource,
		};

		if (attrs.TryGetValue("dlq_timestamp", out var timestamp) &&
			DateTimeOffset.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.None, out var dlqTime))
		{
			dlqMessage.DeadLetteredAt = dlqTime;
		}
		else
		{
			dlqMessage.DeadLetteredAt = receivedMessage.Message.PublishTime.ToDateTimeOffset();
		}

		// Copy DLQ metadata
		foreach (var attr in attrs.Where(static a => a.Key.StartsWith("dlq_", StringComparison.Ordinal)))
		{
			dlqMessage.Metadata[attr.Key] = attr.Value;
		}

		return dlqMessage;
	}

	private async Task ReprocessSingleMessageAsync(
		DeadLetterMessage dlqMessage,
		ReprocessOptions options,
		CancellationToken cancellationToken)
	{
		// Build a PubSub message from the TransportMessage for publishing
		var reprocessMessage = new PubsubMessage
		{
			Data = ByteString.CopyFrom(dlqMessage.OriginalMessage.Body.Span),
			Attributes =
			{
				["reprocessed"] = "true",
				["reprocess_timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
				["original_dlq_reason"] = dlqMessage.Reason,
			},
		};

		// Apply transform if provided
		var cloudMsg = dlqMessage.OriginalMessage;
		if (options.MessageTransform != null)
		{
			cloudMsg = options.MessageTransform(cloudMsg);
			reprocessMessage.Data = ByteString.CopyFrom(cloudMsg.Body.Span);
		}

		// Determine target topic from options or by looking up subscription
		TopicName topicName;
		if (!string.IsNullOrEmpty(options.TargetQueue))
		{
			topicName = TopicName.Parse(options.TargetQueue);
		}
		else
		{
			var dlqTopic = _options.DeadLetterTopicName ??
				throw new InvalidOperationException("No target topic configured for reprocessing.");

			// Publish back to the DLQ topic's original source if possible
			topicName = dlqTopic;
		}

		var publishRequest = new PublishRequest { TopicAsTopicName = topicName, Messages = { reprocessMessage } };

		_ = await _publisherClient.PublishAsync(publishRequest, cancellationToken).ConfigureAwait(false);

		LogReprocessedMessage(dlqMessage.OriginalMessage.Id);
	}

	// Source-generated logging methods
	[LoggerMessage(GooglePubSubEventId.DeadLetterPolicyConfigured, LogLevel.Information,
		"Configured dead letter policy for {Subscription}: topic={Topic}, maxAttempts={MaxAttempts}")]
	private partial void LogConfiguredDeadLetterPolicy(string subscription, string topic, int maxAttempts);

	[LoggerMessage(GooglePubSubEventId.DeadLetterSubscriptionNotFound, LogLevel.Error,
		"Subscription {Subscription} not found")]
	private partial void LogSubscriptionNotFound(string subscription, Exception ex);

	[LoggerMessage(GooglePubSubEventId.MessageMovedToDeadLetter, LogLevel.Information,
		"Moved message {MessageId} to dead letter queue: {Reason}")]
	private partial void LogMovedToDeadLetterQueue(string messageId, string reason);

	[LoggerMessage(GooglePubSubEventId.ExceptionCausedDeadLettering, LogLevel.Error,
		"Exception that caused dead lettering")]
	private partial void LogExceptionCausedDeadLettering(Exception ex);

	[LoggerMessage(GooglePubSubEventId.DeadLetterParseFailed, LogLevel.Error,
		"Failed to parse message {MessageId}")]
	private partial void LogFailedToParse(string messageId, Exception ex);

	[LoggerMessage(GooglePubSubEventId.DeadLetterMessagesRetrieved, LogLevel.Information,
		"Retrieved {Count} messages from dead letter queue {Subscription}")]
	private partial void LogRetrievedDeadLetterMessages(int count, string subscription);

	[LoggerMessage(GooglePubSubEventId.DeadLetterReprocessFailed, LogLevel.Error,
		"Failed to reprocess message {MessageId}")]
	private partial void LogFailedToReprocessMessage(string messageId, Exception ex);

	[LoggerMessage(GooglePubSubEventId.DeadLetterMessagesReprocessed, LogLevel.Information,
		"Reprocessed {Success}/{Total} messages in {Duration}ms (skipped: {Skipped}, failed: {Failed})")]
	private partial void LogReprocessedMessages(int success, int total, long duration, int skipped, int failed);

	[LoggerMessage(GooglePubSubEventId.DeadLetterMessageReprocessed, LogLevel.Information,
		"Reprocessed message {MessageId}")]
	private partial void LogReprocessedMessage(string messageId);

	[LoggerMessage(GooglePubSubEventId.DeadLetterQueuePurged, LogLevel.Information,
		"Purged {Count} messages from dead letter subscription '{Subscription}'")]
	private partial void LogPurgedDeadLetterQueue(int count, string subscription);
}
