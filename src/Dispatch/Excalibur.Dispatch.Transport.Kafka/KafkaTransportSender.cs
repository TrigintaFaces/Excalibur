// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka implementation of <see cref="ITransportSender"/>.
/// Uses Confluent.Kafka's <see cref="IProducer{TKey,TValue}"/> for native message production.
/// </summary>
/// <remarks>
/// <para>
/// Reads well-known property keys from <see cref="TransportMessage.Properties"/>:
/// </para>
/// <list type="bullet">
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.OrderingKey"/> maps to Kafka message key.</item>
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.PartitionKey"/> maps to Kafka message key (fallback).</item>
/// </list>
/// </remarks>
internal sealed partial class KafkaTransportSender : ITransportSender
{
	private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(10);

	private readonly IProducer<string, byte[]> _producer;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaTransportSender"/> class.
	/// </summary>
	/// <param name="producer">The Kafka producer instance.</param>
	/// <param name="destination">The destination topic name.</param>
	/// <param name="logger">The logger instance.</param>
	public KafkaTransportSender(
		IProducer<string, byte[]> producer,
		string destination,
		ILogger<KafkaTransportSender> logger)
	{
		_producer = producer ?? throw new ArgumentNullException(nameof(producer));
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
			var kafkaMessage = CreateKafkaMessage(message);
			var deliveryResult = await _producer.ProduceAsync(Destination, kafkaMessage, cancellationToken)
				.ConfigureAwait(false);

			LogMessageSent(message.Id, Destination, deliveryResult.Partition.Value, deliveryResult.Offset.Value);

			return new SendResult
			{
				IsSuccess = true,
				MessageId = message.Id,
				Partition = deliveryResult.Partition.Value.ToString(),
				SequenceNumber = deliveryResult.Offset.Value,
				AcceptedAt = DateTimeOffset.UtcNow,
			};
		}
		catch (ProduceException<string, byte[]> ex)
		{
			LogSendFailed(message.Id, Destination, ex);
			return SendResult.Failure(new SendError
			{
				Code = ex.Error.Code.ToString(),
				Message = ex.Error.Reason,
				Exception = ex,
				IsRetryable = !ex.Error.IsFatal,
			});
		}
		catch (Exception ex)
		{
			LogSendFailed(message.Id, Destination, ex);
			return SendResult.Failure(SendError.FromException(ex));
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
		var results = new List<SendResult>(messages.Count);

		foreach (var message in messages)
		{
			var result = await SendAsync(message, cancellationToken).ConfigureAwait(false);
			results.Add(result);
		}

		stopwatch.Stop();
		var successCount = results.Count(static r => r.IsSuccess);

		LogBatchSent(Destination, messages.Count, successCount);

		return new BatchSendResult
		{
			TotalMessages = messages.Count,
			SuccessCount = successCount,
			FailureCount = messages.Count - successCount,
			Results = results,
			Duration = stopwatch.Elapsed,
		};
	}

	/// <inheritdoc />
	public Task FlushAsync(CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Kafka Flush uses TimeSpan, not CancellationToken
		_producer.Flush(FlushTimeout);
		LogFlushed(Destination);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(IProducer<string, byte[]>))
		{
			return _producer;
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

	private static Message<string, byte[]> CreateKafkaMessage(TransportMessage message)
	{
		var key = GetMessageKey(message);
		var headers = new Headers();

		if (message.ContentType is not null)
		{
			headers.Add("content-type", Encoding.UTF8.GetBytes(message.ContentType));
		}

		if (message.MessageType is not null)
		{
			headers.Add("message-type", Encoding.UTF8.GetBytes(message.MessageType));
		}

		if (message.CorrelationId is not null)
		{
			headers.Add("correlation-id", Encoding.UTF8.GetBytes(message.CorrelationId));
		}

		if (message.Subject is not null)
		{
			headers.Add("subject", Encoding.UTF8.GetBytes(message.Subject));
		}

		headers.Add("message-id", Encoding.UTF8.GetBytes(message.Id));

		// Copy custom properties as headers
		foreach (var (propKey, propValue) in message.Properties)
		{
			if (!propKey.StartsWith("dispatch.", StringComparison.Ordinal))
			{
				headers.Add(propKey, Encoding.UTF8.GetBytes(propValue?.ToString() ?? string.Empty));
			}
		}

		return new Message<string, byte[]>
		{
			Key = key,
			Value = message.Body.ToArray(),
			Headers = headers,
		};
	}

	private static string GetMessageKey(TransportMessage message)
	{
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.OrderingKey, out var orderingKey) &&
			orderingKey is string orderingKeyStr)
		{
			return orderingKeyStr;
		}

		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.PartitionKey, out var partitionKey) &&
			partitionKey is string partitionKeyStr)
		{
			return partitionKeyStr;
		}

		return message.Id;
	}

	[LoggerMessage(KafkaEventId.TransportSenderMessageSent, LogLevel.Debug,
		"Kafka transport sender: message {MessageId} sent to {Destination} [partition={Partition}, offset={Offset}]")]
	private partial void LogMessageSent(string messageId, string destination, int partition, long offset);

	[LoggerMessage(KafkaEventId.TransportSenderSendFailed, LogLevel.Error,
		"Kafka transport sender: failed to send message {MessageId} to {Destination}")]
	private partial void LogSendFailed(string messageId, string destination, Exception exception);

	[LoggerMessage(KafkaEventId.TransportSenderBatchSent, LogLevel.Debug,
		"Kafka transport sender: batch of {Count} messages sent to {Destination}, {SuccessCount} succeeded")]
	private partial void LogBatchSent(string destination, int count, int successCount);

	[LoggerMessage(KafkaEventId.TransportSenderFlushed, LogLevel.Debug,
		"Kafka transport sender: flushed producer for {Destination}")]
	private partial void LogFlushed(string destination);

	[LoggerMessage(KafkaEventId.TransportSenderDisposed, LogLevel.Debug,
		"Kafka transport sender disposed for {Destination}")]
	private partial void LogDisposed(string destination);
}
