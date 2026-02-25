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
/// AWS SQS implementation of <see cref="ITransportReceiver"/>.
/// Uses <see cref="IAmazonSQS"/> for native message consumption.
/// </summary>
/// <remarks>
/// Acknowledgment uses <c>DeleteMessage</c> via the receipt handle stored in
/// <see cref="TransportReceivedMessage.ProviderData"/> as <c>"sqs.receipt_handle"</c>.
/// Rejection with requeue uses <c>ChangeMessageVisibility</c> to set visibility timeout to 0.
/// </remarks>
internal sealed partial class SqsTransportReceiver : ITransportReceiver
{
	// CA2213: DI-injected service - lifetime managed by DI container.
	[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
		Justification = "AWS SDK client is injected via DI and owned by the container.")]
	private readonly IAmazonSQS _sqsClient;

	private readonly ILogger _logger;
	private readonly int _waitTimeSeconds;
	private readonly int _visibilityTimeoutSeconds;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqsTransportReceiver"/> class.
	/// </summary>
	/// <param name="sqsClient">The AWS SQS client.</param>
	/// <param name="source">The SQS queue URL to consume from.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="waitTimeSeconds">Long polling wait time in seconds (0-20).</param>
	/// <param name="visibilityTimeoutSeconds">Visibility timeout for received messages.</param>
	public SqsTransportReceiver(
		IAmazonSQS sqsClient,
		string source,
		ILogger<SqsTransportReceiver> logger,
		int waitTimeSeconds = 20,
		int visibilityTimeoutSeconds = 30)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_waitTimeSeconds = Math.Clamp(waitTimeSeconds, 0, 20);
		_visibilityTimeoutSeconds = Math.Clamp(visibilityTimeoutSeconds, 0, 43200);
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		try
		{
			var request = new ReceiveMessageRequest
			{
				QueueUrl = Source,
				MaxNumberOfMessages = Math.Clamp(maxMessages, 1, 10),
				WaitTimeSeconds = _waitTimeSeconds,
				VisibilityTimeout = _visibilityTimeoutSeconds,
				MessageAttributeNames = ["All"],
				AttributeNames = ["All"],
			};

			var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken)
				.ConfigureAwait(false);

			if (response.Messages is null || response.Messages.Count == 0)
			{
				return [];
			}

			var messages = new List<TransportReceivedMessage>(response.Messages.Count);
			foreach (var sqsMessage in response.Messages)
			{
				var received = ConvertToReceivedMessage(sqsMessage);
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
			var request = new DeleteMessageRequest
			{
				QueueUrl = Source,
				ReceiptHandle = receiptHandle,
			};

			await _sqsClient.DeleteMessageAsync(request, cancellationToken).ConfigureAwait(false);
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

		var receiptHandle = GetReceiptHandle(message);

		try
		{
			if (requeue)
			{
				// Change visibility timeout to 0 so message becomes visible again immediately
				var request = new ChangeMessageVisibilityRequest
				{
					QueueUrl = Source,
					ReceiptHandle = receiptHandle,
					VisibilityTimeout = 0,
				};

				await _sqsClient.ChangeMessageVisibilityAsync(request, cancellationToken).ConfigureAwait(false);
				LogMessageRejectedRequeue(message.Id, Source, reason ?? "no reason");
			}
			else
			{
				// Delete the message; DLQ routing is handled by the decorator or SQS redrive policy
				var request = new DeleteMessageRequest
				{
					QueueUrl = Source,
					ReceiptHandle = receiptHandle,
				};

				await _sqsClient.DeleteMessageAsync(request, cancellationToken).ConfigureAwait(false);
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
				["sqs.receipt_handle"] = sqsMessage.ReceiptHandle,
				["sqs.message_id"] = sqsMessage.MessageId,
			},
		};
	}

	private static string GetReceiptHandle(TransportReceivedMessage message)
	{
		if (message.ProviderData.TryGetValue("sqs.receipt_handle", out var handle) && handle is string handleStr)
		{
			return handleStr;
		}

		throw new InvalidOperationException("Message does not contain an SQS receipt handle in ProviderData.");
	}

	[LoggerMessage(AwsSqsEventId.TransportReceiverMessageReceived, LogLevel.Debug,
		"SQS transport receiver: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(AwsSqsEventId.TransportReceiverReceiveError, LogLevel.Error,
		"SQS transport receiver: failed to receive messages from {Source}")]
	private partial void LogReceiveError(string source, Exception exception);

	[LoggerMessage(AwsSqsEventId.TransportReceiverMessageAcknowledged, LogLevel.Debug,
		"SQS transport receiver: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(AwsSqsEventId.TransportReceiverAcknowledgeError, LogLevel.Error,
		"SQS transport receiver: failed to acknowledge message {MessageId} from {Source}")]
	private partial void LogAcknowledgeError(string messageId, string source, Exception exception);

	[LoggerMessage(AwsSqsEventId.TransportReceiverMessageRejected, LogLevel.Warning,
		"SQS transport receiver: message {MessageId} rejected from {Source}: {Reason}")]
	private partial void LogMessageRejected(string messageId, string source, string reason);

	[LoggerMessage(AwsSqsEventId.TransportReceiverMessageRejectedRequeue, LogLevel.Debug,
		"SQS transport receiver: message {MessageId} rejected (requeue) from {Source}: {Reason}")]
	private partial void LogMessageRejectedRequeue(string messageId, string source, string reason);

	[LoggerMessage(AwsSqsEventId.TransportReceiverRejectError, LogLevel.Error,
		"SQS transport receiver: failed to reject message {MessageId} from {Source}")]
	private partial void LogRejectError(string messageId, string source, Exception exception);

	[LoggerMessage(AwsSqsEventId.TransportReceiverDisposed, LogLevel.Debug,
		"SQS transport receiver disposed for {Source}")]
	private partial void LogDisposed(string source);
}
