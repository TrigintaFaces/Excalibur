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

	private readonly SemaphoreSlim _queueSpaceAvailable;

	private readonly DataProcessingConfiguration _configuration;

	private readonly IServiceProvider _serviceProvider;

	private readonly IHostApplicationLifetime _appLifetime;

	private readonly ILogger _logger;

	private long _skipCount;

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
		_queueSpaceAvailable = new SemaphoreSlim(_configuration.QueueSize, _configuration.QueueSize);

		_ = _appLifetime.ApplicationStopping.Register(OnApplicationStopping);
	}

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

		var producerTask = Task.Run(() => ProducerLoop(linkedToken), linkedToken);
		var consumerTask = Task.Run(() => ConsumerLoop(linkedToken), linkedToken);

		var consumerResult = await consumerTask.ConfigureAwait(false);
		await producerTask.ConfigureAwait(false);

		return consumerResult;
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
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		if (disposing)
		{
			_logger.LogInformation("Disposing DataProcessor resources.");
			_dataQueue.Dispose();
			_queueSpaceAvailable.Dispose();
		}
	}

	private async Task ProducerLoop(CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var availableSlots = Math.Max(0, _configuration.QueueSize - _dataQueue.Count);
				if (availableSlots < 1)
				{
					await Task.Delay(10, cancellationToken).ConfigureAwait(false);
					continue;
				}

				var batchSize = Math.Min(_configuration.ProducerBatchSize, availableSlots);
				var batch = await FetchBatchAsync(_skipCount, batchSize, cancellationToken).ConfigureAwait(false) as ICollection<TRecord>
							?? [];

				if (batch.Count == 0)
				{
					_logger.LogInformation("No more DataProcessor records. Producer exiting.");
					Interlocked.Exchange(ref _producerCompleted, 1);
					break;
				}

				_logger.LogInformation("Enqueuing {BatchSize} DataProcessor records", batch.Count);

				foreach (var record in batch)
				{
					cancellationToken.ThrowIfCancellationRequested();

					await _queueSpaceAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
					await _dataQueue.EnqueueAsync(record, cancellationToken).ConfigureAwait(false);
				}

				_ = Interlocked.Add(ref _skipCount, batch.Count);

				_logger.LogInformation("Successfully enqueued {EnqueuedRowCount} DataProcessor records", batch.Count);
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

		if (_producerCompleted == 1)
		{
			_logger.LogInformation("DataProcessor Producer has completed execution.");
		}
	}

	private async Task<long> ConsumerLoop(CancellationToken cancellationToken)
	{
		var totalProcessedCount = 0;

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (_producerCompleted == 1 && _dataQueue.IsEmpty())
				{
					_logger.LogInformation("No more DataProcessor records. Consumer is exiting.");
					break;
				}

				var batch = await _dataQueue.DequeueBatchAsync(_configuration.ConsumerBatchSize, cancellationToken).ConfigureAwait(false);

				if (_producerCompleted == 0 && batch.Count == 0)
				{
					_logger.LogInformation("DataProcessor Queue is empty. Waiting for producer...");

					var waitTime = 10;
					for (var i = 0; i < 10; i++)
					{
						if (!_dataQueue.IsEmpty())
						{
							break;
						}

						await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
						waitTime = Math.Min(waitTime * 2, 100);
					}

					continue;
				}

				foreach (var record in batch)
				{
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
					finally
					{
						if (record is IDisposable disposable)
						{
							disposable.Dispose();
						}

						if (_queueSpaceAvailable.CurrentCount < _configuration.QueueSize)
						{
							_ = _queueSpaceAvailable.Release();
						}
					}
				}

				totalProcessedCount += batch.Count;

				_logger.LogDebug("Completed DataProcessor batch of {BatchSize} events", batch.Count);
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

		return totalProcessedCount;
	}

	private async Task ProcessRecordAsync(TRecord record, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);
		ArgumentNullException.ThrowIfNull(cancellationToken);

		using var scope = _serviceProvider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<IRecordHandler<TRecord>>();

		if (handler is null)
		{
			_logger.LogError("No handler found for {RecordType}. Skipping record.", typeof(TRecord).Name);
			return;
		}

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
