// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

using Excalibur.Dispatch.Metrics;
using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Manages ordered message processing for Google Cloud Pub/Sub messages with ordering keys. Ensures messages with the same ordering key
/// are processed sequentially while maintaining high throughput for messages with different ordering keys.
/// </summary>
public sealed partial class OrderingKeyProcessor : IOrderingKeyProcessor
{
	private readonly ConcurrentDictionary<string, OrderingKeyQueue> _orderingKeyQueues;
	private readonly Channel<OrderingKeyWork> _workChannel;
	private readonly Task[] _workerTasks;
	private readonly ILogger<OrderingKeyProcessor> _logger;
	private readonly OrderingKeyOptions _options;
	private readonly CancellationTokenSource _shutdownTokenSource;
	private readonly RateCounter _processedCount;
	private readonly RateCounter _errorCount;
	private readonly ValueHistogram _processingDuration;
	private readonly ValueHistogram _queueDepth;
	private readonly SemaphoreSlim _concurrencyLimiter;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderingKeyProcessor" /> class.
	/// </summary>
	/// <param name="options"> Configuration options. </param>
	/// <param name="logger"> Logger instance. </param>
	public OrderingKeyProcessor(
		IOptions<OrderingKeyOptions> options,
		ILogger<OrderingKeyProcessor> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_orderingKeyQueues = new ConcurrentDictionary<string, OrderingKeyQueue>(
			StringComparer.Ordinal);

		var workerCount = _options.MaxConcurrentOrderingKeys > 0
			? Math.Min(_options.MaxConcurrentOrderingKeys, Environment.ProcessorCount * 2)
			: Environment.ProcessorCount;

		_workChannel = Channel.CreateUnbounded<OrderingKeyWork>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

		_workerTasks = new Task[workerCount];
		_shutdownTokenSource = new CancellationTokenSource();

		_concurrencyLimiter = new SemaphoreSlim(
			_options.MaxConcurrentOrderingKeys,
			_options.MaxConcurrentOrderingKeys);

		// Initialize performance counters
		_processedCount = new RateCounter();
		_errorCount = new RateCounter();
		_processingDuration = new ValueHistogram();
		_queueDepth = new ValueHistogram();

		// Start worker tasks
		for (var i = 0; i < workerCount; i++)
		{
			var workerId = i;
			_workerTasks[i] = Task.Run(() => ProcessorWorkerAsync(workerId, _shutdownTokenSource.Token));
		}

		LogProcessorStarted(workerCount, _options.MaxConcurrentOrderingKeys);
	}

	/// <summary>
	/// Processes a message with ordering key support.
	/// </summary>
	/// <param name="message"> The message to process. </param>
	/// <param name="handler"> The handler to process the message. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the processing completion. </returns>
	/// <exception cref="InvalidOperationException"></exception>
	public async Task ProcessAsync(
		PubsubMessage message,
		Func<PubsubMessage, CancellationToken, Task> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(handler);

		ThrowIfDisposed();

		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity(
			"OrderingKey.Process",
			ActivityKind.Producer);

		_ = activity?.SetTag("messaging.message_id", message.MessageId);
		_ = activity?.SetTag("messaging.ordering_key", message.OrderingKey);

		// If no ordering key, process immediately without ordering constraints
		if (string.IsNullOrEmpty(message.OrderingKey))
		{
			await ProcessUnorderedMessageAsync(message, handler, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Get or create queue for this ordering key
		var queue = _orderingKeyQueues.GetOrAdd(
			message.OrderingKey,
			key => new OrderingKeyQueue(key, _options.MaxMessagesPerOrderingKey));

		// Enqueue the message
		var work = new OrderingKeyWork
		{
			Message = message,
			Handler = handler,
			OrderingKey = message.OrderingKey,
			CompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
			EnqueuedAt = Stopwatch.GetTimestamp(),
		};

		if (!queue.TryEnqueue(work))
		{
			throw new InvalidOperationException(
				$"Ordering key queue for '{message.OrderingKey}' is full. " +
				$"Max capacity: {_options.MaxMessagesPerOrderingKey}");
		}

		_queueDepth.Record(queue.Count);

		// Schedule processing if this queue isn't already being processed
		if (queue.TryStartProcessing())
		{
			await _workChannel.Writer.WriteAsync(
				new OrderingKeyWork { OrderingKey = message.OrderingKey },
				cancellationToken).ConfigureAwait(false);
		}

		// Wait for completion
		await work.CompletionSource.Task.ConfigureAwait(false);
	}

	/// <summary>
	/// Gets statistics about ordering key processing.
	/// </summary>
	/// <returns> Processing statistics. </returns>
	public OrderingKeyStatistics GetStatistics()
	{
		var queueStats = new List<QueueStatistics>();

		foreach (var kvp in _orderingKeyQueues)
		{
			queueStats.Add(new QueueStatistics
			{
				OrderingKey = kvp.Key,
				QueueDepth = kvp.Value.Count,
				IsProcessing = kvp.Value.IsProcessing,
				ProcessedCount = kvp.Value.ProcessedCount,
				ErrorCount = kvp.Value.ErrorCount,
			});
		}

		return new OrderingKeyStatistics
		{
			ActiveOrderingKeys = _orderingKeyQueues.Count,
			TotalProcessed = _processedCount.Value,
			TotalErrors = _errorCount.Value,
			AverageProcessingTime = _processingDuration.GetSnapshot().Mean,
			AverageQueueDepth = _queueDepth.GetSnapshot().Mean,
			QueueStatistics = queueStats,
		};
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		try
		{
			// Signal shutdown
			await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);
			_ = _workChannel.Writer.TryComplete();

			// Wait for all workers to complete
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await Task.WhenAll(_workerTasks).WaitAsync(cts.Token).ConfigureAwait(false);

			// Clean up all queues
			foreach (var queue in _orderingKeyQueues.Values)
			{
				queue.Dispose();
			}

			LogProcessorShutdown(_processedCount.Value, _errorCount.Value);
		}
		catch (OperationCanceledException)
		{
			LogShutdownTimeout();
		}
		finally
		{
			_shutdownTokenSource.Dispose();
			_concurrencyLimiter.Dispose();
		}
	}

	private async Task ProcessorWorkerAsync(int workerId, CancellationToken cancellationToken)
	{
		LogWorkerStarted(workerId);

		try
		{
			await foreach (var work in _workChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				if (!string.IsNullOrEmpty(work.OrderingKey))
				{
					await ProcessOrderingKeyQueueAsync(work.OrderingKey, cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Expected during shutdown
		}
		catch (Exception ex)
		{
			LogWorkerError(workerId, ex);
		}
		finally
		{
			LogWorkerStopped(workerId);
		}
	}

	private async Task ProcessOrderingKeyQueueAsync(string orderingKey, CancellationToken cancellationToken)
	{
		if (!_orderingKeyQueues.TryGetValue(orderingKey, out var queue))
		{
			return;
		}

		try
		{
			// Process all messages in the queue sequentially
			while (queue.TryDequeue(out var work))
			{
				if (work != null)
				{
					await ProcessWorkItemAsync(work, cancellationToken).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			// Mark queue as not processing
			queue.StopProcessing();

			// If queue still has items, reschedule processing
			if (queue.Count > 0 && queue.TryStartProcessing())
			{
				await _workChannel.Writer.WriteAsync(
					new OrderingKeyWork { OrderingKey = orderingKey },
					cancellationToken).ConfigureAwait(false);
			}

			// Clean up empty queues
			else if (queue.Count == 0 && _options.RemoveEmptyQueues)
			{
				CleanupEmptyQueue(orderingKey, queue);
			}
		}
	}

	private async Task ProcessWorkItemAsync(OrderingKeyWork work, CancellationToken cancellationToken)
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity(
			"OrderingKey.ProcessMessage",
			ActivityKind.Consumer);

		_ = activity?.SetTag("messaging.message_id", work.Message.MessageId);
		_ = activity?.SetTag("messaging.ordering_key", work.OrderingKey);

		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Acquire concurrency permit
			await _concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				// Record queue time
				var queueTime = Stopwatch.GetElapsedTime(work.EnqueuedAt);
				_ = activity?.SetTag("messaging.queue_time_ms", queueTime.TotalMilliseconds);

				// Process the message
				await work.Handler(work.Message, cancellationToken).ConfigureAwait(false);

				// Record success
				_ = _processedCount.Increment();
				_processingDuration.Record(stopwatch.ElapsedMilliseconds);

				if (_orderingKeyQueues.TryGetValue(work.OrderingKey, out var queue))
				{
					queue.IncrementProcessedCount();
				}

				_ = work.CompletionSource.TrySetResult();
			}
			finally
			{
				_ = _concurrencyLimiter.Release();
			}
		}
		catch (Exception ex)
		{
			_ = _errorCount.Increment();

			if (_orderingKeyQueues.TryGetValue(work.OrderingKey, out var queue))
			{
				queue.IncrementErrorCount();
			}

			LogMessageProcessingError(work.Message.MessageId, work.OrderingKey,
				_options.MaxRetries, ex);

			_ = work.CompletionSource.TrySetException(ex);
			_ = activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
		}
	}

	private async Task ProcessUnorderedMessageAsync(
		PubsubMessage message,
		Func<PubsubMessage, CancellationToken, Task> handler,
		CancellationToken cancellationToken)
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity(
			"OrderingKey.ProcessUnordered",
			ActivityKind.Consumer);

		_ = activity?.SetTag("messaging.message_id", message.MessageId);

		var stopwatch = Stopwatch.StartNew();

		try
		{
			await _concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				await handler(message, cancellationToken).ConfigureAwait(false);
				_ = _processedCount.Increment();
				_processingDuration.Record(stopwatch.ElapsedMilliseconds);
			}
			finally
			{
				_ = _concurrencyLimiter.Release();
			}
		}
		catch (Exception ex)
		{
			_ = _errorCount.Increment();
			LogUnorderedMessageError(message.MessageId, ex);
			throw;
		}
	}

	private void CleanupEmptyQueue(string orderingKey, OrderingKeyQueue queue)
	{
		if (queue is { Count: 0, IsProcessing: false } && _orderingKeyQueues.TryRemove(orderingKey, out _))
		{
			queue.Dispose();
			LogQueueRemoved(orderingKey);
		}
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(OrderingKeyProcessor));
		}
	}

	// Source-generated logging methods (Sprint 363 - EventId Migration)
	[LoggerMessage(GooglePubSubEventId.OrderingProcessorStarted, LogLevel.Information,
		"Ordering key processor started with {WorkerCount} workers, max concurrent keys: {MaxConcurrentKeys}")]
	private partial void LogProcessorStarted(int workerCount, int maxConcurrentKeys);

	[LoggerMessage(GooglePubSubEventId.OrderingProcessorShutdown, LogLevel.Information,
		"Ordering key processor shutdown complete. Processed: {ProcessedCount}, Errors: {ErrorCount}")]
	private partial void LogProcessorShutdown(long processedCount, long errorCount);

	[LoggerMessage(GooglePubSubEventId.OrderingProcessorShutdownTimeout, LogLevel.Warning,
		"Ordering key processor shutdown timed out")]
	private partial void LogShutdownTimeout();

	[LoggerMessage(GooglePubSubEventId.OrderingWorkerStarted, LogLevel.Debug,
		"Ordering key worker {WorkerId} started")]
	private partial void LogWorkerStarted(int workerId);

	[LoggerMessage(GooglePubSubEventId.OrderingWorkerStopped, LogLevel.Debug,
		"Ordering key worker {WorkerId} stopped")]
	private partial void LogWorkerStopped(int workerId);

	[LoggerMessage(GooglePubSubEventId.OrderingWorkerError, LogLevel.Error,
		"Ordering key worker {WorkerId} encountered fatal error")]
	private partial void LogWorkerError(int workerId, Exception ex);

	[LoggerMessage(GooglePubSubEventId.OrderingMessageProcessingError, LogLevel.Error,
		"Failed to process message {MessageId} with ordering key {OrderingKey} after {RetryCount} retries")]
	private partial void LogMessageProcessingError(string messageId, string orderingKey, int retryCount, Exception ex);

	[LoggerMessage(GooglePubSubEventId.UnorderedMessageError, LogLevel.Error,
		"Failed to process unordered message {MessageId}")]
	private partial void LogUnorderedMessageError(string messageId, Exception ex);

	[LoggerMessage(GooglePubSubEventId.OrderingQueueRemoved, LogLevel.Debug,
		"Removed empty queue for ordering key {OrderingKey}")]
	private partial void LogQueueRemoved(string orderingKey);

	/// <summary>
	/// Represents a unit of work for ordered processing.
	/// </summary>
	internal sealed class OrderingKeyWork
	{
		public PubsubMessage Message { get; init; } = null!;

		public Func<PubsubMessage, CancellationToken, Task> Handler { get; init; } = null!;

		public string OrderingKey { get; init; } = string.Empty;

		public TaskCompletionSource CompletionSource { get; init; } = null!;

		public long EnqueuedAt { get; init; }
	}
}
