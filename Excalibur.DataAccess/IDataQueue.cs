namespace Excalibur.DataAccess;

/// <summary>
///     Defines a generic data queue interface for enqueuing and dequeuing records asynchronously.
/// </summary>
/// <typeparam name="TRecord"> The type of the records in the queue. </typeparam>
public interface IDataQueue<TRecord> : IDisposable
{
	/// <summary>
	///     Asynchronously enqueues a record into the queue.
	/// </summary>
	/// <param name="record"> The record to enqueue. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A <see cref="ValueTask" /> that represents the asynchronous operation. </returns>
	ValueTask EnqueueAsync(TRecord record, CancellationToken cancellationToken = default);

	/// <summary>
	///     Asynchronously enqueues a batch of records into the queue.
	/// </summary>
	ValueTask EnqueueBatchAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default);

	/// <summary>
	///     Asynchronously dequeues all records from the queue.
	/// </summary>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> An <see cref="IAsyncEnumerable{T}" /> that yields records from the queue as they are dequeued. </returns>
	IAsyncEnumerable<TRecord> DequeueAllAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Asynchronously dequeues a batch of records from the queue.
	/// </summary>
	/// <param name="batchSize"> The maximum number of records to retrieve in the batch. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous operation. The result contains a list of records retrieved in the batch. </returns>
	/// <remarks>
	///     If the queue contains fewer than <paramref name="batchSize" /> records, the returned batch will contain all available records.
	///     If the queue is empty, the result will be an empty list.
	/// </remarks>
	Task<Memory<TRecord>> DequeueBatchAsync(int batchSize, CancellationToken cancellationToken = default);

	/// <summary>
	///     Determines whether the queue contains pending items.
	/// </summary>
	/// <returns> <c> true </c> if the queue contains items that have not yet been dequeued; otherwise, <c> false </c>. </returns>
	/// <remarks> This method can be used to check if the queue is active or has any remaining records for processing. </remarks>
	bool HasPendingItems();
}
