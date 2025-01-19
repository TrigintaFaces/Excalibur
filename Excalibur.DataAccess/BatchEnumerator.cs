namespace Excalibur.DataAccess;

/// <summary>
///     Represents a delegate to fetch a batch of records.
/// </summary>
/// <typeparam name="T"> The type of the record to fetch. </typeparam>
/// <param name="skip"> The number of records to skip. </param>
/// <param name="take"> The number of records to take. </param>
/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
/// <returns> A task representing the asynchronous operation, containing the fetched records. </returns>
public delegate Task<IEnumerable<T>> FetchBatchDelegate<T>(long skip, int take, CancellationToken cancellationToken);

/// <summary>
///     Provides functionality to fetch records in batches asynchronously.
/// </summary>
/// <typeparam name="T"> The type of the record being enumerated. </typeparam>
public class BatchEnumerator<T> : IBatchEnumerator<T>, IRecordFetcher<T>
{
	private readonly FetchBatchDelegate<T> _fetchBatch;

	/// <summary>
	///     Initializes a new instance of the <see cref="BatchEnumerator{T}" /> class.
	/// </summary>
	/// <param name="fetchBatch"> The delegate responsible for fetching a batch of records. </param>
	/// <param name="batchSize"> The size of each batch. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="fetchBatch" /> is null. </exception>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown if <paramref name="batchSize" /> is less than 1. </exception>
	public BatchEnumerator(FetchBatchDelegate<T> fetchBatch, int batchSize)
	{
		ArgumentNullException.ThrowIfNull(fetchBatch);
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		_fetchBatch = fetchBatch;
		BatchSize = batchSize;
	}

	/// <inheritdoc />
	public int BatchSize { get; }

	/// <inheritdoc cref="IBatchEnumerator{TRecord}.FetchAllAsync" />
	public async IAsyncEnumerable<T> FetchAllAsync(long complete,
		[System.Runtime.CompilerServices.EnumeratorCancellation]
		CancellationToken cancellationToken = default)
	{
		var skip = complete;

		while (true)
		{
			var batch = (await _fetchBatch(skip, BatchSize, cancellationToken).ConfigureAwait(false)).ToList();

			if (batch.Count == 0)
			{
				yield break;
			}

			foreach (var record in batch)
			{
				cancellationToken.ThrowIfCancellationRequested();
				yield return record;
			}

			skip += batch.Count;

			if (batch.Count < BatchSize)
			{
				yield break;
			}
		}
	}
}
