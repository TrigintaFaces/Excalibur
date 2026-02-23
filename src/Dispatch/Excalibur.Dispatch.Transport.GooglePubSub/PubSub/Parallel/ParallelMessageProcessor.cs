// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;
using System.Diagnostics;
using System.Threading.Channels;

using Excalibur.Dispatch.Metrics;
using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// High-performance parallel message processor for Google Cloud Pub/Sub. Distributes message processing across multiple concurrent
/// workers using channels.
/// </summary>
public sealed partial class ParallelMessageProcessor : IAsyncDisposable
{
	private readonly Channel<ProcessingWork> _workChannel;
	private readonly Task[] _workerTasks;
	private readonly SemaphoreSlim _shutdownSemaphore;
	private readonly ILogger<ParallelMessageProcessor> _logger;
	private readonly GooglePubSubOptions _options;
	private readonly IGooglePubSubMetrics _metrics;
	private readonly CancellationTokenSource _shutdownTokenSource;
	private readonly RateCounter _processedCount;
	private readonly RateCounter _errorCount;
	private readonly ValueHistogram _processingDuration;
	private readonly Dictionary<string, int> _orderingKeyAffinity;
	private readonly ReaderWriterLockSlim _affinityLock;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ParallelMessageProcessor" /> class.
	/// </summary>
	/// <param name="options"> Configuration options. </param>
	/// <param name="metrics"> Metrics collector. </param>
	/// <param name="logger"> Logger instance. </param>
	public ParallelMessageProcessor(
		IOptions<GooglePubSubOptions> options,
		IGooglePubSubMetrics metrics,
		ILogger<ParallelMessageProcessor> logger)
	{
		_options = options.Value ?? throw new ArgumentNullException(nameof(options));
		_metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		var parallelism = _options.MaxConcurrentMessages > 0
			? _options.MaxConcurrentMessages
			: Environment.ProcessorCount * 2;

		_workChannel = Channel.CreateUnbounded<ProcessingWork>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

		_workerTasks = new Task[parallelism];
		_shutdownSemaphore = new SemaphoreSlim(0);
		_shutdownTokenSource = new CancellationTokenSource();
		_orderingKeyAffinity = new Dictionary<string, int>(StringComparer.Ordinal);
		_affinityLock = new ReaderWriterLockSlim();

		// Initialize performance counters
		_processedCount = new RateCounter();
		_errorCount = new RateCounter();
		_processingDuration = new ValueHistogram();

			// Start worker tasks
			for (var i = 0; i < parallelism; i++)
			{
				var workerId = i;
				_workerTasks[i] = StartBackgroundTask(() => ProcessorWorkerAsync(workerId, _shutdownTokenSource.Token));
			}

		LogProcessorStarted(parallelism);
	}

	/// <summary>
	/// Gets the current number of pending work items.
	/// </summary>
	/// <value>
	/// The current number of pending work items.
	/// </value>
	public int PendingWorkCount => _workChannel.Reader.Count;

	/// <summary>
	/// Gets the configured degree of parallelism.
	/// </summary>
	/// <value>
	/// The configured degree of parallelism.
	/// </value>
	public int DegreeOfParallelism => _workerTasks.Length;

	/// <summary>
	/// Enqueues a message for parallel processing.
	/// </summary>
	/// <param name="message"> The message to process. </param>
	/// <param name="handler"> The handler to process the message. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the processing completion. </returns>
	public async Task<ProcessingResult> EnqueueAsync(
		PubsubMessage message,
		Func<PubsubMessage, CancellationToken, Task> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(handler);

		ThrowIfDisposed();

		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity(
			"ParallelProcessor.Enqueue",
			ActivityKind.Producer);

		_ = activity?.SetTag("messaging.message_id", message.MessageId);
		_ = activity?.SetTag("messaging.ordering_key", message.OrderingKey);

		var work = new ProcessingWork
		{
			Message = message,
			Handler = handler,
			CompletionSource = new TaskCompletionSource<ProcessingResult>(
				TaskCreationOptions.RunContinuationsAsynchronously),
			EnqueuedAt = Stopwatch.GetTimestamp(),
		};

		// Assign work to specific worker if ordering key is present
		if (!string.IsNullOrEmpty(message.OrderingKey))
		{
			work.PreferredWorkerId = GetWorkerIdForOrderingKey(message.OrderingKey);
		}

		await _workChannel.Writer.WriteAsync(work, cancellationToken).ConfigureAwait(false);
		_metrics.MessageEnqueued();

		return await work.CompletionSource.Task.ConfigureAwait(false);
	}

	/// <summary>
	/// Gets statistics about worker utilization.
	/// </summary>
	/// <returns> Worker utilization statistics. </returns>
	public WorkerStatistics GetWorkerStatistics() =>
		new()
		{
			TotalWorkers = _workerTasks.Length,
			ActiveWorkers = _workerTasks.Count(static t => !t.IsCompleted),
			PendingWork = PendingWorkCount,
			ProcessedCount = _processedCount.Value,
			ErrorCount = _errorCount.Value,
			AverageProcessingTime = _processingDuration.GetSnapshot().Mean,
		};

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

			// Wait for all workers to complete with timeout
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await Task.WhenAll(_workerTasks).WaitAsync(cts.Token).ConfigureAwait(false);

			LogProcessorShutdown(_processedCount.Value, _errorCount.Value);
		}
		catch (OperationCanceledException)
		{
			LogShutdownTimeout();
		}
		finally
		{
			_shutdownSemaphore.Dispose();
			_shutdownTokenSource.Dispose();
			_affinityLock.Dispose();
		}
	}

	private static Task StartBackgroundTask(Func<Task> operation) =>
		Task.Factory.StartNew(
			operation,
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default).Unwrap();

	private async Task ProcessorWorkerAsync(int workerId, CancellationToken cancellationToken)
	{
		LogWorkerStarted(workerId);

		try
		{
			await foreach (var work in _workChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				// Skip if this work has ordering key affinity to different worker
				if (work.PreferredWorkerId.HasValue && work.PreferredWorkerId.Value != workerId)
				{
					// Re-enqueue for correct worker
					await _workChannel.Writer.WriteAsync(work, cancellationToken).ConfigureAwait(false);
					continue;
				}

				await ProcessWorkItemAsync(work, workerId, cancellationToken).ConfigureAwait(false);
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

	private async Task ProcessWorkItemAsync(
		ProcessingWork work,
		int workerId,
		CancellationToken cancellationToken)
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity(
			"ParallelProcessor.Process",
			ActivityKind.Consumer);

		_ = activity?.SetTag("messaging.worker_id", workerId);
		_ = activity?.SetTag("messaging.message_id", work.Message.MessageId);

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			// Update metrics
			var queueTime = Stopwatch.GetElapsedTime(work.EnqueuedAt);
			_metrics.MessageDequeued(queueTime);

			// Process the message
			await work.Handler(work.Message, cancellationToken).ConfigureAwait(false);

			// Record success
			_ = _processedCount.Increment();
			_processingDuration.Record(stopwatch.ElapsedMilliseconds);
			_metrics.MessageProcessed(stopwatch.Elapsed);

			work.CompletionSource.SetResult(
				new ProcessingResult { Success = true, WorkerId = workerId, ProcessingTime = stopwatch.Elapsed });
		}
		catch (Exception ex)
		{
			_ = _errorCount.Increment();
			_metrics.MessageFailed();

			LogMessageProcessingError(workerId, work.Message.MessageId, ex);

			work.CompletionSource.SetException(ex);
			_ = activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
		}
	}

	private int GetWorkerIdForOrderingKey(string orderingKey)
	{
		_affinityLock.EnterUpgradeableReadLock();
		try
		{
			if (_orderingKeyAffinity.TryGetValue(orderingKey, out var workerId))
			{
				return workerId;
			}

			_affinityLock.EnterWriteLock();
			try
			{
				// Double-check after acquiring write lock
				if (_orderingKeyAffinity.TryGetValue(orderingKey, out workerId))
				{
					return workerId;
				}

				// Assign to worker with least affinity
				var workerCounts = new int[_workerTasks.Length];
				foreach (var kvp in _orderingKeyAffinity)
				{
					workerCounts[kvp.Value]++;
				}

				workerId = Array.IndexOf(workerCounts, workerCounts.Min());
				_orderingKeyAffinity[orderingKey] = workerId;

				LogOrderingKeyAssigned(orderingKey, workerId);

				return workerId;
			}
			finally
			{
				_affinityLock.ExitWriteLock();
			}
		}
		finally
		{
			_affinityLock.ExitUpgradeableReadLock();
		}
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(ParallelMessageProcessor));
		}
	}

	// Source-generated logging methods (Sprint 363 - EventId Migration)
	[LoggerMessage(GooglePubSubEventId.ParallelProcessorStarted, LogLevel.Information,
		"Parallel message processor started with {WorkerCount} workers")]
	private partial void LogProcessorStarted(int workerCount);

	[LoggerMessage(GooglePubSubEventId.ParallelProcessorShutdown, LogLevel.Information,
		"Parallel processor shutdown complete. Processed: {ProcessedCount}, Errors: {ErrorCount}")]
	private partial void LogProcessorShutdown(long processedCount, long errorCount);

	[LoggerMessage(GooglePubSubEventId.ParallelProcessorShutdownTimeout, LogLevel.Warning,
		"Parallel processor shutdown timed out")]
	private partial void LogShutdownTimeout();

	[LoggerMessage(GooglePubSubEventId.ParallelWorkerStarted, LogLevel.Debug,
		"Worker {WorkerId} started")]
	private partial void LogWorkerStarted(int workerId);

	[LoggerMessage(GooglePubSubEventId.ParallelWorkerStopped, LogLevel.Debug,
		"Worker {WorkerId} stopped")]
	private partial void LogWorkerStopped(int workerId);

	[LoggerMessage(GooglePubSubEventId.ParallelWorkerError, LogLevel.Error,
		"Worker {WorkerId} encountered fatal error")]
	private partial void LogWorkerError(int workerId, Exception ex);

	[LoggerMessage(GooglePubSubEventId.ParallelMessageProcessingError, LogLevel.Error,
		"Worker {WorkerId} failed to process message {MessageId}")]
	private partial void LogMessageProcessingError(int workerId, string messageId, Exception ex);

	[LoggerMessage(GooglePubSubEventId.ParallelOrderingKeyAssigned, LogLevel.Debug,
		"Assigned ordering key {OrderingKey} to worker {WorkerId}")]
	private partial void LogOrderingKeyAssigned(string orderingKey, int workerId);

	/// <summary>
	/// Represents a unit of work for parallel processing.
	/// </summary>
	private sealed class ProcessingWork
	{
		public required PubsubMessage Message { get; init; }

		public required Func<PubsubMessage, CancellationToken, Task> Handler { get; init; }

		public required TaskCompletionSource<ProcessingResult> CompletionSource { get; init; }

		public required long EnqueuedAt { get; init; }

		public int? PreferredWorkerId { get; set; }
	}
}
