// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
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
/// </para>
/// <para>
/// Provider-specific data stored in <see cref="TransportReceivedMessage.ProviderData"/>:
/// <c>"aws.receipt_handle"</c> and <c>"aws.message_id"</c>.
/// </para>
/// </remarks>
internal sealed partial class SqsTransportSubscriber : ITransportSubscriber
{
	// CA2213: DI-injected service - lifetime managed by DI container.
	[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
		Justification = "AWS SDK client is injected via DI and owned by the container.")]
	private readonly IAmazonSQS _sqsClient;

	private readonly string _queueUrl;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqsTransportSubscriber"/> class.
	/// </summary>
	/// <param name="sqsClient">The AWS SQS client.</param>
	/// <param name="source">The logical source name for this subscriber.</param>
	/// <param name="queueUrl">The SQS queue URL to consume from.</param>
	/// <param name="logger">The logger instance.</param>
	public SqsTransportSubscriber(
		IAmazonSQS sqsClient,
		string source,
		string queueUrl,
		ILogger<SqsTransportSubscriber> logger)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
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
					AttributeNames = ["All"],
				};

				var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken)
					.ConfigureAwait(false);

				if (response.Messages is null || response.Messages.Count == 0)
				{
					continue;
				}

				foreach (var sqsMessage in response.Messages)
				{
					var received = ConvertToReceivedMessage(sqsMessage);
					LogMessageReceived(received.Id, Source);

					try
					{
						var action = await handler(received, cancellationToken).ConfigureAwait(false);

						switch (action)
						{
							case MessageAction.Acknowledge:
								await _sqsClient.DeleteMessageAsync(
									new DeleteMessageRequest
									{
										QueueUrl = _queueUrl,
										ReceiptHandle = sqsMessage.ReceiptHandle,
									},
									cancellationToken).ConfigureAwait(false);
								LogMessageAcknowledged(received.Id, Source);
								break;

							case MessageAction.Reject:
								// Delete the message; DLQ routing is handled by the SQS redrive policy
								await _sqsClient.DeleteMessageAsync(
									new DeleteMessageRequest
									{
										QueueUrl = _queueUrl,
										ReceiptHandle = sqsMessage.ReceiptHandle,
									},
									cancellationToken).ConfigureAwait(false);
								LogMessageRejected(received.Id, Source);
								break;

							case MessageAction.Requeue:
								// Change visibility timeout to 0 so message becomes visible again immediately
								await _sqsClient.ChangeMessageVisibilityAsync(
									new ChangeMessageVisibilityRequest
									{
										QueueUrl = _queueUrl,
										ReceiptHandle = sqsMessage.ReceiptHandle,
										VisibilityTimeout = 0,
									},
									cancellationToken).ConfigureAwait(false);
								LogMessageRequeued(received.Id, Source);
								break;
						}
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception ex)
					{
						LogError(received.Id, Source, ex);
						// Change visibility to 0 for retry
						try
						{
							await _sqsClient.ChangeMessageVisibilityAsync(
								new ChangeMessageVisibilityRequest
								{
									QueueUrl = _queueUrl,
									ReceiptHandle = sqsMessage.ReceiptHandle,
									VisibilityTimeout = 0,
								},
								cancellationToken).ConfigureAwait(false);
						}
						catch (Exception visEx)
						{
							LogError(received.Id, Source, visEx);
						}
					}
				}
			}
		}
		catch (OperationCanceledException)
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

	private static TransportReceivedMessage ConvertToReceivedMessage(Message sqsMessage)
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
		var correlationId = properties.TryGetValue("correlation-id", out var cid) ? cid as string : null;
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
			Source = sqsMessage.MessageId,
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

	[LoggerMessage(AwsSqsEventId.TransportSubscriberStopped, LogLevel.Information,
		"SQS transport subscriber: subscription stopped for {Source}")]
	private partial void LogSubscriptionStopped(string source);

	[LoggerMessage(AwsSqsEventId.TransportSubscriberDisposed, LogLevel.Debug,
		"SQS transport subscriber disposed for {Source}")]
	private partial void LogDisposed(string source);
}
