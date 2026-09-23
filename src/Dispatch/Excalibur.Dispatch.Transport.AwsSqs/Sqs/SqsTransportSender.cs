// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Transport.AwsSqs;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SQS implementation of <see cref="ITransportSender"/>.
/// Uses <see cref="IAmazonSQS"/> for native message production.
/// </summary>
/// <remarks>
/// <para>
/// Reads well-known property keys from <see cref="TransportMessage.Properties"/>:
/// </para>
/// <list type="bullet">
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.OrderingKey"/> maps to <c>MessageGroupId</c> (FIFO queues).</item>
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.DeduplicationId"/> maps to <c>MessageDeduplicationId</c> (FIFO queues).</item>
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.DelaySeconds"/> maps to <c>DelaySeconds</c>.</item>
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.ScheduledTime"/> maps to <c>DelaySeconds</c> (computed).</item>
/// </list>
/// </remarks>
internal sealed partial class SqsTransportSender : ITransportSender
{
	private const int SqsBatchLimit = 10;

	// CA2213: DI-injected service - lifetime managed by DI container.
	[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
		Justification = "AWS SDK client is injected via DI and owned by the container.")]
	private readonly IAmazonSQS _sqsClient;

	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqsTransportSender"/> class.
	/// </summary>
	/// <param name="sqsClient">The AWS SQS client.</param>
	/// <param name="destination">The SQS queue URL.</param>
	/// <param name="logger">The logger instance.</param>
	public SqsTransportSender(
		IAmazonSQS sqsClient,
		string destination,
		ILogger<SqsTransportSender> logger)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		Destination = destination ?? throw new ArgumentNullException(nameof(destination));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Destination { get; }

	/// <inheritdoc />
	public async Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		try
		{
			var request = CreateSendRequest(message);

			var response = await _sqsClient.SendMessageAsync(request, cancellationToken)
				.ConfigureAwait(false);

			LogMessageSent(message.Id, Destination);

			return new SendResult
			{
				IsSuccess = true,
				MessageId = response.MessageId,
				SequenceNumber = ParseSequenceNumber(response.SequenceNumber),
				AcceptedAt = DateTimeOffset.UtcNow,
			};
		}
		catch (Exception ex)
		{
			LogSendFailed(message.Id, Destination, ex);
			return SendResult.Failure(SendError.FromException(ex, IsTransient(ex)));
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// The returned <see cref="BatchSendResult.Results"/> is positional: element <c>i</c> is the result
	/// for <c>messages[i]</c>, and there is exactly one element per input. A caller recovering from a
	/// partial failure can therefore retry precisely the inputs that failed.
	/// </remarks>
	public async Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);

		if (messages.Count == 0)
		{
			return new BatchSendResult { TotalMessages = 0, SuccessCount = 0, FailureCount = 0 };
		}

		var stopwatch = ValueStopwatch.StartNew();

		// Results are held positionally: slot i is the result for messages[i]. SQS returns the entry ID
		// we issued in both the Successful and Failed arrays, so each result is placed against the input
		// it came from rather than appended in arrival order. Appending would order the batch as all
		// successes then all failures, which leaves a caller recovering from a partial failure with no
		// way to tell which inputs to retry -- and on an at-least-once transport, retrying the whole
		// batch to be safe manufactures duplicates of messages that were already delivered.
		var results = new SendResult?[messages.Count];
		Exception? batchException = null;

		try
		{
			for (var chunkStart = 0; chunkStart < messages.Count; chunkStart += SqsBatchLimit)
			{
				var chunkEnd = Math.Min(chunkStart + SqsBatchLimit, messages.Count);
				var entries = new List<SendMessageBatchRequestEntry>(chunkEnd - chunkStart);

				for (var i = chunkStart; i < chunkEnd; i++)
				{
					entries.Add(CreateBatchEntry(messages[i], i));
				}

				var batchRequest = new SendMessageBatchRequest
				{
					QueueUrl = Destination,
					Entries = entries,
				};

				var batchResponse = await _sqsClient.SendMessageBatchAsync(batchRequest, cancellationToken)
					.ConfigureAwait(false);

				foreach (var success in batchResponse.Successful)
				{
					if (TryResolveInputIndex(success.Id, chunkStart, chunkEnd, out var index))
					{
						results[index] = new SendResult
						{
							IsSuccess = true,
							MessageId = success.MessageId,
							SequenceNumber = ParseSequenceNumber(success.SequenceNumber),
							AcceptedAt = DateTimeOffset.UtcNow,
						};
					}
					else
					{
						LogUnresolvedBatchEntry(Destination, success.Id ?? string.Empty);
					}
				}

				foreach (var failure in batchResponse.Failed)
				{
					if (TryResolveInputIndex(failure.Id, chunkStart, chunkEnd, out var index))
					{
						results[index] = new SendResult
						{
							IsSuccess = false,
							Error = new SendError
							{
								Code = failure.Code,
								Message = failure.Message,
								IsRetryable = failure.SenderFault != true,
							},
						};
					}
					else
					{
						LogUnresolvedBatchEntry(Destination, failure.Id ?? string.Empty);
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogBatchSendFailed(Destination, messages.Count, ex);
			batchException = ex;
		}

		// Every input gets exactly one result. A slot still empty here is an input the broker returned
		// no per-entry result for -- either because the call failed before reaching it, or because the
		// response omitted it. Its outcome is unknown, so it is reported as a retryable failure: on an
		// at-least-once transport a duplicate is recoverable and a silent loss is not.
		var successCount = 0;
		var finalResults = new SendResult[messages.Count];
		for (var i = 0; i < finalResults.Length; i++)
		{
			finalResults[i] = results[i] ?? (batchException is not null
				? SendResult.Failure(SendError.FromException(batchException, IsTransient(batchException)))
				: SendResult.Failure(new SendError
				{
					Code = "NoBatchResult",
					Message = "SQS returned no result entry for this message; its delivery status is unknown.",
					IsRetryable = true,
				}));

			if (finalResults[i].IsSuccess)
			{
				successCount++;
			}
		}

		LogBatchSent(Destination, messages.Count, successCount);

		return new BatchSendResult
		{
			TotalMessages = messages.Count,
			SuccessCount = successCount,
			FailureCount = messages.Count - successCount,
			Results = finalResults,
			Duration = stopwatch.Elapsed,
		};
	}

	/// <inheritdoc />
	public Task FlushAsync(CancellationToken cancellationToken)
	{
		// SQS sends are immediately committed; no buffering to flush.
		return Task.CompletedTask;
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
		LogDisposed(Destination);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Builds a single SQS request from a <see cref="TransportMessage"/>. Both the single-send and the
	/// batch path go through here, so a field can only reach the wire on one path if it is deleted from
	/// the other -- the two paths previously carried separate mappings and the batch copy fell behind.
	/// </summary>
	private static SqsOutboundMapping MapMessage(TransportMessage message)
	{
		var body = AwsSqsMessageBodyCodec.EncodeBody(message.Body.Span, out var isBase64Body);

		string? messageGroupId = null;
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.OrderingKey, out var orderingKey) &&
			orderingKey is string orderingKeyStr)
		{
			messageGroupId = orderingKeyStr;
		}

		string? messageDeduplicationId = null;
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.DeduplicationId, out var dedupId) &&
			dedupId is string dedupIdStr)
		{
			messageDeduplicationId = dedupIdStr;
		}

		Dictionary<string, MessageAttributeValue>? attributes = null;

		// Copy custom properties as message attributes.
		if (message.Properties.Count > 0)
		{
			attributes = new Dictionary<string, MessageAttributeValue>(
				message.Properties.Count, StringComparer.Ordinal);

			foreach (var (key, value) in message.Properties)
			{
				// "dispatch."-prefixed keys are transport hints consumed above, not consumer metadata.
				// The body-encoding attribute is written authoritatively below: a value that survived a
				// previous round trip in Properties would otherwise mislabel this body.
				if (key.StartsWith("dispatch.", StringComparison.Ordinal) ||
					string.Equals(key, AwsSqsMessageAttributes.BodyEncoding, StringComparison.Ordinal))
				{
					continue;
				}

				attributes[key] = StringAttribute(value?.ToString() ?? string.Empty);
			}
		}

		// Message metadata, written last so it wins over a same-named custom property.
		AddIfPresent(ref attributes, "content-type", message.ContentType);
		AddIfPresent(ref attributes, OutboxHeaderNames.CorrelationId, message.CorrelationId);
		AddIfPresent(ref attributes, OutboxHeaderNames.CausationId, message.CausationId);
		AddIfPresent(ref attributes, "message-type", message.MessageType);

		if (isBase64Body)
		{
			AddIfPresent(ref attributes, AwsSqsMessageAttributes.BodyEncoding, AwsSqsMessageAttributes.BodyEncodingBase64);
		}

		return new SqsOutboundMapping
		{
			Body = body,
			MessageGroupId = messageGroupId,
			MessageDeduplicationId = messageDeduplicationId,
			DelaySeconds = ComputeDelaySeconds(message),
			MessageAttributes = attributes,
		};
	}

	private static void AddIfPresent(
		ref Dictionary<string, MessageAttributeValue>? attributes,
		string name,
		string? value)
	{
		if (value is null)
		{
			return;
		}

		attributes ??= new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);
		attributes[name] = StringAttribute(value);
	}

	private static MessageAttributeValue StringAttribute(string value) =>
		new() { DataType = AwsSqsMessageAttributes.StringDataType, StringValue = value };

	private static int? ComputeDelaySeconds(TransportMessage message)
	{
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.DelaySeconds, out var delayObj) &&
			delayObj is string delayStr && int.TryParse(delayStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delaySec))
		{
			return Math.Clamp(delaySec, 0, 900);
		}

		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.ScheduledTime, out var scheduledObj) &&
			scheduledObj is string scheduledStr &&
			DateTimeOffset.TryParse(scheduledStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var scheduledTime))
		{
			var computed = (int)Math.Ceiling((scheduledTime - DateTimeOffset.UtcNow).TotalSeconds);
			if (computed > 0)
			{
				return Math.Clamp(computed, 0, 900);
			}
		}

		return null;
	}

	private SendMessageRequest CreateSendRequest(TransportMessage message)
	{
		var mapping = MapMessage(message);

		var request = new SendMessageRequest
		{
			QueueUrl = Destination,
			MessageBody = mapping.Body,
		};

		if (mapping.MessageGroupId is not null)
		{
			request.MessageGroupId = mapping.MessageGroupId;
		}

		if (mapping.MessageDeduplicationId is not null)
		{
			request.MessageDeduplicationId = mapping.MessageDeduplicationId;
		}

		if (mapping.DelaySeconds is { } delaySeconds)
		{
			request.DelaySeconds = delaySeconds;
		}

		if (mapping.MessageAttributes is not null)
		{
			request.MessageAttributes = mapping.MessageAttributes;
		}

		return request;
	}

	private static SendMessageBatchRequestEntry CreateBatchEntry(TransportMessage message, int index)
	{
		var mapping = MapMessage(message);

		var entry = new SendMessageBatchRequestEntry
		{
			Id = index.ToString(CultureInfo.InvariantCulture),
			MessageBody = mapping.Body,
		};

		if (mapping.MessageGroupId is not null)
		{
			entry.MessageGroupId = mapping.MessageGroupId;
		}

		if (mapping.MessageDeduplicationId is not null)
		{
			entry.MessageDeduplicationId = mapping.MessageDeduplicationId;
		}

		if (mapping.DelaySeconds is { } delaySeconds)
		{
			entry.DelaySeconds = delaySeconds;
		}

		if (mapping.MessageAttributes is not null)
		{
			entry.MessageAttributes = mapping.MessageAttributes;
		}

		return entry;
	}

	/// <summary>
	/// Reads the broker's sequence number without letting it turn an accepted message into a failure.
	/// </summary>
	/// <remarks>
	/// SQS defines <c>SequenceNumber</c> as a 128-bit decimal string and observed FIFO values exceed
	/// <see cref="long.MaxValue"/>, so the value does not always fit the <see cref="SendResult"/>
	/// contract's <see cref="long"/>. The sequence number is optional metadata reported after the broker
	/// has already accepted the message; a value that does not fit is dropped rather than allowed to
	/// report a delivered message as failed, which would make a correctly-written caller retry it.
	/// </remarks>
	private static long? ParseSequenceNumber(string? sequenceNumber) =>
		!string.IsNullOrEmpty(sequenceNumber) &&
		long.TryParse(sequenceNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: null;

	/// <summary>
	/// Resolves an SQS batch result entry ID back to the index of the input message it came from.
	/// Bounding the index to the chunk that was actually sent rejects an ID we did not issue rather than
	/// attributing a result to the wrong input.
	/// </summary>
	private static bool TryResolveInputIndex(string? entryId, int chunkStart, int chunkEnd, out int index) =>
		int.TryParse(entryId, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
		index >= chunkStart &&
		index < chunkEnd;

	private static bool IsTransient(Exception ex) =>
		ex is AmazonSQSException sqsEx && sqsEx.ErrorCode is
			"ServiceUnavailable" or "InternalError" or "ThrottlingException" or "RequestThrottled";

	[LoggerMessage(AwsSqsEventId.TransportSenderMessageSent, LogLevel.Debug,
		"SQS transport sender: message {MessageId} sent to {Destination}")]
	private partial void LogMessageSent(string messageId, string destination);

	[LoggerMessage(AwsSqsEventId.TransportSenderSendFailed, LogLevel.Error,
		"SQS transport sender: failed to send message {MessageId} to {Destination}")]
	private partial void LogSendFailed(string messageId, string destination, Exception exception);

	[LoggerMessage(AwsSqsEventId.TransportSenderBatchSent, LogLevel.Debug,
		"SQS transport sender: batch of {Count} messages sent to {Destination}, {SuccessCount} succeeded")]
	private partial void LogBatchSent(string destination, int count, int successCount);

	[LoggerMessage(AwsSqsEventId.TransportSenderBatchSendFailed, LogLevel.Error,
		"SQS transport sender: batch send of {Count} messages to {Destination} failed")]
	private partial void LogBatchSendFailed(string destination, int count, Exception exception);

	[LoggerMessage(AwsSqsEventId.TransportSenderDisposed, LogLevel.Debug,
		"SQS transport sender disposed for {Destination}")]
	private partial void LogDisposed(string destination);

	[LoggerMessage(AwsSqsEventId.TransportSenderUnresolvedBatchEntry, LogLevel.Warning,
		"SQS transport sender: batch result for {Destination} carried entry ID {EntryId}, which was not issued for that batch; the corresponding message is reported with an unknown outcome")]
	private partial void LogUnresolvedBatchEntry(string destination, string entryId);

	/// <summary>
	/// The single mapping of a <see cref="TransportMessage"/> onto the SQS wire shape, shared by the
	/// single-send request and the batch entry.
	/// </summary>
	private sealed class SqsOutboundMapping
	{
		public string Body { get; init; } = string.Empty;

		public string? MessageGroupId { get; init; }

		public string? MessageDeduplicationId { get; init; }

		public int? DelaySeconds { get; init; }

		public Dictionary<string, MessageAttributeValue>? MessageAttributes { get; init; }
	}
}
