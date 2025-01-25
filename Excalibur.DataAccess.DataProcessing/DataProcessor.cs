using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     A delegate for updating the count of completed records during data processing.
/// </summary>
/// <param name="complete"> The current number of completed records. </param>
/// <param name="cancellationToken"> A token to signal cancellation. </param>
/// <returns> A task that represents the asynchronous operation. </returns>
public delegate Task UpdateCompletedCount(long complete, CancellationToken cancellationToken = default);

/// <summary>
///     Provides an abstract base implementation for a batched data processing pipeline.
/// </summary>
/// <typeparam name="TRecord"> The type of records being processed. </typeparam>
public abstract class DataProcessor<TRecord> : IDataProcessor, IRecordFetcher<TRecord>
{
	private readonly TimeSpan _batchInterval;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger _logger;
	private readonly int _maxDegreeOfParallelism;
	private long _processedTotal;
	private CancellationTokenSource? _coordinatorCts;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataProcessor{TRecord}" /> class.
	/// </summary>
	/// <param name="serviceProvider"> The root service provider for creating new scopes. </param>
	/// <param name="logger"> The logger used for diagnostic information. </param>
	/// <param name="batchSize"> The size of batches used during processing. Default is 1000. </param>
	/// <param name="batchInterval"> The interval, in seconds, between batch updates. Default is 5 seconds. </param>
	/// <param name="maxDegreeOfParallelism"> The maximum number of concurrent consumer tasks. Default is 4. </param>
	protected DataProcessor(
		IServiceProvider serviceProvider,
		ILogger logger,
		int batchSize = BatchSize,
		int batchInterval = BatchIntervalSeconds,
		int maxDegreeOfParallelism = MaxDegreeOfParallelism)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
		}

		if (batchInterval <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchInterval), "Batch interval must be greater than zero.");
		}

		if (maxDegreeOfParallelism <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "Max degree of parallelism must be greater than zero.");
		}

		_serviceProvider = serviceProvider;
		_logger = logger;
		_maxDegreeOfParallelism = maxDegreeOfParallelism;
		_batchInterval = TimeSpan.FromSeconds(batchInterval);
		DataQueue = new InMemoryDataQueue<TRecord>(batchSize);
	}

	/// <summary>
	///     Gets the queue used for storing and retrieving data records.
	/// </summary>
	public IDataQueue<TRecord> DataQueue { get; }

	/// <inheritdoc />
	public virtual async Task RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount,
		CancellationToken cancellationToken = default)
	{
		_coordinatorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var coordinatorTask = Task.Run(() => CoordinatorLoop(updateCompletedCount, _coordinatorCts.Token), _coordinatorCts.Token);

		_ = Interlocked.Exchange(ref _processedTotal, completedCount);

		var producerTask = Task.Run(async () =>
		{
			await foreach (var record in FetchAllAsync(_processedTotal, cancellationToken).ConfigureAwait(false))
			{
				await DataQueue.EnqueueAsync(record, cancellationToken).ConfigureAwait(false);
			}
		}, cancellationToken);

		var consumerTasks = Enumerable.Range(0, _maxDegreeOfParallelism)
			.Select(_ => Task.Run(async () =>
			{
				await foreach (var record in DataQueue.DequeueAllAsync(cancellationToken).ConfigureAwait(false))
				{
					await ProcessRecordAsync(record, cancellationToken).ConfigureAwait(false);

#pragma warning disable IDE0058 // Expression value is never used
					Interlocked.Increment(ref _processedTotal);
#pragma warning restore IDE0058 // Expression value is never used
				}
			}, cancellationToken)).ToArray();

		await producerTask.ConfigureAwait(false);

		if (DataQueue is IDisposable disposable)
		{
			disposable.Dispose();
		}

		await Task.WhenAll(consumerTasks).ConfigureAwait(false);
		await _coordinatorCts.CancelAsync().ConfigureAwait(false);
		await coordinatorTask.ConfigureAwait(false);
	}

	/// <summary>
	///     Fetches all records asynchronously starting from a specific point.
	/// </summary>
	/// <param name="skip"> The number of records to skip. </param>
	/// <param name="cancellationToken"> A token to signal cancellation. </param>
	/// <returns> An asynchronous enumerable of records. </returns>
	public abstract IAsyncEnumerable<TRecord> FetchAllAsync(long skip, CancellationToken cancellationToken = default);

	private async Task ProcessRecordAsync(TRecord record, CancellationToken cancellationToken)
	{
		using var scope = _serviceProvider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<IRecordHandler<TRecord>>();

		try
		{
			await handler.HandleAsync(record, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing record of type {RecordType}", typeof(TRecord).Name);
			throw;
		}
	}

	private async Task CoordinatorLoop(UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken = default)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(_batchInterval, cancellationToken).ConfigureAwait(false);
				await UpdateComplete(updateCompletedCount, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (TaskCanceledException)
		{
			_logger.LogDebug("Coordinator loop canceled as expected.");
		}
		finally
		{
			await UpdateComplete(updateCompletedCount, CancellationToken.None).ConfigureAwait(false);
		}
	}

	private async Task UpdateComplete(UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken = default)
	{
		var totalSoFar = Interlocked.Read(ref _processedTotal);

		await updateCompletedCount(totalSoFar, cancellationToken).ConfigureAwait(false);
	}
}
