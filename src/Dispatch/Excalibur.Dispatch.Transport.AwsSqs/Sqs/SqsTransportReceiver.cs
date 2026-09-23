// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
internal sealed partial class SqsTransportReceiver : ITransportReceiver, ISqsVisibilityExtender
{
	// CA2213: DI-injected service - lifetime managed by DI container.
	[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
		Justification = "AWS SDK client is injected via DI and owned by the container.")]
	private readonly IAmazonSQS _sqsClient;

	private readonly ILogger _logger;
	private readonly int _waitTimeSeconds;
	private readonly int _visibilityTimeoutSeconds;
	private readonly int? _maxPayloadBytes;

	private readonly bool _hasDeadLetterQueue;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqsTransportReceiver"/> class.
	/// </summary>
	/// <param name="sqsClient">The AWS SQS client.</param>
	/// <param name="source">The SQS queue URL to consume from.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="waitTimeSeconds">Long polling wait time in seconds (0-20).</param>
	/// <param name="visibilityTimeoutSeconds">Visibility timeout for received messages.</param>
	/// <param name="maxPayloadBytes">
	/// The maximum inbound-payload length, in bytes, enforced before the body is materialized;
	/// <see langword="null"/> opts out of the size limit. Defaults to the SQS provider ceiling (256 KiB).
	/// </param>
	/// <param name="hasDeadLetterQueue">
	/// <see langword="true"/> when a dead-letter queue is configured for this queue. It decides how an
	/// oversized poison payload is settled -- see <see cref="SqsPoisonPayloadSettlement"/>. It defaults to
	/// <see langword="false"/> so that a caller who has not said otherwise keeps the loop-breaking
	/// behaviour; the registration supplies the real value.
	/// </param>
	public SqsTransportReceiver(
		IAmazonSQS sqsClient,
		string source,
		ILogger<SqsTransportReceiver> logger,
		int waitTimeSeconds = 20,
		int visibilityTimeoutSeconds = 30,
		int? maxPayloadBytes = AwsSqsTransportAdapterOptions.SqsMaxPayloadBytes,
		bool hasDeadLetterQueue = false)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_waitTimeSeconds = Math.Clamp(waitTimeSeconds, 0, 20);
		_visibilityTimeoutSeconds = Math.Clamp(visibilityTimeoutSeconds, 0, 43200);
		_maxPayloadBytes = maxPayloadBytes;
		_hasDeadLetterQueue = hasDeadLetterQueue;
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
				MessageSystemAttributeNames = ["All"],
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
				TransportReceivedMessage received;
				try
				{
					received = ConvertToReceivedMessage(sqsMessage);
				}
				catch (PayloadTooLargeException ex)
				{
					// Poison-message guard. An oversized payload can never be processed, so it is settled
					// here rather than allowed to abort the whole receive - but HOW it is settled depends
					// on whether a dead-letter queue exists to catch it.
					//
					// THIS BRANCH USED TO DELETE UNCONDITIONALLY, and its comment said "DLQ routing is
					// handled by the SQS redrive policy". That was not true of the code beneath it.
					// DeleteMessage REMOVES the message from SQS; redrive is driven by the receive count
					// of a message that becomes visible again, so a deleted message never redrives. A
					// consumer who had configured a dead-letter queue - and whose producer sent a payload
					// that is valid for the broker but larger than this consumer's MaxPayloadBytes - lost
					// accepted durable work on first receive, with no copy anywhere.
					//
					// Doing NOTHING is the correct action when a DLQ exists: letting the visibility
					// timeout expire is exactly what advances the receive count that drives redrive.
					LogPayloadTooLargeRejected(Source, Encoding.UTF8.GetByteCount(sqsMessage.Body ?? string.Empty), ex);

					if (SqsPoisonPayloadSettlement.ShouldDelete(_hasDeadLetterQueue))
					{
						// No dead-letter queue: the message has nowhere to go, and leaving it would
						// redeliver it forever and stall the queue. Dropping is the fail-safe, and it is
						// the same trade the Pub/Sub surface makes for the same reason.
						using var poisonCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
						await _sqsClient.DeleteMessageAsync(
							new DeleteMessageRequest { QueueUrl = Source, ReceiptHandle = sqsMessage.ReceiptHandle },
							poisonCts.Token).ConfigureAwait(false);
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

		var receiptHandle = GetReceiptHandle(message);

		try
		{
			var request = new DeleteMessageRequest
			{
				QueueUrl = Source,
				ReceiptHandle = receiptHandle,
			};

			// Ack must complete even during shutdown to prevent redelivery;
			// use dedicated timeout instead of caller's cancellation token
			using var ackCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await _sqsClient.DeleteMessageAsync(request, ackCts.Token).ConfigureAwait(false);
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
			// Reject must complete even during shutdown to prevent redelivery;
			// use dedicated timeout instead of caller's cancellation token
			using var rejectCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			if (requeue)
			{
				// Change visibility timeout to 0 so message becomes visible again immediately
				var request = new ChangeMessageVisibilityRequest
				{
					QueueUrl = Source,
					ReceiptHandle = receiptHandle,
					VisibilityTimeout = 0,
				};

				await _sqsClient.ChangeMessageVisibilityAsync(request, rejectCts.Token).ConfigureAwait(false);
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

				await _sqsClient.DeleteMessageAsync(request, rejectCts.Token).ConfigureAwait(false);
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
	public async Task ExtendVisibilityTimeoutAsync(
		string receiptHandle,
		TimeSpan extension,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(receiptHandle);

		var visibilityTimeoutSeconds = (int)Math.Clamp(extension.TotalSeconds, 0, 43200);

		try
		{
			var request = new ChangeMessageVisibilityRequest
			{
				QueueUrl = Source,
				ReceiptHandle = receiptHandle,
				VisibilityTimeout = visibilityTimeoutSeconds,
			};

			await _sqsClient.ChangeMessageVisibilityAsync(request, cancellationToken)
				.ConfigureAwait(false);
			LogVisibilityExtended(receiptHandle[..Math.Min(receiptHandle.Length, 20)], Source, visibilityTimeoutSeconds);
		}
		catch (Exception ex)
		{
			LogVisibilityExtendError(receiptHandle[..Math.Min(receiptHandle.Length, 20)], Source, ex);
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

		if (serviceType == typeof(ISqsVisibilityExtender))
		{
			return this;
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

	private TransportReceivedMessage ConvertToReceivedMessage(Message sqsMessage)
	{
		// Defense-in-depth DoS guard: reject an oversized payload BEFORE materializing the body
		// (Encoding.UTF8.GetBytes below). The raw SQS wire length is the UTF-8 byte count of the message
		// Body string; GetByteCount measures without allocating. Fail-closed — throws
		// PayloadTooLargeException, which the receive loop catches to delete the poison message.
		PayloadSizeGuard.EnsureWithinLimit(Encoding.UTF8.GetByteCount(sqsMessage.Body ?? string.Empty), _maxPayloadBytes);

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

		// TransportReceivedMessage.Source is the queue this message was received from, not the message's
		// own identity -- that is Id, and the receipt handle in ProviderData settles it. Reporting the
		// message ID here made every message from one queue claim a different source.
		var sourceQueue = Source;

		return new TransportReceivedMessage
		{
			Id = sqsMessage.MessageId,
			Body = AwsSqsMessageBodyCodec.DecodeBody(sqsMessage),
			ContentType = contentType,
			MessageType = messageType,
			CorrelationId = correlationId,
			DeliveryCount = deliveryCount,
			EnqueuedAt = enqueuedAt,
			Source = sourceQueue,
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

	[LoggerMessage(AwsSqsEventId.TransportReceiverVisibilityExtended, LogLevel.Debug,
		"SQS visibility timeout extended for receipt {ReceiptHandle} on {Source} to {TimeoutSeconds}s")]
	private partial void LogVisibilityExtended(string receiptHandle, string source, int timeoutSeconds);

	[LoggerMessage(AwsSqsEventId.TransportReceiverVisibilityExtendError, LogLevel.Error,
		"Failed to extend visibility timeout for receipt {ReceiptHandle} on {Source}")]
	private partial void LogVisibilityExtendError(string receiptHandle, string source, Exception exception);

	[LoggerMessage(AwsSqsEventId.TransportReceiverDisposed, LogLevel.Debug,
		"SQS transport receiver disposed for {Source}")]
	private partial void LogDisposed(string source);

	[LoggerMessage(AwsSqsEventId.TransportReceiverPayloadTooLarge, LogLevel.Warning,
		"SQS transport receiver: rejected an oversized inbound payload ({PayloadBytes} bytes) from {Source} before materialization (deleted; dead-lettered if a redrive policy is configured)")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);
}
