// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Thread-safe queue for messages with the same ordering key. Ensures sequential processing while maintaining high performance.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="OrderingKeyQueue" /> class. </remarks>
/// <param name="orderingKey"> The ordering key for this queue. </param>
/// <param name="maxCapacity"> Maximum queue capacity. </param>
internal sealed class OrderingKeyQueue(string orderingKey, int maxCapacity) : IDisposable
{
	private readonly ConcurrentQueue<OrderingKeyProcessor.OrderingKeyWork> _queue = new();
	private int _isProcessing;
	private long _processedCount;
	private long _errorCount;
	private volatile bool _disposed;

	/// <summary>
	/// Gets the ordering key for this queue.
	/// </summary>
	/// <value>
	/// The ordering key for this queue.
	/// </value>
	public string OrderingKey { get; } = orderingKey ?? throw new ArgumentNullException(nameof(orderingKey));

	/// <summary>
	/// Gets the current number of items in the queue.
	/// </summary>
	/// <value>
	/// The current number of items in the queue.
	/// </value>
	public int Count => _queue.Count;

	/// <summary>
	/// Gets a value indicating whether gets whether the queue is currently being processed.
	/// </summary>
	/// <value>
	/// A value indicating whether gets whether the queue is currently being processed.
	/// </value>
	public bool IsProcessing => _isProcessing == 1;

	/// <summary>
	/// Gets the total number of successfully processed messages.
	/// </summary>
	/// <value>
	/// The total number of successfully processed messages.
	/// </value>
	public long ProcessedCount => Interlocked.Read(ref _processedCount);

	/// <summary>
	/// Gets the total number of errors encountered.
	/// </summary>
	/// <value>
	/// The total number of errors encountered.
	/// </value>
	public long ErrorCount => Interlocked.Read(ref _errorCount);

	/// <summary>
	/// Attempts to enqueue a work item.
	/// </summary>
	/// <param name="work"> The work item to enqueue. </param>
	/// <returns> True if enqueued successfully; false if queue is at capacity. </returns>
	/// <exception cref="ObjectDisposedException"></exception>
	public bool TryEnqueue(OrderingKeyProcessor.OrderingKeyWork work)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(OrderingKeyQueue));
		}

		if (_queue.Count >= maxCapacity)
		{
			return false;
		}

		_queue.Enqueue(work);
		return true;
	}

	/// <summary>
	/// Attempts to dequeue a work item.
	/// </summary>
	/// <param name="work">The dequeued work item, or null if the queue is empty.</param>
	/// <returns>True if an item was dequeued; false if queue is empty.</returns>
	/// <exception cref="ObjectDisposedException">Thrown when the queue has been disposed.</exception>
	public bool TryDequeue(out OrderingKeyProcessor.OrderingKeyWork? work)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(OrderingKeyQueue));
		}

		return _queue.TryDequeue(out work);
	}

	/// <summary>
	/// Attempts to mark the queue as being processed.
	/// </summary>
	/// <returns> True if processing was started; false if already processing. </returns>
	public bool TryStartProcessing() => Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0;

	/// <summary>
	/// Marks the queue as no longer being processed.
	/// </summary>
	public void StopProcessing() => _ = Interlocked.Exchange(ref _isProcessing, 0);

	/// <summary>
	/// Increments the processed count.
	/// </summary>
	public void IncrementProcessedCount() => _ = Interlocked.Increment(ref _processedCount);

	/// <summary>
	/// Increments the error count.
	/// </summary>
	public void IncrementErrorCount() => _ = Interlocked.Increment(ref _errorCount);

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Complete any remaining work items with cancellation
		while (_queue.TryDequeue(out var work))
		{
			_ = work.CompletionSource.TrySetCanceled();
		}
	}
}
