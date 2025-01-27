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
	private readonly int _originalBatchSize;
	private int _consecutiveSuccesses;

	public BatchEnumerator(FetchBatchDelegate<T> fetchBatch, int batchSize)
	{
		ArgumentNullException.ThrowIfNull(fetchBatch);
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		_fetchBatch = fetchBatch;
		_originalBatchSize = batchSize;
		BatchSize = batchSize;
	}

	public int BatchSize { get; private set; }

	public async IAsyncEnumerable<T> FetchAllAsync(long complete,
		[System.Runtime.CompilerServices.EnumeratorCancellation]
		CancellationToken cancellationToken = default)
	{
		Task<IEnumerable<T>>? nextBatchTask = null;

		while (true)
		{
			var currentBatch = (nextBatchTask == null
				? await FetchBatchWithRetries(complete, BatchSize, cancellationToken).ConfigureAwait(false)
				: await nextBatchTask.ConfigureAwait(false)).ToArray();

			if (currentBatch.Length == 0)
			{
				yield break;
			}

			nextBatchTask = FetchBatchWithRetries(complete + currentBatch.Length, BatchSize, cancellationToken);

			foreach (var record in currentBatch)
			{
				cancellationToken.ThrowIfCancellationRequested();
				yield return record;
			}

			if (currentBatch.Length < BatchSize)
			{
				yield break;
			}
		}
	}

	private async Task<IEnumerable<T>> FetchBatchWithRetries(long skip, int batchSize, CancellationToken cancellationToken)
	{
		const int MaxRetries = 3;
		var attempt = 0;

		while (true)
		{
			try
			{
				var batch = await _fetchBatch(skip, batchSize, cancellationToken).ConfigureAwait(false);
				AdjustBatchSizeOnSuccess();
				return batch;
			}
			catch (Exception) when (attempt < MaxRetries)
			{
				attempt++;
				await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)), cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				AdjustBatchSizeOnError();
				throw;
			}
		}
	}

	private void AdjustBatchSizeOnError()
	{
		_consecutiveSuccesses = 0; // Reset success counter
		BatchSize = Math.Max(1, BatchSize / 2); // Reduce batch size
	}

	private void AdjustBatchSizeOnSuccess()
	{
		_consecutiveSuccesses++;
		if (_consecutiveSuccesses < 5)
		{
			return;
		}

		// Gradually increase batch size after 5 consecutive successes
		BatchSize = Math.Min(_originalBatchSize, BatchSize + 1);
		_consecutiveSuccesses = 0; // Reset counter after increasing batch size
	}
}
