using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Excalibur.DataAccess;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix

/// <summary>
///     Provides an in-memory implementation of a data queue using bounded channels.
/// </summary>
/// <typeparam name="TRecord"> The type of the records in the queue. </typeparam>
/// <remarks>
///     This class is suitable for single-producer, single-consumer scenarios. The queue has a configurable capacity, and when the capacity
///     is reached, further enqueue operations will wait until space becomes available.
/// </remarks>
public sealed class InMemoryDataQueue<TRecord> : IDataQueue<TRecord>
#pragma warning restore CA1711
{
	// Identifiers should not have incorrect suffix
	private readonly Channel<TRecord> _channel;

	private int _count;

	private bool _disposed;

	/// <summary>
	///     Initializes a new instance of the <see cref="InMemoryDataQueue{TRecord}" /> class with the specified capacity.
	/// </summary>
	/// <param name="capacity"> The maximum capacity of the queue. </param>
	public InMemoryDataQueue(int capacity = 1000)
	{
		if (capacity <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
		}

		_channel = Channel.CreateBounded<TRecord>(
			new BoundedChannelOptions(capacity) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
	}

	/// <summary>
	///     Finalizes an instance of the <see cref="InMemoryDataQueue{TRecord}" /> class.
	/// </summary>
	~InMemoryDataQueue() => Dispose(false);

	/// <summary>
	///     Gets the current number of items in the queue.
	/// </summary>
	public int Count => Volatile.Read(ref _count);

	/// <inheritdoc />
	public async ValueTask EnqueueAsync(TRecord record, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _channel.Writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
		_ = Interlocked.Increment(ref _count);
	}

	/// <inheritdoc />
	public async ValueTask EnqueueBatchAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(records);

		foreach (var record in records)
		{
			cancellationToken.ThrowIfCancellationRequested();

			await _channel.Writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
			_ = Interlocked.Increment(ref _count);
		}
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<TRecord> DequeueAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await foreach (var record in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			_ = Interlocked.Decrement(ref _count);
			yield return record;
		}
	}

	/// <inheritdoc />
	public async Task<Memory<TRecord>> DequeueBatchAsync(int batchSize, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var buffer = ArrayPool<TRecord>.Shared.Rent(batchSize);
		Array.Clear(buffer, 0, batchSize);
		var index = 0;

		try
		{
			while (index < batchSize && await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_channel.Reader.TryRead(out var record))
				{
					buffer[index++] = record;
					_ = Interlocked.Decrement(ref _count);
				}
			}
		}
		finally
		{
			if (index == batchSize)
			{
				ArrayPool<TRecord>.Shared.Return(buffer, clearArray: true);
			}
		}

		return new Memory<TRecord>(buffer, 0, index);
	}

	public bool HasPendingItems() => Count > 0;

	public bool IsEmpty() => Count == 0;

	/// <summary>
	///     Releases resources used by the <see cref="InMemoryDataQueue{TRecord}" />.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Releases the unmanaged and optionally managed resources used by the <see cref="InMemoryDataQueue{TRecord}" />.
	/// </summary>
	/// <param name="disposing">
	///     <c> true </c> if the method is called from <see cref="Dispose" />; <c> false </c> if it is called from the finalizer.
	/// </param>
	private void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			// Complete the channel to release managed resources
			_ = _channel.Writer.TryComplete();
		}

		_disposed = true;
	}
}
