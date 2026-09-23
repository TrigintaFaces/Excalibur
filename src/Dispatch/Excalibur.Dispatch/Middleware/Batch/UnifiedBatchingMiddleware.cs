// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Dispatch.BatchProcessing;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Options.Performance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Batch;

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
/// <param name="options"> The unified batching options. </param>
/// <param name="logger"> The logger. </param>
/// <param name="loggerFactory"> The logger factory. </param>
[AppliesTo(MessageKinds.All)]
public sealed partial class UnifiedBatchingMiddleware(
	IOptions<UnifiedBatchingOptions> options,
	ILogger<UnifiedBatchingMiddleware> logger,
	ILoggerFactory loggerFactory) : IDispatchMiddleware, IAsyncDisposable, IDisposable
{
	private static readonly ActivitySource ActivitySource = new(DispatchTelemetryConstants.ActivitySources.UnifiedBatchingMiddleware, "1.0.0");

	private readonly UnifiedBatchingOptions _options = ValidateOrThrow(options);
	private readonly ILogger<UnifiedBatchingMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Checks the batching options at construction, so the check cannot be skipped by constructing this
	/// middleware directly instead of through the dispatch pipeline.
	/// </summary>
	/// <remarks>
	/// Registering batching on the pipeline validates these options at host start, which is where a
	/// misconfiguration is cheapest to see. That covers the container; it does not cover a caller who
	/// constructs this type itself, and the values would then reach the batch processor unexamined.
	/// Validating here makes a batcher holding unusable options impossible to construct by any route,
	/// rather than leaving one route honest and the other silent.
	/// </remarks>
	/// <param name="options">The options to check.</param>
	/// <returns>The validated options.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> or its value is null.</exception>
	/// <exception cref="ArgumentException">The options are not usable.</exception>
	private static UnifiedBatchingOptions ValidateOrThrow(IOptions<UnifiedBatchingOptions> options)
	{
		var value = options?.Value ?? throw new ArgumentNullException(nameof(options));

		var result = new UnifiedBatchingOptionsValidator().Validate(name: null, value);

		return result.Failed
			? throw new ArgumentException(result.FailureMessage, nameof(options))
			: value;
	}

	// Source-generated logging methods
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

	// Disposable fields should be disposed - _loggerFactory is injected via DI and owned by the dependency injection container, not by this class
#pragma warning disable CA2213
	private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
#pragma warning restore CA2213

	private readonly ConcurrentDictionary<string, BatchProcessor<BatchItem>> _processors =
		new(StringComparer.Ordinal);

	private readonly CancellationTokenSource _cancellationTokenSource = new();
	/// <summary>
	/// The disposal claim. An <see langword="int"/> rather than a <see langword="bool"/> because exactly
	/// one caller must win it, and <c>volatile</c> cannot express that.
	/// </summary>
	/// <remarks>
	/// <b>Visibility is not atomicity.</b> This was a <c>volatile bool</c> tested and then set, which is two
	/// steps: two threads disposing concurrently could both pass the test, and the window spanned the whole
	/// teardown because the flag was set on the LAST line rather than the first. Inside that window a second
	/// disposer could call <see cref="CancellationTokenSource.Dispose()"/> while the first was still inside
	/// <c>Cancel()</c> -- which Microsoft documents as unsupported, that being the one member of
	/// <see cref="CancellationTokenSource"/> which is not thread-safe. A single interlocked exchange makes
	/// the race unrepresentable rather than unlikely, and it is the pattern this middleware's own
	/// <c>BatchProcessor</c> already uses.
	/// </remarks>
	private int _disposed;

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

		// REFUSE AFTER DISPOSAL rather than resurrect. Without this check GetOrAdd below repopulates the
		// dictionary that ReleaseResources just cleared, CONSTRUCTING a new BatchProcessor -- a dedicated
		// long-running thread, a Meter and an ActivitySource -- that nothing will ever dispose, because the
		// only disposer has already run. The middleware is registered SCOPED, so disposal happens at the end
		// of every scope and this is a steady-state path, not a shutdown-only one.
		ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

		var batchKey = GetBatchKey(message);
		_ = (activity?.SetTag("batching.key", batchKey));
		_ = (activity?.SetTag("batching.enabled", value: true));

		// Create completion source for this message
		var completionSource = new TaskCompletionSource<IMessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		var batchItem = new BatchItem(message, context, completionSource, nextDelegate);

		// THE AWAIT BELOW IS UNBOUNDED, SO EVERY WAY THE ITEM CAN STOP BEING PROCESSED MUST COMPLETE IT HERE.
		// The batch loop is not obliged to complete what it discards, and it discards on two ordinary paths:
		// it drops an item whose token is already cancelled when the batch is assembled, and it abandons the
		// items it has read but not yet flushed when disposal cancels it. Neither is exceptional -- the first
		// is a client disconnect, the second is the end of any scope -- and in both the completion source was
		// simply never completed, so the caller awaited a task that nothing would ever finish.
		//
		// Registering on both tokens closes that by construction rather than by agreement with the batch
		// loop: whichever happens first -- processed, caller cancelled, middleware disposed -- completes the
		// task, and TrySet* makes the other two no-ops. So the item is completed EXACTLY once without the
		// two components having to coordinate, which is what the seam could not express before.
		await using var callerCancellation = cancellationToken.Register(
			static state => ((TaskCompletionSource<IMessageResult>)state!).TrySetCanceled(),
			completionSource).ConfigureAwait(false);

		await using var disposalCancellation = _cancellationTokenSource.Token.Register(
			static state => ((TaskCompletionSource<IMessageResult>)state!).TrySetException(
				new ObjectDisposedException(
					nameof(UnifiedBatchingMiddleware),
					"The batching middleware was disposed while this message was waiting in a batch. The "
					+ "message was accepted but not dispatched; it has not been sent and may be retried.")),
			completionSource).ConfigureAwait(false);

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
		// Claim disposal before touching anything. Exactly one caller wins; every other returns.
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		// The only genuinely asynchronous step. CancelAsync runs registered callbacks without blocking the
		// caller's thread; the synchronous path below cannot do that and says so.
		try
		{
			if (!_cancellationTokenSource.IsCancellationRequested)
			{
				await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// DISPOSAL NEVER THROWS, and ObjectDisposedException is not the only thing that reaches here.
			// Cancel/CancelAsync run callbacks registered on this token, and the token is handed to arbitrary
			// consumer handler code -- so a consumer callback that throws arrives as an AggregateException.
			// Letting it escape would surface at the closing brace of the consumer's
			// `using var provider = ...`, which is precisely the crash this type's synchronous disposal was
			// added to remove. Cancellation is best-effort; the releases below still run.
		}

		ReleaseResources();
	}

	/// <summary>
	/// Releases this middleware's resources synchronously.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This exists because a service resolved from the container MUST be disposable synchronously, and
	/// omitting it turned the most common line a consumer writes into a crash.</b> Microsoft's dependency
	/// injection container throws <see cref="InvalidOperationException"/> from its scope disposal when a
	/// resolved service implements <see cref="IAsyncDisposable"/> and not <see cref="IDisposable"/> — so
	/// <c>using var provider = services.BuildServiceProvider();</c> threw at the closing brace for any
	/// consumer whose pipeline seated this middleware. The container is the caller here, not the consumer,
	/// and it decides which contract to invoke.
	/// </para>
	/// <para>
	/// <b>It does NOT block on the asynchronous path, and that is deliberate.</b> Calling
	/// <c>DisposeAsync().GetAwaiter().GetResult()</c> would be sync-over-async on a shutdown path and can
	/// deadlock on a context that serialises continuations. The one step that differs is cancellation:
	/// <see cref="CancellationTokenSource.Cancel()"/> runs registered callbacks on the calling thread where
	/// <c>CancelAsync</c> does not. Everything after it — disposing the processors, clearing the map,
	/// disposing the source — is synchronous in both paths and is shared rather than duplicated.
	/// </para>
	/// <para>
	/// Prefer <see cref="DisposeAsync"/> where the caller can await it. Both are idempotent.
	/// </para>
	/// </remarks>
	public void Dispose()
	{
		// Claim disposal before touching anything. Exactly one caller wins; every other returns.
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		try
		{
			if (!_cancellationTokenSource.IsCancellationRequested)
			{
				_cancellationTokenSource.Cancel();
			}
		}
		catch (Exception)
		{
			// DISPOSAL NEVER THROWS, and ObjectDisposedException is not the only thing that reaches here.
			// Cancel/CancelAsync run callbacks registered on this token, and the token is handed to arbitrary
			// consumer handler code -- so a consumer callback that throws arrives as an AggregateException.
			// Letting it escape would surface at the closing brace of the consumer's
			// `using var provider = ...`, which is precisely the crash this type's synchronous disposal was
			// added to remove. Cancellation is best-effort; the releases below still run.
		}

		ReleaseResources();
	}

	/// <summary>
	/// The disposal steps that are identical on both paths, so the two cannot drift apart.
	/// </summary>
	private void ReleaseResources()
	{
		foreach (var processor in _processors.Values)
		{
			processor.Dispose();
		}

		_processors.Clear();
		_cancellationTokenSource.Dispose();
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
		catch (HandlerNotRegisteredException ex)
		{
			// A missing handler registration is a configuration fault, not a request outcome, so it reaches each
			// caller AS the fault rather than as a 500-shaped failed result they can neither act on nor fix.
			// Every item in the batch is failed with it because the registration is missing for the message
			// TYPE: there is no sibling here that would have succeeded.
			var configFaultElapsedMs = (long)stopwatch.ElapsedMilliseconds;
			LogBatchError(batchKey, batch.Count, configFaultElapsedMs, ex);

			foreach (var item in batch)
			{
				_ = item.CompletionSource.TrySetException(ex);
			}

			_ = (activity?.SetTag("batch.duration_ms", configFaultElapsedMs));
			_ = (activity?.SetTag("batch.success", value: false));
			_ = (activity?.SetTag("exception.type", ex.GetType().FullName));
			_ = (activity?.SetTag("exception.message", ex.Message));
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
		// Create bulk message/context payloads without LINQ allocations.
		var messages = new IDispatchMessage[batch.Count];
		var contexts = new IMessageContext[batch.Count];
		for (var i = 0; i < batch.Count; i++)
		{
			var item = batch[i];
			messages[i] = item.Message;
			contexts[i] = item.Context;
		}

		var bulkMessage = new BulkMessage(messages, batchKey);
		var bulkContext = new BulkContext(contexts);

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
		using var semaphore = new SemaphoreSlim(_options.MaxParallelism, _options.MaxParallelism);
		var cancellationToken = _cancellationTokenSource.Token;
		var tasks = new Task[batch.Count];
		for (var i = 0; i < batch.Count; i++)
		{
			tasks[i] = ProcessBatchItemAsync(batch[i], semaphore, cancellationToken);
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	private static async Task ProcessBatchItemAsync(BatchItem item, SemaphoreSlim semaphore, CancellationToken cancellationToken)
	{
		await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var result = await item.NextDelegate(item.Message, item.Context, cancellationToken).ConfigureAwait(false);
			_ = item.CompletionSource.TrySetResult(result);
		}
		catch (HandlerNotRegisteredException ex)
		{
			// Per-item, so only the caller whose message type has no handler sees the configuration fault;
			// siblings of a different type in the same batch are unaffected.
			_ = item.CompletionSource.TrySetException(ex);
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
	}
}
