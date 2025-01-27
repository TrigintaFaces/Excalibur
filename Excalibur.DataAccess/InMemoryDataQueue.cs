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
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
	private readonly Channel<TRecord> _channel;
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

		_channel = Channel.CreateBounded<TRecord>(new BoundedChannelOptions(capacity)
		{
			SingleReader = true,
			SingleWriter = true,
			FullMode = BoundedChannelFullMode.Wait
		});
	}

	/// <summary>
	///     Finalizes an instance of the <see cref="InMemoryDataQueue{TRecord}" /> class.
	/// </summary>
	~InMemoryDataQueue() => Dispose(false);

	/// <inheritdoc />
	public ValueTask EnqueueAsync(TRecord record, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		return _channel.Writer.WriteAsync(record, cancellationToken);
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<TRecord> DequeueAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await foreach (var record in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return record;
		}
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<IList<TRecord>> DequeueAllInBatchesAsync(int batchSize,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var batch = new List<TRecord>();

		await foreach (var record in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			batch.Add(record);

			if (batch.Count >= batchSize)
			{
				yield return batch.ToList();
				batch.Clear();
			}
		}

		if (batch.Count > 0)
		{
			yield return batch;
		}
	}

	/// <inheritdoc />
	public async Task<IList<TRecord>> DequeueBatchAsync(int batchSize, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var batch = new List<TRecord>(batchSize);
		for (var i = 0; i < batchSize; i++)
		{
			if (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_channel.Reader.TryRead(out var record))
				{
					batch.Add(record);
				}
			}
			else
			{
				break;
			}
		}

		return batch;
	}

	public bool HasPendingItems()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return !_channel.Reader.Completion.IsCompleted && _channel.Reader.Count > 0;
	}

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
