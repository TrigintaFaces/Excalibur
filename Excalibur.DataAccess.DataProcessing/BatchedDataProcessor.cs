using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
	private readonly DataProcessingConfiguration _configuration;

	/// <summary>
	///     Initializes a new instance of the <see cref="BatchedDataProcessor{TRecord}" /> class.
	/// </summary>
	/// <param name="appLifetime"> Provides notifications about application lifetime events. </param>
	/// <param name="configuration"> The data processing configuration options. </param>
	/// <param name="serviceProvider"> The root service provider for creating new scopes. </param>
	/// <param name="logger"> The logger used for logging processing information and errors. </param>
	protected BatchedDataProcessor(
		IHostApplicationLifetime appLifetime,
		IOptions<DataProcessingConfiguration> configuration,
		IServiceProvider serviceProvider,
		ILogger logger) : base(appLifetime, configuration, serviceProvider, logger)
	{
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_configuration = configuration.Value;
	}

	/// <summary>
	///     Fetches all records asynchronously using a batch enumerator.
	/// </summary>
	/// <param name="skip"> The number of records that have already been processed. </param>
	/// <param name="cancellationToken"> A token that can be used to cancel the operation. </param>
	/// <returns> An asynchronous enumerable of records of type <typeparamref name="TRecord" />. </returns>
	public override IAsyncEnumerable<TRecord> FetchAllAsync(long skip, CancellationToken cancellationToken = default)
	{
		var batchEnumerator = new BatchEnumerator<TRecord>(FetchBatchAsync, _configuration.ProducerBatchSize);

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
