// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SQS implementation of <see cref="ITransportSubscriber"/>.
/// Uses <see cref="IAmazonSQS"/> with long-polling in a continuous loop for push-based message delivery.
/// </summary>
/// <remarks>
/// <para>
/// Message settlement is determined by the <see cref="MessageAction"/> returned from the handler:
/// <list type="bullet">
/// <item><see cref="MessageAction.Acknowledge"/> deletes the message from the queue.</item>
/// <item><see cref="MessageAction.Reject"/> deletes the message (SQS routes to DLQ after max receives via redrive policy).</item>
/// <item><see cref="MessageAction.Requeue"/> changes visibility timeout to 0 for immediate redelivery.</item>
/// </list>
/// Settlement is applied per receive batch using <c>DeleteMessageBatch</c> and
/// <c>ChangeMessageVisibilityBatch</c> so a full long-poll batch costs at most two settlement
/// API calls instead of one call per message.
/// </para>
/// <para>
/// When the visibility-timeout heartbeat is enabled, the subscriber extends the in-flight message's
/// visibility while a long-running handler executes, preventing redelivery before the handler completes.
/// </para>
/// <para>
/// Provider-specific data stored in <see cref="TransportReceivedMessage.ProviderData"/>:
/// <c>"aws.receipt_handle"</c> and <c>"aws.message_id"</c>.
/// </para>
/// </remarks>
internal sealed partial class SqsTransportSubscriber : ITransportSubscriber
{
	/// <summary>The largest visibility timeout AWS SQS accepts, in seconds (12 hours).</summary>
	private const int MaxVisibilityTimeoutSeconds = 43200;

	// CA2213: DI-injected service - lifetime managed by DI container.
	[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
		Justification = "AWS SDK client is injected via DI and owned by the container.")]
	private readonly IAmazonSQS _sqsClient;

	private readonly string _queueUrl;
	private readonly AwsSqsVisibilityHeartbeatOptions _heartbeat;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqsTransportSubscriber"/> class.
	/// </summary>
	/// <param name="sqsClient">The AWS SQS client.</param>
	/// <param name="source">The logical source name for this subscriber.</param>
	/// <param name="queueUrl">The SQS queue URL to consume from.</param>
	/// <param name="heartbeatOptions">The in-flight visibility-timeout heartbeat options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqsTransportSubscriber(
		IAmazonSQS sqsClient,
		string source,
		string queueUrl,
		AwsSqsVisibilityHeartbeatOptions heartbeatOptions,
		ILogger<SqsTransportSubscriber> logger)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
		_heartbeat = heartbeatOptions ?? throw new ArgumentNullException(nameof(heartbeatOptions));
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

		LogSubscriptionStarted(Source);

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var request = new ReceiveMessageRequest
				{
					QueueUrl = _queueUrl,
					MaxNumberOfMessages = 10,
					WaitTimeSeconds = 20,
					MessageAttributeNames = ["All"],
					MessageSystemAttributeNames = ["All"],
				};

				var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken)
					.ConfigureAwait(false);

				if (response.Messages is null || response.Messages.Count == 0)
				{
					continue;
				}

				// Collect settlement actions for the whole receive batch, then apply them with at most
				// one DeleteMessageBatch + one ChangeMessageVisibilityBatch call.
				var deleteEntries = new List<DeleteMessageBatchRequestEntry>();
				var requeueEntries = new List<ChangeMessageVisibilityBatchRequestEntry>();

				for (var i = 0; i < response.Messages.Count; i++)
				{
					var sqsMessage = response.Messages[i];
					var received = ConvertToReceivedMessage(sqsMessage);
					LogMessageReceived(received.Id, Source);

					var entryId = i.ToString(CultureInfo.InvariantCulture);

					try
					{
						var action = await InvokeHandlerAsync(
							handler, received, sqsMessage.ReceiptHandle, cancellationToken).ConfigureAwait(false);

						switch (action)
						{
							case MessageAction.Acknowledge:
								deleteEntries.Add(new DeleteMessageBatchRequestEntry
								{
									Id = entryId,
									ReceiptHandle = sqsMessage.ReceiptHandle,
								});
								LogMessageAcknowledged(received.Id, Source);
								break;

							case MessageAction.Reject:
								// Delete the message; DLQ routing is handled by the SQS redrive policy.
								deleteEntries.Add(new DeleteMessageBatchRequestEntry
								{
									Id = entryId,
									ReceiptHandle = sqsMessage.ReceiptHandle,
								});
								LogMessageRejected(received.Id, Source);
								break;

							case MessageAction.Requeue:
								requeueEntries.Add(new ChangeMessageVisibilityBatchRequestEntry
								{
									Id = entryId,
									ReceiptHandle = sqsMessage.ReceiptHandle,
									VisibilityTimeout = 0,
								});
								LogMessageRequeued(received.Id, Source);
								break;
						}
					}
					catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
					{
						throw;
					}
					catch (Exception ex)
					{
						LogError(received.Id, Source, ex);

						// Make the message visible again immediately for retry.
						requeueEntries.Add(new ChangeMessageVisibilityBatchRequestEntry
						{
							Id = entryId,
							ReceiptHandle = sqsMessage.ReceiptHandle,
							VisibilityTimeout = 0,
						});
					}
				}

				await SettleBatchAsync(deleteEntries, requeueEntries, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			// Expected on cancellation — fall through to stop
		}

		LogSubscriptionStopped(Source);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(IAmazonSQS))
		{
			return _sqsClient;
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
	/// Invokes the handler, optionally running a visibility-timeout heartbeat alongside it so a
	/// long-running handler does not have its message redelivered before it completes.
	/// </summary>
	private async Task<MessageAction> InvokeHandlerAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		TransportReceivedMessage received,
		string receiptHandle,
		CancellationToken cancellationToken)
	{
		if (!_heartbeat.Enabled)
		{
			return await handler(received, cancellationToken).ConfigureAwait(false);
		}

		using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var heartbeatTask = RunVisibilityHeartbeatAsync(received.Id, receiptHandle, heartbeatCts.Token);

		try
		{
			return await handler(received, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await heartbeatCts.CancelAsync().ConfigureAwait(false);
			try
			{
				await heartbeatTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected: the heartbeat loop is cancelled once the handler settles.
			}
		}
	}

	/// <summary>
	/// Periodically extends the in-flight message's visibility timeout until the handler settles,
	/// cancellation is requested, or the configured maximum extension budget is exhausted.
	/// </summary>
	private async Task RunVisibilityHeartbeatAsync(
		string messageId,
		string receiptHandle,
		CancellationToken cancellationToken)
	{
		var visibilitySeconds = (int)Math.Clamp(
			_heartbeat.VisibilityTimeout.TotalSeconds, 0, MaxVisibilityTimeoutSeconds);
		var deadline = DateTimeOffset.UtcNow + _heartbeat.MaxExtension;

		while (!cancellationToken.IsCancellationRequested && DateTimeOffset.UtcNow < deadline)
		{
			await Task.Delay(_heartbeat.Interval, cancellationToken).ConfigureAwait(false);

			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			try
			{
				await _sqsClient.ChangeMessageVisibilityAsync(
					new ChangeMessageVisibilityRequest
					{
						QueueUrl = _queueUrl,
						ReceiptHandle = receiptHandle,
						VisibilityTimeout = visibilitySeconds,
					},
					cancellationToken).ConfigureAwait(false);
				LogVisibilityExtended(messageId, Source, visibilitySeconds);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				// A failed extension is not fatal; stop heartbeating and let normal redelivery apply.
				LogVisibilityExtendFailed(messageId, Source, ex);
				break;
			}
		}
	}

	/// <summary>
	/// Applies the collected settlement actions for a receive batch using batch SQS operations.
	/// </summary>
	private async Task SettleBatchAsync(
		List<DeleteMessageBatchRequestEntry> deleteEntries,
		List<ChangeMessageVisibilityBatchRequestEntry> requeueEntries,
		CancellationToken cancellationToken)
	{
		if (deleteEntries.Count > 0)
		{
			try
			{
				var deleteResponse = await _sqsClient.DeleteMessageBatchAsync(
					new DeleteMessageBatchRequest { QueueUrl = _queueUrl, Entries = deleteEntries },
					cancellationToken).ConfigureAwait(false);

				if (deleteResponse.Failed is { Count: > 0 })
				{
					foreach (var failure in deleteResponse.Failed)
					{
						LogBatchDeleteFailed(failure.Id, failure.Code, Source);
					}
				}
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				LogBatchDeleteError(Source, ex);
			}
		}

		if (requeueEntries.Count > 0)
		{
			try
			{
				_ = await _sqsClient.ChangeMessageVisibilityBatchAsync(
					new ChangeMessageVisibilityBatchRequest { QueueUrl = _queueUrl, Entries = requeueEntries },
					cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				LogBatchVisibilityError(Source, ex);
			}
		}
	}

	private TransportReceivedMessage ConvertToReceivedMessage(Message sqsMessage)
	{
		var properties = new Dictionary<string, object>(StringComparer.Ordinal);

		if (sqsMessage.MessageAttributes is { Count: > 0 })
		{
			foreach (var attr in sqsMessage.MessageAttributes)
			{
				properties[attr.Key] = attr.Value.StringValue ?? string.Empty;
			}
		}

		// Add SQS system attributes
		if (sqsMessage.Attributes is { Count: > 0 })
		{
			foreach (var attr in sqsMessage.Attributes)
			{
				properties[$"sqs.{attr.Key}"] = attr.Value;
			}
		}

		var contentType = properties.TryGetValue("content-type", out var ct) ? ct as string : null;
		var correlationId = properties.TryGetValue(OutboxHeaderNames.CorrelationId, out var cid) ? cid as string : null;
		var messageType = properties.TryGetValue("message-type", out var mt) ? mt as string : null;

		// Determine delivery count from ApproximateReceiveCount
		var deliveryCount = 1;
		if (sqsMessage.Attributes?.TryGetValue("ApproximateReceiveCount", out var receiveCountStr) == true &&
			int.TryParse(receiveCountStr, out var receiveCount))
		{
			deliveryCount = receiveCount;
		}

		// Determine enqueued time from SentTimestamp
		var enqueuedAt = DateTimeOffset.UtcNow;
		if (sqsMessage.Attributes?.TryGetValue("SentTimestamp", out var sentTimestampStr) == true &&
			long.TryParse(sentTimestampStr, out var sentTimestampMs))
		{
			enqueuedAt = DateTimeOffset.FromUnixTimeMilliseconds(sentTimestampMs);
		}

		return new TransportReceivedMessage
		{
			Id = sqsMessage.MessageId,
			Body = Encoding.UTF8.GetBytes(sqsMessage.Body ?? string.Empty),
			ContentType = contentType,
			MessageType = messageType,
			CorrelationId = correlationId,
			DeliveryCount = deliveryCount,
			EnqueuedAt = enqueuedAt,
			Source = _queueUrl,
			MessageGroupId = sqsMessage.Attributes?.TryGetValue("MessageGroupId", out var groupId) == true ? groupId : null,
			Properties = properties,
			ProviderData = new Dictionary<string, object>
			{
				["aws.receipt_handle"] = sqsMessage.ReceiptHandle,
				["aws.message_id"] = sqsMessage.MessageId,
			},
		};
	}

	[LoggerMessage(AwsSqsEventId.TransportSubscriberStarted, LogLevel.Information,
		"SQS transport subscriber: subscription started for {Source}")]
	private partial void LogSubscriptionStarted(string source);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberMessageReceived, LogLevel.Debug,
		"SQS transport subscriber: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberMessageAcknowledged, LogLevel.Debug,
		"SQS transport subscriber: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberMessageRejected, LogLevel.Warning,
		"SQS transport subscriber: message {MessageId} rejected from {Source}")]
	private partial void LogMessageRejected(string messageId, string source);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberMessageRequeued, LogLevel.Debug,
		"SQS transport subscriber: message {MessageId} requeued from {Source}")]
	private partial void LogMessageRequeued(string messageId, string source);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberError, LogLevel.Error,
		"SQS transport subscriber: error processing message {MessageId} from {Source}")]
	private partial void LogError(string messageId, string source, Exception exception);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberBatchDeleteFailed, LogLevel.Warning,
		"SQS transport subscriber: batch delete failed for entry {EntryId} ({Code}) from {Source}")]
	private partial void LogBatchDeleteFailed(string entryId, string code, string source);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberBatchDeleteError, LogLevel.Error,
		"SQS transport subscriber: batch delete error for {Source}")]
	private partial void LogBatchDeleteError(string source, Exception exception);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberBatchVisibilityError, LogLevel.Error,
		"SQS transport subscriber: batch visibility change error for {Source}")]
	private partial void LogBatchVisibilityError(string source, Exception exception);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberVisibilityExtended, LogLevel.Debug,
		"SQS transport subscriber: extended visibility of message {MessageId} from {Source} by {Seconds}s")]
	private partial void LogVisibilityExtended(string messageId, string source, int seconds);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberVisibilityExtendFailed, LogLevel.Warning,
		"SQS transport subscriber: failed to extend visibility of message {MessageId} from {Source}")]
	private partial void LogVisibilityExtendFailed(string messageId, string source, Exception exception);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberStopped, LogLevel.Information,
		"SQS transport subscriber: subscription stopped for {Source}")]
	private partial void LogSubscriptionStopped(string source);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberDisposed, LogLevel.Debug,
		"SQS transport subscriber disposed for {Source}")]
	private partial void LogDisposed(string source);
}
