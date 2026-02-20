// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Collects and tracks metrics for Azure Storage Queue operations using .NET Metrics API and OpenTelemetry.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="QueueMetricsCollector" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
public sealed partial class QueueMetricsCollector(ILogger<QueueMetricsCollector> logger) : IQueueMetricsCollector, IDisposable
{
	private static readonly ActivitySource ActivitySource = new("Excalibur.Dispatch.Transport.AzureServiceBus.StorageQueues", "1.0.0");
	private static readonly Meter Meter = new("Excalibur.Dispatch.Transport.AzureServiceBus.StorageQueues", "1.0.0");

	private static readonly Counter<long> MessagesProcessedCounter = Meter.CreateCounter<long>(
		name: "dispatch.azure_queue.messages_processed_total",
		description: "Total number of messages processed from Azure Storage Queues");

	private static readonly Histogram<double> ProcessingDurationHistogram = Meter.CreateHistogram<double>(
		name: "dispatch.azure_queue.processing_duration_ms",
		unit: "ms",
		description: "Duration of message processing in milliseconds");

	private static readonly Counter<long> BatchesProcessedCounter = Meter.CreateCounter<long>(
		name: "dispatch.azure_queue.batches_processed_total",
		description: "Total number of message batches processed");

	private static readonly Histogram<double> BatchSizeHistogram = Meter.CreateHistogram<double>(
		name: "dispatch.azure_queue.batch_size",
		description: "Size of message batches processed");

	private static readonly Counter<long> ReceiveOperationsCounter = Meter.CreateCounter<long>(
		name: "dispatch.azure_queue.receive_operations_total",
		description: "Total number of queue receive operations");

	private static readonly Histogram<double> ReceiveDurationHistogram = Meter.CreateHistogram<double>(
		name: "dispatch.azure_queue.receive_duration_ms",
		unit: "ms",
		description: "Duration of queue receive operations in milliseconds");

	private static readonly Counter<long> DeleteOperationsCounter = Meter.CreateCounter<long>(
		name: "dispatch.azure_queue.delete_operations_total",
		description: "Total number of message delete operations");

	private static readonly Counter<long> VisibilityTimeoutUpdatesCounter = Meter.CreateCounter<long>(
		name: "dispatch.azure_queue.visibility_updates_total",
		description: "Total number of visibility timeout update operations");

	private static readonly Gauge<long> QueueDepthGauge = Meter.CreateGauge<long>(
		name: "dispatch.azure_queue.depth",
		description: "Approximate number of messages in the queue");

	private readonly ILogger<QueueMetricsCollector> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	/// <summary>
	/// Local counters for snapshot generation.
	/// </summary>
	private long _totalMessagesProcessed;

	private long _successfulMessages;
	private long _failedMessages;
	private double _totalProcessingTimeMs;
	private long _totalBatchesProcessed;
	private double _totalBatchSize;
	private long _totalReceiveOperations;
	private double _totalReceiveTimeMs;
	private long _totalDeleteOperations;
	private long _successfulDeletes;
	private long _totalVisibilityUpdates;
	private long _successfulVisibilityUpdates;
	private volatile bool _disposed;

	/// <inheritdoc />
	public void RecordMessageProcessed(double processingTimeMs, bool success, string? messageType = null)
	{
		ThrowIfDisposed();

		var tags = CreateTags(success, messageType);

		MessagesProcessedCounter.Add(1, tags);
		ProcessingDurationHistogram.Record(processingTimeMs, tags);

		lock (_lock)
		{
			_totalMessagesProcessed++;
			_totalProcessingTimeMs += processingTimeMs;

			if (success)
			{
				_successfulMessages++;
			}
			else
			{
				_failedMessages++;
			}
		}

		LogMessageProcessed(processingTimeMs, success, messageType ?? "unknown");
	}

	/// <inheritdoc />
	public void RecordBatchProcessed(int batchSize, double processingTimeMs, int successCount, int errorCount)
	{
		ThrowIfDisposed();

		var tags = new KeyValuePair<string, object?>[]
		{
			new("batch_size", batchSize), new("success_count", successCount), new("error_count", errorCount),
		};

		BatchesProcessedCounter.Add(1, tags);
		BatchSizeHistogram.Record(batchSize, tags);
		ProcessingDurationHistogram.Record(processingTimeMs, tags);

		lock (_lock)
		{
			_totalBatchesProcessed++;
			_totalBatchSize += batchSize;
			_totalMessagesProcessed += batchSize;
			_totalProcessingTimeMs += processingTimeMs;
			_successfulMessages += successCount;
			_failedMessages += errorCount;
		}

		LogBatchProcessed(batchSize, processingTimeMs, successCount, errorCount);
	}

	/// <inheritdoc />
	public void RecordReceiveOperation(int messageCount, double receiveTimeMs)
	{
		ThrowIfDisposed();

		var tags = new KeyValuePair<string, object?>[] { new("message_count", messageCount) };

		ReceiveOperationsCounter.Add(1, tags);
		ReceiveDurationHistogram.Record(receiveTimeMs, tags);

		lock (_lock)
		{
			_totalReceiveOperations++;
			_totalReceiveTimeMs += receiveTimeMs;
		}

		LogReceiveOperation(messageCount, receiveTimeMs);
	}

	/// <inheritdoc />
	public void RecordDeleteOperation(bool success, double deleteTimeMs)
	{
		ThrowIfDisposed();

		var tags = CreateTags(success);

		DeleteOperationsCounter.Add(1, tags);

		lock (_lock)
		{
			_totalDeleteOperations++;
			if (success)
			{
				_successfulDeletes++;
			}
		}

		LogDeleteOperation(success, deleteTimeMs);
	}

	/// <inheritdoc />
	public void RecordVisibilityTimeoutUpdate(bool success, double updateTimeMs)
	{
		ThrowIfDisposed();

		var tags = CreateTags(success);

		VisibilityTimeoutUpdatesCounter.Add(1, tags);

		lock (_lock)
		{
			_totalVisibilityUpdates++;
			if (success)
			{
				_successfulVisibilityUpdates++;
			}
		}

		LogVisibilityTimeoutUpdate(success, updateTimeMs);
	}

	/// <inheritdoc />
	public void RecordQueueHealth(string queueName, long approximateMessageCount, bool isHealthy)
	{
		ThrowIfDisposed();
		ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

		var tags = new KeyValuePair<string, object?>[] { new("queue_name", queueName), new("is_healthy", isHealthy) };

		QueueDepthGauge.Record(approximateMessageCount, tags);

		LogQueueHealth(queueName, approximateMessageCount, isHealthy);
	}

	/// <inheritdoc />
	public QueueMetricsSnapshot GetMetricsSnapshot()
	{
		ThrowIfDisposed();

		lock (_lock)
		{
			return new QueueMetricsSnapshot
			{
				TotalMessagesProcessed = _totalMessagesProcessed,
				SuccessfulMessages = _successfulMessages,
				FailedMessages = _failedMessages,
				AverageProcessingTimeMs = _totalMessagesProcessed > 0 ? _totalProcessingTimeMs / _totalMessagesProcessed : 0,
				TotalBatchesProcessed = _totalBatchesProcessed,
				AverageBatchSize = _totalBatchesProcessed > 0 ? _totalBatchSize / _totalBatchesProcessed : 0,
				TotalReceiveOperations = _totalReceiveOperations,
				AverageReceiveTimeMs = _totalReceiveOperations > 0 ? _totalReceiveTimeMs / _totalReceiveOperations : 0,
				TotalDeleteOperations = _totalDeleteOperations,
				SuccessfulDeletes = _successfulDeletes,
				TotalVisibilityUpdates = _totalVisibilityUpdates,
				SuccessfulVisibilityUpdates = _successfulVisibilityUpdates,
			};
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		try
		{
			LogDisposing(_totalMessagesProcessed, _totalBatchesProcessed, _totalReceiveOperations);

			ActivitySource.Dispose();
			Meter.Dispose();
		}
		catch (Exception ex)
		{
			LogDisposalError(ex);
		}
		finally
		{
			_disposed = true;
		}
	}

	private static KeyValuePair<string, object?>[] CreateTags(bool success, string? messageType = null)
	{
		var tagList = new List<KeyValuePair<string, object?>> { new("success", success) };

		if (!string.IsNullOrWhiteSpace(messageType))
		{
			tagList.Add(new("message_type", messageType));
		}

		return [.. tagList];
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(QueueMetricsCollector));
		}
	}

	// Source-generated logging methods (Sprint 362 - EventId Migration)
	[LoggerMessage(AzureServiceBusEventId.StorageQueueMetricsMessageProcessed, LogLevel.Debug,
		"Recorded message processing: Duration={ProcessingTimeMs}ms, Success={Success}, Type={MessageType}")]
	private partial void LogMessageProcessed(double processingTimeMs, bool success, string messageType);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueMetricsBatchProcessed, LogLevel.Debug,
		"Recorded batch processing: Size={BatchSize}, Duration={ProcessingTimeMs}ms, Success={SuccessCount}, Errors={ErrorCount}")]
	private partial void LogBatchProcessed(int batchSize, double processingTimeMs, int successCount, int errorCount);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueMetricsReceiveOperation, LogLevel.Debug,
		"Recorded receive operation: Count={MessageCount}, Duration={ReceiveTimeMs}ms")]
	private partial void LogReceiveOperation(int messageCount, double receiveTimeMs);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueMetricsDeleteOperation, LogLevel.Debug,
		"Recorded delete operation: Success={Success}, Duration={DeleteTimeMs}ms")]
	private partial void LogDeleteOperation(bool success, double deleteTimeMs);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueMetricsVisibilityUpdate, LogLevel.Debug,
		"Recorded visibility timeout update: Success={Success}, Duration={UpdateTimeMs}ms")]
	private partial void LogVisibilityTimeoutUpdate(bool success, double updateTimeMs);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueMetricsHealthRecorded, LogLevel.Debug,
		"Recorded queue health: Queue={QueueName}, Depth={ApproximateMessageCount}, Healthy={IsHealthy}")]
	private partial void LogQueueHealth(string queueName, long approximateMessageCount, bool isHealthy);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueMetricsDisposing, LogLevel.Debug,
		"Disposing QueueMetricsCollector - Final metrics: Messages={TotalMessages}, Batches={TotalBatches}, Receives={TotalReceives}")]
	private partial void LogDisposing(long totalMessages, long totalBatches, long totalReceives);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueMetricsDisposalError, LogLevel.Warning,
		"Error during QueueMetricsCollector disposal")]
	private partial void LogDisposalError(Exception ex);
}
