// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Performance;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.BatchProcessing;

/// <summary>
/// Micro-batch processor for efficient batch processing.
/// </summary>
internal sealed partial class BatchProcessor<T> : IDisposable, IAsyncDisposable
	where T : class
{
	private readonly Func<IReadOnlyList<T>, ValueTask> _batchProcessor;
	private readonly MicroBatchOptions _options;
	private readonly Channel<ItemWithToken> _inputChannel;
	private readonly Task _processingTask;
	[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
		Justification = "Disposed via Interlocked.Exchange in Dispose/DisposeAsync to prevent race conditions")]
	private CancellationTokenSource _shutdownTokenSource;
	private readonly ILogger<BatchProcessor<T>> _logger;
	// AD-251-4: Lock retained for List<Task> operations - List<T> is not thread-safe
	private readonly List<Task> _inFlightTasks = [];
#if NET9_0_OR_GREATER
	private readonly System.Threading.Lock _inFlightTasksLock = new();
#else
	private readonly object _inFlightTasksLock = new();
#endif
	private int _disposed;

	/// <summary>
	/// One-shot signal that fires when <see cref="ProcessBatchesAsync"/> starts reading from the channel.
	/// Awaited by <see cref="DisposeAsync"/> to prevent completing the channel before the processor starts.
	/// </summary>
	private readonly TaskCompletionSource _processingStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>
	/// ActivitySource for distributed tracing (instance-scoped to enable test listener registration).
	/// </summary>
	private readonly ActivitySource _activitySource;

	/// <summary>
	/// Meter for metrics collection (instance-scoped to enable test listener registration).
	/// </summary>
	private readonly Meter _meter;

	/// <summary>
	/// Counter for total items processed across all batches.
	/// </summary>
	private readonly Counter<long> _itemsProcessedCounter;

	/// <summary>
	/// Histogram for batch sizes.
	/// </summary>
	private readonly Histogram<int> _batchSizeHistogram;

	/// <summary>
	/// Histogram for batch processing duration in milliseconds.
	/// </summary>
	private readonly Histogram<double> _processingDurationHistogram;

	/// <summary>
	/// Wraps an item with its cancellation token for per-item cancellation tracking.
	/// </summary>
	private readonly struct ItemWithToken
	{
		public readonly T Item;
		public readonly CancellationToken Token;

		public ItemWithToken(T item, CancellationToken token)
		{
			Item = item;
			Token = token;
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BatchProcessor{T}" /> class.
	/// </summary>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by this class and disposed in Dispose()")]
	public BatchProcessor(
		Func<IReadOnlyList<T>, ValueTask> batchProcessor,
		ILogger<BatchProcessor<T>> logger,
		MicroBatchOptions? options = null)
		: this(batchProcessor, logger, meterFactory: null, options)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BatchProcessor{T}" /> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="batchProcessor"> The delegate that processes a batch of items. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates an unmanaged meter. </param>
	/// <param name="options"> Optional micro-batch configuration options. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory or this class and disposed in Dispose()")]
	public BatchProcessor(
		Func<IReadOnlyList<T>, ValueTask> batchProcessor,
		ILogger<BatchProcessor<T>> logger,
		IMeterFactory? meterFactory,
		MicroBatchOptions? options = null)
	{
		_batchProcessor = batchProcessor ?? throw new ArgumentNullException(nameof(batchProcessor));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options ?? new MicroBatchOptions();

		// Initialize OpenTelemetry instrumentation (instance-scoped for test listener compatibility)
		_activitySource = new ActivitySource(DispatchTelemetryConstants.ActivitySources.BatchProcessor, "1.0.0");
		_meter = meterFactory?.Create(DispatchTelemetryConstants.Meters.BatchProcessor)
			?? new Meter(DispatchTelemetryConstants.Meters.BatchProcessor, "1.0.0");
		_itemsProcessedCounter = _meter.CreateCounter<long>("dispatch.microbatch.items.processed", description: "Total number of items processed in batches");
		_batchSizeHistogram = _meter.CreateHistogram<int>("dispatch.microbatch.batch.size", description: "Size of batches processed");
		_processingDurationHistogram = _meter.CreateHistogram<double>("dispatch.microbatch.processing.duration", unit: "ms", description: "Duration of batch processing in milliseconds");

		// Create bounded input channel for backpressure -- producers block when full (Wait mode)
		// to prevent unbounded memory growth. Use DropOldest only for telemetry; batch data must not be lost.
		var channelOptions = new BoundedChannelOptions(_options.ChannelCapacity)
		{
			SingleWriter = false,
			SingleReader = true,
			FullMode = BoundedChannelFullMode.Wait,
		};
		_inputChannel = Channel.CreateBounded<ItemWithToken>(channelOptions);

		// Start processing task
		_shutdownTokenSource = new CancellationTokenSource();
		_processingTask = Task.Factory
			.StartNew(
				() => ProcessBatchesAsync(_shutdownTokenSource.Token),
				_shutdownTokenSource.Token,
				TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
				TaskScheduler.Default)
			.Unwrap();
	}

	/// <summary>
	/// Adds an item to be processed in a batch.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueTask AddAsync(T item, CancellationToken cancellationToken)
	{
		// Check for cancellation BEFORE queuing the item to honor pre-cancelled tokens
		cancellationToken.ThrowIfCancellationRequested();

		var wrapped = new ItemWithToken(item, cancellationToken);

		if (_inputChannel.Writer.TryWrite(wrapped))
		{
			return ValueTask.CompletedTask;
		}

		return _inputChannel.Writer.WriteAsync(wrapped, cancellationToken);
	}

	/// <summary>
	/// Asynchronously disposes the processor and waits for graceful shutdown of background processing.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the preferred disposal method. It properly awaits all background processing
	/// without blocking threads.
	/// </para>
	/// <para>
	/// Disposal waits up to 30 seconds for the processing task to complete and up to
	/// 60 seconds for in-flight batch tasks. If these timeouts are exceeded, warnings
	/// are logged but disposal completes successfully.
	/// </para>
	/// <para>
	/// Per requirement R1.13, disposal timeouts are classified as Canceled (not Error)
	/// and do not throw exceptions.
	/// </para>
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return; // Idempotent - safe to call multiple times
		}

		// Capture a local reference to avoid races with concurrent Dispose
		var cts = Interlocked.Exchange(ref _shutdownTokenSource, null!);

		// Wait for the processing task to start reading before completing the channel.
		// Without this, under thread pool saturation the channel could be completed
		// before ProcessBatchesAsync is scheduled, causing silent item loss.
		try
		{
			await _processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			LogProcessingTaskStartTimeout(_logger);

			// Safety net: drain any buffered items before completing the channel.
			// When ProcessBatchesAsync never started, these items would be silently lost.
			while (_inputChannel.Reader.TryRead(out var orphanedItem))
			{
				try
				{
					await _batchProcessor([orphanedItem.Item]).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogOrphanedItemProcessingError(_logger, ex);
				}
			}
		}

		_ = _inputChannel.Writer.TryComplete();

		try
		{
			// Wait for processing task with timeout
			using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			try
			{
				await _processingTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
			{
				LogProcessingTaskTimeout(_logger);
				try
				{
					if (cts is { IsCancellationRequested: false })
					{
						await cts.CancelAsync().ConfigureAwait(false);
					}
				}
				catch (ObjectDisposedException)
				{
					LogShutdownCancellationTokenDisposed(_logger);
				}
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				// Processing task was cancelled - expected during shutdown
			}

			// Wait for any remaining in-flight batch tasks to complete
			Task[] tasksToWait;
			lock (_inFlightTasksLock)
			{
				tasksToWait = [.. _inFlightTasks];
			}

			if (tasksToWait.Length > 0)
			{
				using var inFlightTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
				try
				{
					await Task.WhenAll(tasksToWait).WaitAsync(inFlightTimeoutCts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (inFlightTimeoutCts.IsCancellationRequested)
				{
					LogInFlightTasksTimeout(_logger, tasksToWait.Length);
				}
				catch (Exception)
				{
					// Ignore errors from in-flight tasks during shutdown - they're already logged
				}
			}
		}
		catch (Exception ex)
		{
			// Log error but don't rethrow - disposal must complete
			LogDisposeAsyncError(_logger, ex);
		}
		finally
		{
			// Always dispose resources in finally block
			cts?.Dispose();
			_meter?.Dispose();
			_activitySource?.Dispose();
		}

		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the processor synchronously. Prefer <see cref="DisposeAsync"/> for proper async cleanup.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This method signals cancellation and completes the channel but does not block waiting
	/// for background tasks. Use <see cref="DisposeAsync"/> for graceful shutdown with proper
	/// awaiting of background processing.
	/// </para>
	/// <para>
	/// This method is idempotent and safe to call multiple times. Subsequent calls after
	/// the first disposal are no-ops.
	/// </para>
	/// </remarks>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return; // Idempotent - safe to call multiple times
		}

		// Capture a local reference to avoid races with concurrent Dispose/DisposeAsync
		var cts = Interlocked.Exchange(ref _shutdownTokenSource, null!);

		try
		{
			// Safe cancellation - check before calling Cancel()
			if (cts is { IsCancellationRequested: false })
			{
				cts.Cancel();
			}
		}
		catch (ObjectDisposedException)
		{
			// CTS already disposed by another thread - safe to ignore
			LogShutdownCancellationTokenDisposed(_logger);
		}

		// Complete the channel to signal no more items
		_ = _inputChannel.Writer.TryComplete();

		// Don't block waiting for tasks - fire and forget cleanup
		// The tasks will complete on their own when cancelled
		// Use DisposeAsync for graceful shutdown with proper awaiting

		// Dispose resources immediately
		try
		{
			cts?.Dispose();
		}
		catch (ObjectDisposedException)
		{
			// Already disposed - ignore
		}

		_meter?.Dispose();
		_activitySource?.Dispose();

		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Processes batches in the background.
	/// </summary>
	private async Task ProcessBatchesAsync(CancellationToken cancellationToken)
	{
		_processingStarted.TrySetResult();

		var pendingItems = new List<ItemWithToken>(_options.MaxBatchSize);
		var activeBatch = new List<T>(_options.MaxBatchSize);
		const int maxConcurrentBatches = 10; // Limit to prevent unbounded concurrency
		var lastFlush = ValueStopwatch.GetTimestamp();

		try
		{
			// Use WaitToReadAsync with timeout instead of ReadAllAsync to enable time-based flushing
			while (true)
			{
				// Clean up completed tasks to free slots for new batches
				lock (_inFlightTasksLock)
				{
					_ = _inFlightTasks.RemoveAll(t => t.IsCompleted);
				}

				if (cancellationToken.IsCancellationRequested)
				{
					break;
				}

				// Wait if we've hit the concurrent batch limit
				int inFlightCount;
				lock (_inFlightTasksLock)
				{
					inFlightCount = _inFlightTasks.Count;
				}

				while (inFlightCount >= maxConcurrentBatches && !cancellationToken.IsCancellationRequested)
				{
					Task[] throttleTasks;
					lock (_inFlightTasksLock)
					{
						throttleTasks = [.. _inFlightTasks];
					}

					if (throttleTasks.Length > 0)
					{
						_ = await Task.WhenAny(throttleTasks).ConfigureAwait(false);
					}

					lock (_inFlightTasksLock)
					{
						_ = _inFlightTasks.RemoveAll(t => t.IsCompleted);
						inFlightCount = _inFlightTasks.Count;
					}
				}

				// Calculate remaining time until next flush
				var now = ValueStopwatch.GetTimestamp();
				var elapsedMs = (now - lastFlush) * 1000.0 / ValueStopwatch.GetFrequency();
				var remainingMs = _options.MaxBatchDelay.TotalMilliseconds - elapsedMs;

				// Check if flush time elapsed - if so, try to drain any available items immediately
				var shouldCheckForItems = true;
				if (remainingMs <= 0)
				{
					// Flush time elapsed - read any immediately available items without waiting
					while (pendingItems.Count < _options.MaxBatchSize && _inputChannel.Reader.TryRead(out var item))
					{
						pendingItems.Add(item);
					}

					shouldCheckForItems = pendingItems.Count == 0; // Only wait if we didn't find any items
				}

				// Wait for items if needed
				if (shouldCheckForItems && remainingMs > 0)
				{
					try
					{
						var timeout = TimeSpan.FromMilliseconds(remainingMs);
						var itemAvailable = await _inputChannel.Reader.WaitToReadAsync(cancellationToken).AsTask()
							.WaitAsync(timeout, cancellationToken)
							.ConfigureAwait(false);

						if (itemAvailable)
						{
							// Read all immediately available items up to batch size
							while (pendingItems.Count < _options.MaxBatchSize && _inputChannel.Reader.TryRead(out var wrappedItem))
							{
								pendingItems.Add(wrappedItem);
							}
						}
					}
					catch (TimeoutException)
					{
						// Timeout waiting for items - will flush pending batch below
					}
				}

				// Check if batch should be flushed (size-based or time-based)
				now = ValueStopwatch.GetTimestamp();
				elapsedMs = (now - lastFlush) * 1000.0 / ValueStopwatch.GetFrequency();
				var shouldFlush = pendingItems.Count >= _options.MaxBatchSize || elapsedMs >= _options.MaxBatchDelay.TotalMilliseconds;

				if (shouldFlush && pendingItems.Count > 0)
				{
					// Cooperative cancellation check before processing batch
					cancellationToken.ThrowIfCancellationRequested();

					FlushBatch(pendingItems, activeBatch, isShutdown: false);
					pendingItems.Clear();
					lastFlush = now;
				}

				if (_inputChannel.Reader.Completion.IsCompleted && pendingItems.Count == 0)
				{
					break;
				}
			}

			// Process remaining items - allow graceful flush even during shutdown
			if (pendingItems.Count > 0)
			{
				FlushBatch(pendingItems, activeBatch, isShutdown: true);
			}
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			_processingStarted.TrySetResult(); // Ensure DisposeAsync doesn't hang
			// Expected during shutdown
		}
		catch (TimeoutException)
		{
			// Timeout on WaitAsync - flush pending batch if any
			if (pendingItems.Count > 0)
			{
				FlushBatch(pendingItems, activeBatch, isShutdown: true);
			}
		}

		// Wait for all in-flight batch processing tasks to complete
		Task[] tasksToWait;
		lock (_inFlightTasksLock)
		{
			tasksToWait = [.. _inFlightTasks];
		}

		if (tasksToWait.Length > 0)
		{
			try
			{
				await Task.WhenAll(tasksToWait).ConfigureAwait(false);
			}
			catch
			{
				// Ignore errors from in-flight tasks during shutdown
			}
		}
	}

	/// <summary>
	/// Filters cancelled items, creates a batch snapshot, and fires off batch processing
	/// with activity tracing and metrics. Adds the task to in-flight tracking.
	/// </summary>
	/// <param name="pendingItems">The pending items to flush.</param>
	/// <param name="activeBatch">Reusable list for building the active batch.</param>
	/// <param name="isShutdown">Whether this is a shutdown/cleanup flush (suppresses rethrow on error).</param>
	private void FlushBatch(List<ItemWithToken> pendingItems, List<T> activeBatch, bool isShutdown)
	{
		// Filter out cancelled items
		activeBatch.Clear();
		foreach (var wrapped in pendingItems)
		{
			if (!wrapped.Token.IsCancellationRequested)
			{
				activeBatch.Add(wrapped.Item);
			}
		}

		if (activeBatch.Count == 0)
		{
			return;
		}

		// AD-251-4: Use ToArray() instead of ToList() for immutable batch snapshot
		var batchCopy = activeBatch.ToArray();

		// Capture parent Activity context for propagation across Task.Run() boundary (Phase 9.1h-2)
		var parentContext = Activity.Current?.Context ?? default;

		// Fire off batch processing without awaiting
		var processingTask = StartBatchWorkAsync(
			async () =>
		{
			// Create activity for batch processing with trace context linked to parent
			using var activity = _activitySource.StartActivity("BatchProcessor.ProcessBatch", ActivityKind.Internal, parentContext);
			_ = (activity?.SetTag("batch.size", batchCopy.Length));
			_ = (activity?.SetTag("component.name", "BatchProcessor"));
			if (isShutdown)
			{
				_ = (activity?.SetTag("shutdown", "true"));
			}

			var startTimestamp = ValueStopwatch.GetTimestamp();
			try
			{
				await _batchProcessor(batchCopy).ConfigureAwait(false);

				// Record success metrics
				var endTimestamp = ValueStopwatch.GetTimestamp();
				var elapsedMs = (endTimestamp - startTimestamp) * 1000.0 / ValueStopwatch.GetFrequency();
				_itemsProcessedCounter.Add(batchCopy.Length);
				_batchSizeHistogram.Record(batchCopy.Length);
				_processingDurationHistogram.Record(elapsedMs);

				_ = (activity?.SetStatus(ActivityStatusCode.Ok));
			}
			catch (Exception ex)
			{
				LogErrorProcessingBatchOfItems(ex, batchCopy.Length);
				_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
				_ = (activity?.AddTag("exception.type", ex.GetType().FullName));
				_ = (activity?.AddTag("exception.message", ex.Message));
				_ = (activity?.AddTag("exception.stacktrace", ex.StackTrace?.Length > 1024 ? ex.StackTrace[..1024] : ex.StackTrace));
				if (!isShutdown)
				{
					throw;
				}
			}
		});

		lock (_inFlightTasksLock)
		{
			_inFlightTasks.Add(processingTask);
		}
	}

	private Task StartBatchWorkAsync(Func<Task> work)
	{
		var token = _shutdownTokenSource?.Token ?? CancellationToken.None;
		return Task.Factory.StartNew(
			work,
			token,
			TaskCreationOptions.DenyChildAttach,
			TaskScheduler.Default).Unwrap();
	}

	// Source-generated logging methods
	[LoggerMessage(LogLevel.Debug, "CancellationTokenSource already disposed during shutdown signal")]
	private static partial void LogShutdownCancellationTokenDisposed(ILogger logger);

	[LoggerMessage(LogLevel.Warning, "BatchProcessor processing task did not start within 30 seconds during disposal")]
	private static partial void LogProcessingTaskStartTimeout(ILogger logger);

	[LoggerMessage(CoreEventId.OrphanedItemProcessingError, LogLevel.Error, "Error processing orphaned item during BatchProcessor shutdown drain")]
	private static partial void LogOrphanedItemProcessingError(ILogger logger, Exception exception);

	[LoggerMessage(LogLevel.Warning, "BatchProcessor processing task did not complete within 30 seconds during disposal")]
	private static partial void LogProcessingTaskTimeout(ILogger logger);

	[LoggerMessage(LogLevel.Warning, "BatchProcessor: {TaskCount} in-flight tasks did not complete within 60 seconds during disposal")]
	private static partial void LogInFlightTasksTimeout(ILogger logger, int taskCount);

	[LoggerMessage(LogLevel.Error, "Error during BatchProcessor async disposal")]
	private static partial void LogDisposeAsyncError(ILogger logger, Exception exception);

	[LoggerMessage(CoreEventId.MicroBatchError, LogLevel.Error,
		"Error processing batch of {BatchCount} items")]
	private partial void LogErrorProcessingBatchOfItems(Exception ex, int batchCount);
}
