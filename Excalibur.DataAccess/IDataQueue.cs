namespace Excalibur.DataAccess;

/// <summary>
///     Defines a generic data queue interface for enqueuing and dequeuing records asynchronously.
/// </summary>
/// <typeparam name="TRecord"> The type of the records in the queue. </typeparam>
public interface IDataQueue<TRecord>
{
	/// <summary>
	///     Asynchronously enqueues a record into the queue.
	/// </summary>
	/// <param name="record"> The record to enqueue. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A <see cref="ValueTask" /> that represents the asynchronous operation. </returns>
	ValueTask EnqueueAsync(TRecord record, CancellationToken cancellationToken = default);

	/// <summary>
	///     Asynchronously dequeues all records from the queue.
	/// </summary>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> An <see cref="IAsyncEnumerable{T}" /> that yields records from the queue as they are dequeued. </returns>
	IAsyncEnumerable<TRecord> DequeueAllAsync(CancellationToken cancellationToken = default);
}
