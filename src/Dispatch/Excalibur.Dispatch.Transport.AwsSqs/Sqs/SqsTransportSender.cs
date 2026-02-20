// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Amazon.SQS;
using Amazon.SQS.Model;

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
				SequenceNumber = !string.IsNullOrEmpty(response.SequenceNumber)
					? long.Parse(response.SequenceNumber, CultureInfo.InvariantCulture)
					: null,
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
	public async Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);

		if (messages.Count == 0)
		{
			return new BatchSendResult { TotalMessages = 0, SuccessCount = 0, FailureCount = 0 };
		}

		var stopwatch = Stopwatch.StartNew();
		var allResults = new List<SendResult>(messages.Count);
		var successCount = 0;
		var failureCount = 0;

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
					allResults.Add(new SendResult
					{
						IsSuccess = true,
						MessageId = success.MessageId,
						SequenceNumber = !string.IsNullOrEmpty(success.SequenceNumber)
							? long.Parse(success.SequenceNumber, CultureInfo.InvariantCulture)
							: null,
						AcceptedAt = DateTimeOffset.UtcNow,
					});
					successCount++;
				}

				foreach (var failure in batchResponse.Failed)
				{
					allResults.Add(new SendResult
					{
						IsSuccess = false,
						Error = new SendError
						{
							Code = failure.Code,
							Message = failure.Message,
							IsRetryable = failure.SenderFault != true,
						},
					});
					failureCount++;
				}
			}
		}
		catch (Exception ex)
		{
			LogBatchSendFailed(Destination, messages.Count, ex);

			for (var i = allResults.Count; i < messages.Count; i++)
			{
				allResults.Add(SendResult.Failure(SendError.FromException(ex, IsTransient(ex))));
				failureCount++;
			}
		}

		stopwatch.Stop();
		LogBatchSent(Destination, messages.Count, successCount);

		return new BatchSendResult
		{
			TotalMessages = messages.Count,
			SuccessCount = successCount,
			FailureCount = failureCount,
			Results = allResults,
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

	private SendMessageRequest CreateSendRequest(TransportMessage message)
	{
		var request = new SendMessageRequest
		{
			QueueUrl = Destination,
			MessageBody = Encoding.UTF8.GetString(message.Body.Span),
		};

		// Map well-known properties to SQS native concepts
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.OrderingKey, out var orderingKey) &&
			orderingKey is string orderingKeyStr)
		{
			request.MessageGroupId = orderingKeyStr;
		}

		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.DeduplicationId, out var dedupId) &&
			dedupId is string dedupIdStr)
		{
			request.MessageDeduplicationId = dedupIdStr;
		}

		ApplyDelay(request, message);

		// Copy custom properties as message attributes
		if (message.Properties.Count > 0)
		{
			request.MessageAttributes = new Dictionary<string, MessageAttributeValue>(
				message.Properties.Count, StringComparer.Ordinal);

			foreach (var (key, value) in message.Properties)
			{
				if (!key.StartsWith("dispatch.", StringComparison.Ordinal))
				{
					request.MessageAttributes[key] = new MessageAttributeValue
					{
						DataType = "String",
						StringValue = value?.ToString() ?? string.Empty,
					};
				}
			}
		}

		// Add message metadata as attributes
		if (message.ContentType is not null)
		{
			request.MessageAttributes ??= new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);
			request.MessageAttributes["content-type"] = new MessageAttributeValue
			{
				DataType = "String",
				StringValue = message.ContentType,
			};
		}

		if (message.CorrelationId is not null)
		{
			request.MessageAttributes ??= new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);
			request.MessageAttributes["correlation-id"] = new MessageAttributeValue
			{
				DataType = "String",
				StringValue = message.CorrelationId,
			};
		}

		if (message.MessageType is not null)
		{
			request.MessageAttributes ??= new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);
			request.MessageAttributes["message-type"] = new MessageAttributeValue
			{
				DataType = "String",
				StringValue = message.MessageType,
			};
		}

		return request;
	}

	private SendMessageBatchRequestEntry CreateBatchEntry(TransportMessage message, int index)
	{
		var entry = new SendMessageBatchRequestEntry
		{
			Id = index.ToString(CultureInfo.InvariantCulture),
			MessageBody = Encoding.UTF8.GetString(message.Body.Span),
		};

		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.OrderingKey, out var orderingKey) &&
			orderingKey is string orderingKeyStr)
		{
			entry.MessageGroupId = orderingKeyStr;
		}

		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.DeduplicationId, out var dedupId) &&
			dedupId is string dedupIdStr)
		{
			entry.MessageDeduplicationId = dedupIdStr;
		}

		// Apply delay
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.DelaySeconds, out var delayObj) &&
			delayObj is string delayStr && int.TryParse(delayStr, out var delaySec))
		{
			entry.DelaySeconds = Math.Clamp(delaySec, 0, 900);
		}
		else if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.ScheduledTime, out var scheduledObj) &&
			scheduledObj is string scheduledStr && DateTimeOffset.TryParse(scheduledStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var scheduledTime))
		{
			var computed = (int)Math.Ceiling((scheduledTime - DateTimeOffset.UtcNow).TotalSeconds);
			if (computed > 0)
			{
				entry.DelaySeconds = Math.Clamp(computed, 0, 900);
			}
		}

		// Copy custom properties as message attributes
		if (message.Properties.Count > 0)
		{
			entry.MessageAttributes = new Dictionary<string, MessageAttributeValue>(
				message.Properties.Count, StringComparer.Ordinal);

			foreach (var (key, value) in message.Properties)
			{
				if (!key.StartsWith("dispatch.", StringComparison.Ordinal))
				{
					entry.MessageAttributes[key] = new MessageAttributeValue
					{
						DataType = "String",
						StringValue = value?.ToString() ?? string.Empty,
					};
				}
			}
		}

		return entry;
	}

	private static void ApplyDelay(SendMessageRequest request, TransportMessage message)
	{
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.DelaySeconds, out var delayObj) &&
			delayObj is string delayStr && int.TryParse(delayStr, out var delaySec))
		{
			request.DelaySeconds = Math.Clamp(delaySec, 0, 900);
			return;
		}

		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.ScheduledTime, out var scheduledObj) &&
			scheduledObj is string scheduledStr && DateTimeOffset.TryParse(scheduledStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var scheduledTime))
		{
			var computed = (int)Math.Ceiling((scheduledTime - DateTimeOffset.UtcNow).TotalSeconds);
			if (computed > 0)
			{
				request.DelaySeconds = Math.Clamp(computed, 0, 900);
			}
		}
	}

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
}
