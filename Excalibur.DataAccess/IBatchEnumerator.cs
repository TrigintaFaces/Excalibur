namespace Excalibur.DataAccess;

/// <summary>
///     Represents a contract for an enumerator that fetches records in batches asynchronously.
/// </summary>
/// <typeparam name="TRecord"> The type of the record being enumerated. </typeparam>
public interface IBatchEnumerator<out TRecord>
{
	/// <summary>
	///     Gets the size of the batch to be fetched.
	/// </summary>
	int BatchSize { get; }

	/// <summary>
	///     Fetches all records asynchronously in batches.
	/// </summary>
	/// <param name="complete"> The number of records already processed. Acts as the starting point for fetching. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> An asynchronous enumerable of records. </returns>
	IAsyncEnumerable<TRecord> FetchAllAsync(long complete, CancellationToken cancellationToken = default);
}
