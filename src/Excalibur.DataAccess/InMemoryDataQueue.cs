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

	private int _disposedFlag;

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
	///     Gets the current number of items in the queue.
	/// </summary>
	public int Count => Volatile.Read(ref _count);

	public void CompleteWriter() => _ = _channel.Writer.TryComplete();

	/// <inheritdoc />
	public async ValueTask EnqueueAsync(TRecord record, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		await _channel.Writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
		_ = Interlocked.Increment(ref _count);
	}

	/// <inheritdoc />
	public async ValueTask EnqueueBatchAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);
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
		while (true)
		{
			ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

			if (_disposedFlag == 1)
			{
				yield break;
			}

			if (IsEmpty())
			{
				yield break;
			}

			if (!await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
			{
				yield break;
			}

			while (_channel.Reader.TryRead(out var record))
			{
				_ = Interlocked.Decrement(ref _count);

				if (record != null)
				{
					yield return record;
				}
			}
		}
	}

	/// <inheritdoc />
	public async Task<IList<TRecord>> DequeueBatchAsync(int batchSize, CancellationToken cancellationToken = default)
	{
		var available = Math.Min(batchSize, Count);
		var buffer = new TRecord[available];
		var index = 0;

		while (index < available && await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
		{
			ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

			while (_channel.Reader.TryRead(out var record))
			{
				if (record != null)
				{
					buffer[index++] = record;
				}

				_ = Interlocked.Decrement(ref _count);

				if (index >= available)
				{
					break;
				}
			}
		}

		return buffer;
	}

	public bool HasPendingItems() => Count > 0;

	public bool IsEmpty() => Count == 0;

	private void DisposeAsyncCore()
	{
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		CompleteWriter();

		while (_channel.Reader.TryRead(out _))
		{
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		DisposeAsyncCore();
		GC.SuppressFinalize(this);
	}
}
