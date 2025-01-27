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
	private readonly IDataQueue<TRecord> _dataQueue = new InMemoryDataQueue<TRecord>();
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger _logger;
	private long _processedTotal;
	private bool _disposed;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataProcessor{TRecord}" /> class.
	/// </summary>
	/// <param name="serviceProvider"> The root service provider for creating new scopes. </param>
	/// <param name="logger"> The logger used for diagnostic information. </param>
	/// <param name="batchSize"> The size of batches used during processing. Default is 1000. </param>
	/// <param name="batchInterval"> The interval, in seconds, between batch updates. Default is 5 seconds. </param>
	protected DataProcessor(
		IServiceProvider serviceProvider,
		ILogger logger,
		int batchSize = BatchSize,
		int batchInterval = BatchIntervalSeconds)
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

		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	/// <summary>
	///     Finalizer for <see cref="DataProcessor" /> to ensure cleanup.
	/// </summary>
	~DataProcessor() => Dispose(false);

	/// <inheritdoc />
	public virtual async Task RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount,
		CancellationToken cancellationToken = default)
	{
		_ = Interlocked.Exchange(ref _processedTotal, completedCount);

		var scheduledTasks = new List<Task>();
		var scheduler = new OrderedTaskScheduler();

		try
		{
			var producerTask = Task.Run(async () =>
			{
				await foreach (var record in FetchAllAsync(_processedTotal, cancellationToken).ConfigureAwait(false))
				{
					await _dataQueue.EnqueueAsync(record, cancellationToken).ConfigureAwait(false);
				}
			}, cancellationToken);

			var consumerTask = Task.Run(async () =>
			{
				await foreach (var record in _dataQueue.DequeueAllAsync(cancellationToken).ConfigureAwait(false))
				{
					// ReSharper disable once AccessToDisposedClosure
					var scheduledTask = scheduler.ScheduleAsync(async () =>
					{
						await ProcessRecordAsync(record, cancellationToken).ConfigureAwait(false);
						var updatedTotal = Interlocked.Increment(ref _processedTotal); // Increment and update
						await updateCompletedCount(updatedTotal, cancellationToken).ConfigureAwait(false);
					});

					scheduledTasks.Add(scheduledTask);
				}
			}, cancellationToken);

			// Wait for producer and consumer to finish
			await Task.WhenAll(producerTask, consumerTask).ConfigureAwait(false);
		}
		finally
		{
			// Ensure all scheduled tasks are complete before disposing of the scheduler
			await Task.WhenAll(scheduledTasks).ConfigureAwait(false);

			scheduler.Dispose();

			if (_dataQueue is IDisposable disposableQueue)
			{
				disposableQueue.Dispose();
			}
		}
	}

	/// <summary>
	///     Fetches all records asynchronously starting from a specific point.
	/// </summary>
	/// <param name="skip"> The number of records to skip. </param>
	/// <param name="cancellationToken"> A token to signal cancellation. </param>
	/// <returns> An asynchronous enumerable of records. </returns>
	public abstract IAsyncEnumerable<TRecord> FetchAllAsync(long skip, CancellationToken cancellationToken = default);

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Disposes of resources used by the DataProcessor.
	/// </summary>
	/// <param name="disposing"> Indicates whether the method was called from <see cref="Dispose" /> or a finalizer. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_logger.LogInformation("Disposing DataProcessor resources.");
			_dataQueue.Dispose();
		}

		_disposed = true;
	}

	private async Task ProcessRecordAsync(TRecord record, CancellationToken cancellationToken)
	{
		using var scope = _serviceProvider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<IRecordHandler<TRecord>>();

		try
		{
			await handler.HandleAsync(record, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Successfully processed record of type {RecordType}.", typeof(TRecord).Name);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing record of type {RecordType}", typeof(TRecord).Name);
			throw;
		}
	}
}
