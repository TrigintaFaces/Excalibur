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
	private long _processedTotal;
	private bool _disposed;

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
	public virtual async Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount,
		CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);
		var linkedToken = linkedCts.Token;
		_ = Interlocked.Exchange(ref _processedTotal, completedCount);

		var producerTask = Task.Run(() => ProducerLoop(linkedToken), linkedToken);

		var consumerTask = Task.Run(() => ConsumerLoop(completedCount, linkedToken), linkedToken);

		await producerTask.ConfigureAwait(false);

		if (_dataQueue is IDisposable disposable)
		{
			disposable.Dispose();
		}

		var totalEvents = await consumerTask.ConfigureAwait(false);

		return totalEvents;
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

	private async Task<long> ConsumerLoop(long completedCount, CancellationToken cancellationToken)
	{
		var totalEvents = completedCount;

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var batch = await _dataQueue.DequeueBatchAsync(_configuration.ConsumerBatchSize, cancellationToken).ConfigureAwait(false);

				foreach (var record in batch)
				{
					await ProcessRecordAsync(record, cancellationToken).ConfigureAwait(false);
					totalEvents++;
				}

				if (batch.Count != 0 || _dataQueue.HasPendingItems())
				{
					continue;
				}

				_logger.LogInformation("Queue is empty. Consumer is idle.");
				break;
			}
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

		return totalEvents;
	}

	private async Task ProducerLoop(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var record in FetchAllAsync(_processedTotal, cancellationToken).ConfigureAwait(false))
			{
				await _dataQueue.EnqueueAsync(record, cancellationToken).ConfigureAwait(false);
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

	/// <summary>
	///     Handles cleanup when the application is stopping.
	/// </summary>
	private void OnApplicationStopping()
	{
		_logger.LogInformation("Application is stopping. Ensuring data processing cleanup.");
		Dispose();
	}
}
