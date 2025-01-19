using Microsoft.Extensions.Logging;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Represents an abstract base class for processing data in batches.
/// </summary>
/// <typeparam name="TRecord"> The type of records being processed. </typeparam>
/// <remarks>
///     This class is designed to process large datasets in batches while supporting asynchronous fetching and configurable parallelism.
/// </remarks>
public abstract class BatchedDataProcessor<TRecord> : DataProcessor<TRecord>
{
	private readonly int _batchSize;

	/// <summary>
	///     Initializes a new instance of the <see cref="BatchedDataProcessor{TRecord}" /> class.
	/// </summary>
	/// <param name="logger"> The logger used for logging processing information and errors. </param>
	/// <param name="batchSize"> The number of records to process in each batch. Default is <c> BATCH_SIZE </c>. </param>
	/// <param name="maxDegreeOfParallelism">
	///     The maximum number of concurrent operations allowed. Default is <c> MAX_DEGREE_OF_PARALLELISM </c>.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when <paramref name="batchSize" /> or <paramref name="maxDegreeOfParallelism" /> is less than or equal to zero.
	/// </exception>
	protected BatchedDataProcessor(
		ILogger logger,
		int batchSize = BatchSize,
		int maxDegreeOfParallelism = MaxDegreeOfParallelism) : base(logger, batchSize, maxDegreeOfParallelism)
	{
		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
		}

		_batchSize = batchSize;
	}

	/// <summary>
	///     Fetches all records asynchronously using a batch enumerator.
	/// </summary>
	/// <param name="skip"> The number of records that have already been processed. </param>
	/// <param name="cancellationToken"> A token that can be used to cancel the operation. </param>
	/// <returns> An asynchronous enumerable of records of type <typeparamref name="TRecord" />. </returns>
	public override IAsyncEnumerable<TRecord> FetchAllAsync(long skip, CancellationToken cancellationToken = default)
	{
		var batchEnumerator = new BatchEnumerator<TRecord>(FetchBatchAsync, _batchSize);

		return batchEnumerator.FetchAllAsync(skip, cancellationToken);
	}

	/// <summary>
	///     Fetches a single batch of records asynchronously.
	/// </summary>
	/// <param name="skip"> The number of records to skip before fetching the batch. </param>
	/// <param name="batchSize"> The number of records to include in the batch. </param>
	/// <param name="cancellationToken"> A token that can be used to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous operation, returning a collection of records in the batch. </returns>
	protected abstract Task<IEnumerable<TRecord>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken);
}
