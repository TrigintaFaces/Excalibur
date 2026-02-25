// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Polly;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Bulkhead policy implementation using Polly.
/// </summary>
public partial class BulkheadPolicy : IBulkheadPolicy, IDisposable, IAsyncDisposable
{
	private readonly string _name;
	private readonly BulkheadOptions _options;
	private readonly ILogger _logger;
	private readonly ResiliencePipeline _pipeline;
	private readonly SemaphoreSlim _semaphore;
	private volatile bool _disposed;
	private long _totalExecutions;
	private long _rejectedExecutions;
	private long _queuedExecutions;
	private int _activeExecutions;
	private int _pendingWaiters;

	/// <summary>
	/// Initializes a new instance of the <see cref="BulkheadPolicy" /> class.
	/// </summary>
	/// <param name="name">The bulkhead identifier.</param>
	/// <param name="options">The bulkhead configuration options.</param>
	/// <param name="logger">The logger used for diagnostic output.</param>
	public BulkheadPolicy(string name, BulkheadOptions options, ILogger? logger = null)
	{
		_name = name ?? throw new ArgumentNullException(nameof(name));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger.Instance;

		_semaphore = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);

		// Build Polly pipeline with bulkhead - using simple semaphore approach for now Polly 8 doesn't have direct bulkhead/concurrency
		// limiter in the same way
		_pipeline = new ResiliencePipelineBuilder()
			.AddTimeout(_options.OperationTimeout)
			.Build();
	}

	/// <inheritdoc />
	public bool HasCapacity => _semaphore.CurrentCount > 0 || Volatile.Read(ref _pendingWaiters) < _options.MaxQueueLength;

	/// <inheritdoc />
	public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);

		_ = Interlocked.Increment(ref _totalExecutions);

		// Check if we should queue or reject.
		// Use semaphore.CurrentCount to determine if all execution slots are occupied.
		// Track pending waiters explicitly since SemaphoreSlim doesn't expose its wait queue depth.
		if (_semaphore.CurrentCount == 0)
		{
			var pending = Volatile.Read(ref _pendingWaiters);
			if (pending >= _options.MaxQueueLength)
			{
				_ = Interlocked.Increment(ref _rejectedExecutions);
				LogBulkheadRejected(_name, _options.MaxQueueLength);
				throw new BulkheadRejectedException($"Bulkhead '{_name}' queue is full");
			}

			_ = Interlocked.Increment(ref _queuedExecutions);
			LogBulkheadQueueing(_name, pending, _options.MaxQueueLength);
		}

		var startTime = DateTimeOffset.UtcNow;
		var semaphoreAcquired = false;

		_ = Interlocked.Increment(ref _pendingWaiters);
		try
		{
			// Wait for available slot
			await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
			semaphoreAcquired = true;
			_ = Interlocked.Decrement(ref _pendingWaiters);
			_ = Interlocked.Increment(ref _activeExecutions);

			LogBulkheadExecuting(_name, _activeExecutions, _options.MaxConcurrency);

			// Execute with Polly pipeline
			var result = await _pipeline.ExecuteAsync(
				async _ => await operation().ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);

			var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
			LogBulkheadCompleted(_name, duration);

			return result;
		}
		finally
		{
			if (semaphoreAcquired)
			{
				_ = Interlocked.Decrement(ref _activeExecutions);
				_ = _semaphore.Release();
			}
			else
			{
				// Semaphore was not acquired (e.g., cancellation while waiting)
				_ = Interlocked.Decrement(ref _pendingWaiters);
			}
		}
	}

	/// <inheritdoc />
	public BulkheadMetrics GetMetrics() =>
		new()
		{
			Name = _name,
			MaxConcurrency = _options.MaxConcurrency,
			MaxQueueLength = _options.MaxQueueLength,
			ActiveExecutions = _activeExecutions,
			QueueLength = Volatile.Read(ref _pendingWaiters),
			TotalExecutions = _totalExecutions,
			RejectedExecutions = _rejectedExecutions,
			QueuedExecutions = _queuedExecutions,
			AvailableCapacity = _semaphore.CurrentCount,
		};

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases managed resources.
	/// </summary>
	/// <param name="disposing">Indicates whether the method is called from <see cref="Dispose()"/>.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_semaphore.Dispose();
		}

		_disposed = true;
	}

	/// <summary>
	/// Asynchronously releases resources used by the bulkhead policy.
	/// </summary>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		Dispose(disposing: true);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.BulkheadExecuting, LogLevel.Debug,
		"Bulkhead '{Name}' executing operation. Active: {Active}/{MaxConcurrency}")]
	private partial void LogBulkheadExecuting(string name, int active, int maxConcurrency);

	[LoggerMessage(ResilienceEventId.BulkheadExecutionRejected, LogLevel.Warning,
		"Bulkhead '{Name}' rejected operation. Queue full at {QueueLength}")]
	private partial void LogBulkheadRejected(string name, int queueLength);

	[LoggerMessage(ResilienceEventId.BulkheadCompleted, LogLevel.Debug,
		"Bulkhead '{Name}' completed operation in {Duration}ms")]
	private partial void LogBulkheadCompleted(string name, double duration);

	[LoggerMessage(ResilienceEventId.BulkheadQueueing, LogLevel.Information,
		"Bulkhead '{Name}' queueing operation. Queue: {QueueLength}/{MaxQueueLength}")]
	private partial void LogBulkheadQueueing(string name, int queueLength, int maxQueueLength);
}
