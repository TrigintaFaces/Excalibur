// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.BatchProcessing;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Options.Performance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Unified high-performance batching middleware that consolidates message batching and bulk processing.
/// </summary>
/// <remarks>
/// This middleware combines the functionality of BatchingMiddleware and BulkProcessingMiddleware into a single, optimized implementation
/// that provides:
/// <list type="bullet">
/// <item> Zero-allocation micro-batching for high-throughput scenarios </item>
/// <item> Configurable batching strategies (time-based, size-based, or hybrid) </item>
/// <item> Bulk operation optimization with intelligent grouping </item>
/// <item> Backpressure-aware processing with bounded queues </item>
/// <item> Comprehensive observability and metrics </item>
/// </list>
/// </remarks>
/// <remarks> Initializes a new instance of the <see cref="UnifiedBatchingMiddleware" /> class. </remarks>
/// <param name="options"> The unified batching options. </param>
/// <param name="logger"> The logger. </param>
/// <param name="loggerFactory"> The logger factory. </param>
[AppliesTo(MessageKinds.All)]
public sealed partial class UnifiedBatchingMiddleware(
	IOptions<UnifiedBatchingOptions> options,
	ILogger<UnifiedBatchingMiddleware> logger,
	ILoggerFactory loggerFactory) : IDispatchMiddleware, IAsyncDisposable
{
	private static readonly ActivitySource ActivitySource = new(DispatchTelemetryConstants.ActivitySources.UnifiedBatchingMiddleware, "1.0.0");

	private readonly UnifiedBatchingOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<UnifiedBatchingMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.BatchingMiddlewareExecuting, LogLevel.Debug,
		"Message {MessageId} of type {MessageType} will not be batched")]
	private partial void LogMessageNotBatched(string messageId, string messageType);

	[LoggerMessage(MiddlewareEventId.MessageAddedToBatch, LogLevel.Debug,
		"Message {MessageId} added to batch {BatchKey} (current count: {Count})")]
	private partial void LogMessageAddedToBatch(string messageId, string batchKey, int count);

	[LoggerMessage(MiddlewareEventId.BatchCreated, LogLevel.Information,
		"Processing batch {BatchKey} with {Count} messages")]
	private partial void LogProcessingBatch(string batchKey, int count);

	[LoggerMessage(MiddlewareEventId.BatchFlushed, LogLevel.Information,
		"Completed batch {BatchKey} with {Count} messages in {Duration}ms")]
	private partial void LogBatchCompleted(string batchKey, int count, long duration);

	[LoggerMessage(MiddlewareEventId.BatchFlushed + 10, LogLevel.Error,
		"Error processing batch {BatchKey} with {Count} messages after {Duration}ms")]
	private partial void LogBatchError(string batchKey, int count, long duration, Exception ex);

	// R0.8: Disposable fields should be disposed - _loggerFactory is injected via DI and owned by the dependency injection container, not by this class
#pragma warning disable CA2213
	private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
#pragma warning restore CA2213

	private readonly ConcurrentDictionary<string, BatchProcessor<BatchItem>> _processors =
		new(StringComparer.Ordinal);

	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private volatile bool _disposed;

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Optimization;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		using var activity = ActivitySource.StartActivity("UnifiedBatchingMiddleware.Invoke");
		_ = (activity?.SetTag("message.id", context.MessageId ?? string.Empty));
		_ = (activity?.SetTag("message.type", message.GetType().Name));

		// Check for cancellation after activity creation to ensure observability tracking
		if (cancellationToken.IsCancellationRequested)
		{
			_ = (activity?.SetStatus(ActivityStatusCode.Error, "Operation was cancelled"));
			_ = (activity?.SetTag("cancellation.requested", value: true));
			cancellationToken.ThrowIfCancellationRequested();
		}

		// Check if this message should be batched
		if (!ShouldBatch(message))
		{
			LogMessageNotBatched(context.MessageId ?? string.Empty, message.GetType().Name);

			_ = (activity?.SetTag("batching.enabled", value: false));
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var batchKey = GetBatchKey(message);
		_ = (activity?.SetTag("batching.key", batchKey));
		_ = (activity?.SetTag("batching.enabled", value: true));

		// Create completion source for this message
		var completionSource = new TaskCompletionSource<IMessageResult>();
		var batchItem = new BatchItem(message, context, completionSource, nextDelegate);

		// Get or create micro-batch processor for this batch key
		var processor = _processors.GetOrAdd(batchKey, CreateProcessor);

		// Add item to the micro-batch processor
		await processor.AddAsync(batchItem, cancellationToken).ConfigureAwait(false);

		LogMessageAddedToBatch(context.MessageId ?? string.Empty, batchKey, 0);
		_ = (activity?.SetTag("batching.added", value: true));

		// Wait for batch processing to complete
		return await completionSource.Task.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		// Cancel if not already cancelled/disposed
		try
		{
			if (!_cancellationTokenSource.IsCancellationRequested)
			{
				await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
			}
		}
		catch (ObjectDisposedException)
		{
			// Already disposed, ignore
		}

		// Dispose all processors
		foreach (var processor in _processors.Values)
		{
			processor.Dispose();
		}

		_processors.Clear();
		_cancellationTokenSource.Dispose();
		_disposed = true;
	}

	private bool ShouldBatch(IDispatchMessage message)
	{
		if (_options.BatchFilter != null)
		{
			return _options.BatchFilter(message);
		}

		// Default: batch all message types unless configured otherwise
		return !_options.NonBatchableMessageTypes.Contains(message.GetType());
	}

	private string GetBatchKey(IDispatchMessage message)
	{
		if (_options.BatchKeySelector != null)
		{
			return _options.BatchKeySelector(message);
		}

		// Default: group by message type
		return message.GetType().Name;
	}

	private BatchProcessor<BatchItem> CreateProcessor(string batchKey)
	{
		var processorOptions = new MicroBatchOptions { MaxBatchSize = _options.MaxBatchSize, MaxBatchDelay = _options.MaxBatchDelay };

		return new BatchProcessor<BatchItem>(
			batch => ProcessBatchAsync(batchKey, batch),
			_loggerFactory.CreateLogger<BatchProcessor<BatchItem>>(),
			processorOptions);
	}

	private async ValueTask ProcessBatchAsync(string batchKey, IReadOnlyList<BatchItem> batch)
	{
		using var activity = ActivitySource.StartActivity("UnifiedBatchingMiddleware.ProcessBatchAsync");
		_ = (activity?.SetTag("batch.key", batchKey));
		_ = (activity?.SetTag("batch.count", batch.Count));

		LogProcessingBatch(batchKey, batch.Count);

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			if (_options.ProcessAsOptimizedBulk)
			{
				await ProcessAsOptimizedBulkAsync(batchKey, batch).ConfigureAwait(false);
			}
			else
			{
				await ProcessIndividuallyAsync(batch).ConfigureAwait(false);
			}

			var elapsedMs = (long)stopwatch.ElapsedMilliseconds;
			LogBatchCompleted(batchKey, batch.Count, elapsedMs);

			_ = (activity?.SetTag("batch.duration_ms", elapsedMs));
			_ = (activity?.SetTag("batch.success", value: true));
		}
		catch (Exception ex)
		{
			var elapsedMs = (long)stopwatch.ElapsedMilliseconds;
			LogBatchError(batchKey, batch.Count, elapsedMs, ex);

			// Complete all items with error
			var errorResult = MessageResult.Failed(new MessageProblemDetails
			{
				Type = "BatchProcessingError",
				Title = ErrorConstants.BatchProcessingFailed,
				ErrorCode = 500,
				Status = 500,
				Detail = $"Batch processing failed: {ex.Message}",
				Instance = batchKey,
			});

			foreach (var item in batch)
			{
				_ = item.CompletionSource.TrySetResult(errorResult);
			}

			_ = (activity?.SetTag("batch.duration_ms", elapsedMs));
			_ = (activity?.SetTag("batch.success", value: false));
			_ = (activity?.SetTag("exception.type", ex.GetType().FullName));
			_ = (activity?.SetTag("exception.message", ex.Message));
		}
	}

	private async ValueTask ProcessAsOptimizedBulkAsync(string batchKey, IReadOnlyList<BatchItem> batch)
	{
		// Create a bulk message
		var bulkMessage = new BulkMessage(
			[.. batch.Select(static item => item.Message)],
			batchKey);

		var bulkContext = new BulkContext(
			[.. batch.Select(static item => item.Context)]);

		try
		{
			// Use the first item's delegate (they should all be the same)
			var result = await batch[0].NextDelegate(bulkMessage, bulkContext, _cancellationTokenSource.Token).ConfigureAwait(false);

			// Complete all items with the same result
			foreach (var item in batch)
			{
				_ = item.CompletionSource.TrySetResult(result);
			}
		}
		catch (Exception)
		{
			// Fallback to individual processing
			await ProcessIndividuallyAsync(batch).ConfigureAwait(false);
		}
	}

	private async ValueTask ProcessIndividuallyAsync(IReadOnlyList<BatchItem> batch)
	{
		var semaphore = new SemaphoreSlim(_options.MaxParallelism, _options.MaxParallelism);
		var tasks = batch.Select(async item =>
		{
			await semaphore.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
			try
			{
				var result = await item.NextDelegate(item.Message, item.Context, _cancellationTokenSource.Token).ConfigureAwait(false);
				_ = item.CompletionSource.TrySetResult(result);
			}
			catch (Exception ex)
			{
				var errorResult = MessageResult.Failed(new MessageProblemDetails
				{
					Type = "MessageProcessingError",
					Title = ErrorConstants.MessageProcessingFailed,
					ErrorCode = 500,
					Status = 500,
					Detail = $"Individual message processing failed: {ex.Message}",
					Instance = item.Context.MessageId ?? "unknown",
				});
				_ = item.CompletionSource.TrySetResult(errorResult);
			}
			finally
			{
				_ = semaphore.Release();
			}
		});

		await Task.WhenAll(tasks).ConfigureAwait(false);
		semaphore.Dispose();
	}
}
