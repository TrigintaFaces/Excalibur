using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
	private readonly InMemoryDataQueue<TRecord> _dataQueue;

	private readonly DataProcessingConfiguration _configuration;

	private readonly IServiceProvider _serviceProvider;

	private readonly IHostApplicationLifetime _appLifetime;

	private readonly ILogger _logger;

	private long _skipCount;

	private long _processedTotal;

	private int _disposedFlag;

	private int _producerCompleted;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataProcessor{TRecord}" /> class.
	/// </summary>
	/// <param name="appLifetime"> Provides notifications about application lifetime events. </param>
	/// <param name="configuration"> The data processing configuration options. </param>
	/// <param name="serviceProvider"> The root service provider for creating new scopes. </param>
	/// <param name="logger"> The logger used for diagnostic information. </param>
	protected DataProcessor(
		IHostApplicationLifetime appLifetime,
		IOptions<DataProcessingConfiguration> configuration,
		IServiceProvider serviceProvider,
		ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_appLifetime = appLifetime;
		_configuration = configuration.Value;
		_serviceProvider = serviceProvider;
		_logger = logger;
		_dataQueue = new InMemoryDataQueue<TRecord>(_configuration.QueueSize);

		_ = _appLifetime.ApplicationStopping.Register(OnApplicationStopping);
	}

	/// <summary>
	///     Finalizer for DataProcessor to ensure cleanup.
	/// </summary>
	~DataProcessor() => Dispose(false);

	/// <inheritdoc />
	public virtual async Task<long> RunAsync(
		long completedCount,
		UpdateCompletedCount updateCompletedCount,
		CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);
		var linkedToken = linkedCts.Token;
		_ = Interlocked.Exchange(ref _skipCount, completedCount);
		_ = Interlocked.Exchange(ref _processedTotal, completedCount);

		var producerTask = Task.Run(() => ProducerLoop(linkedToken), linkedToken);
		var consumerTask = Task.Run(() => ConsumerLoop(linkedToken), linkedToken);

		await Task.WhenAll(producerTask, consumerTask).ConfigureAwait(false);

		_dataQueue.Dispose();

		return _processedTotal;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public abstract Task<IEnumerable<TRecord>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken);

	/// <summary>
	///     Disposes of resources used by the DataProcessor.
	/// </summary>
	/// <param name="disposing"> Indicates whether the method was called from <see cref="Dispose" /> or a finalizer. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (Interlocked.Exchange(ref _disposedFlag, 1) == 1)
		{
			return;
		}

		if (disposing)
		{
			_logger.LogInformation("Disposing DataProcessor resources.");
			_dataQueue.Dispose();
		}
	}

	private async Task ProducerLoop(CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (_dataQueue.Count >= _configuration.QueueSize - _configuration.ProducerBatchSize)
				{
					_logger.LogDebug("DataProcessor Queue is almost full. Producer waiting for consumer...");

					var spinWait = new SpinWait();
					var spinCount = 0;

					while (_dataQueue.Count >= _configuration.QueueSize - _configuration.ProducerBatchSize)
					{
						if (cancellationToken.IsCancellationRequested)
						{
							return;
						}

						if (spinCount < 10)
						{
							spinWait.SpinOnce();
							spinCount++;
						}
						else
						{
							await Task.Delay(100, cancellationToken).ConfigureAwait(false);
						}
					}
				}

				var batch = (await FetchBatchAsync(_skipCount, _configuration.ProducerBatchSize, cancellationToken).ConfigureAwait(false))
					.ToArray();

				if (batch.Length == 0)
				{
					_logger.LogInformation("No more DataProcessor records. Producer exiting.");
					_ = Interlocked.Exchange(ref _producerCompleted, 1);
					break;
				}

				_logger.LogInformation("Enqueuing {BatchSize} DataProcessor records", batch.Length);

				await _dataQueue.EnqueueBatchAsync(batch, cancellationToken).ConfigureAwait(false);
				_ = Interlocked.Add(ref _skipCount, batch.Length);

				_logger.LogInformation("Successfully enqueued {EnqueuedRowCount} DataProcessor records", batch.Length);
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogDebug("DataProcessor Producer canceled");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in DataProcessor ProducerLoop");
			throw;
		}

		if (Interlocked.CompareExchange(ref _producerCompleted, 0, 0) == 1)
		{
			_logger.LogInformation("DataProcessor Producer has completed execution.");
		}
	}

	private async Task<long> ConsumerLoop(CancellationToken cancellationToken)
	{
		const int MaxEmptyCycles = 100;
		var totalProcessedCount = 0;
		var emptyCycles = 0;

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var batch = await _dataQueue.DequeueBatchAsync(_configuration.ConsumerBatchSize, cancellationToken).ConfigureAwait(false);

				if (batch.Length == 0)
				{
					_logger.LogInformation("DataProcessor Queue is empty. Waiting for producer...");

					emptyCycles++;
					await Task.Delay(10, cancellationToken).ConfigureAwait(false);

					if (Interlocked.CompareExchange(ref _producerCompleted, 0, 0) == 1 && _dataQueue.IsEmpty()
																					   && emptyCycles >= MaxEmptyCycles)
					{
						_logger.LogInformation("No more DataProcessor records. Consumer is exiting.");
						break;
					}

					continue;
				}

				for (var i = 0; i < batch.Length; i++)
				{
					var record = batch.Span[i];

					if (record is null)
					{
						continue;
					}

					try
					{
						await ProcessRecordAsync(record, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						_logger.LogError(
							ex,
							"Error processing record of type {RecordType}. Continuing with next record...",
							typeof(TRecord).Name);
					}
				}

				_logger.LogInformation(
					"Completed batch processing in DataProcessor, total records processed so far: {TotalRecords}",
					_processedTotal);

				totalProcessedCount += batch.Length;
				emptyCycles = 0;

				_logger.LogDebug("Completed DataProcessor batch of {BatchSize} events", batch.Length);
			}

			_logger.LogInformation("Completed DataProcessor processing, total events processed: {TotalEvents}", totalProcessedCount);
		}
		catch (OperationCanceledException)
		{
			_logger.LogDebug("Consumer canceled");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in ConsumerLoop");
			throw;
		}

		_ = Interlocked.Add(ref _processedTotal, totalProcessedCount);

		return totalProcessedCount;
	}

	private async Task ProcessRecordAsync(TRecord record, CancellationToken cancellationToken)
	{
		using var scope = _serviceProvider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<IRecordHandler<TRecord>>();

		await handler.HandleAsync(record, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	///     Handles cleanup when the application is stopping.
	/// </summary>
	private void OnApplicationStopping()
	{
		_logger.LogInformation("Application is stopping. Ensuring data processing cleanup.");
		Dispose();
	}
}
