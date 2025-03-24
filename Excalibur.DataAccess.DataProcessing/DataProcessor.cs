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

	private readonly ILogger _logger;

	private readonly CancellationTokenSource _producerCancellationTokenSource = new();

	private readonly OrderedEventProcessor _orderedEventProcessor = new();

	private long _skipCount;

	private int _disposedFlag;

	private Task? _producerTask;

	private Task<long>? _consumerTask;

	private volatile bool _producerStopped;

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

		_configuration = configuration.Value;
		_serviceProvider = serviceProvider;
		_logger = logger;
		_dataQueue = new InMemoryDataQueue<TRecord>(_configuration.QueueSize);

		_ = appLifetime.ApplicationStopping.Register(() => Task.Run(OnApplicationStoppingAsync));
	}

	private CancellationToken ProducerCancellationToken => _producerCancellationTokenSource.Token;

	private bool ShouldWaitForProducer => !_producerStopped && !(_producerTask?.IsCompleted ?? true) && _dataQueue.IsEmpty();

	/// <inheritdoc />
	public virtual async Task<long> RunAsync(
		long completedCount,
		UpdateCompletedCount updateCompletedCount,
		CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		_ = Interlocked.Exchange(ref _skipCount, completedCount);

		_producerTask = Task.Run(() => ProducerLoop(cancellationToken), cancellationToken);
		_consumerTask = Task.Run(() => ConsumerLoop(cancellationToken), cancellationToken);

		await _producerTask.ConfigureAwait(false);
		var consumerResult = await _consumerTask.ConfigureAwait(false);

		return consumerResult;
	}

	public abstract Task<IEnumerable<TRecord>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken);

	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Disposes of resources used by the DataProcessor.
	/// </summary>
	protected virtual async ValueTask DisposeAsyncCore()
	{
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		_logger.LogInformation("Disposing DataProcessor resources asynchronously.");

		if (_consumerTask is { IsCompleted: false })
		{
			_logger.LogWarning("Disposing DataProcessor but Consumer has not completed.");
			_ = await _consumerTask.WaitAsync(TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
		}

		if (_producerTask is not null)
		{
			await CastAndDispose(_producerTask).ConfigureAwait(false);
		}

		if (_consumerTask is not null)
		{
			await CastAndDispose(_consumerTask).ConfigureAwait(false);
		}

		await _dataQueue.DisposeAsync().ConfigureAwait(false);
		await _orderedEventProcessor.DisposeAsync().ConfigureAwait(false);

		_producerCancellationTokenSource.Dispose();
		return;

		static async ValueTask CastAndDispose(IDisposable resource)
		{
			switch (resource)
			{
				case IAsyncDisposable resourceAsyncDisposable:
					await resourceAsyncDisposable.DisposeAsync().ConfigureAwait(false);
					break;

				default:
					resource.Dispose();
					break;
			}
		}
	}

	private async Task ProducerLoop(CancellationToken cancellationToken)
	{
		try
		{
			using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ProducerCancellationToken);
			var combinedToken = combinedTokenSource.Token;

			while (!combinedToken.IsCancellationRequested)
			{
				var availableSlots = Math.Max(0, _configuration.QueueSize - _dataQueue.Count);
				if (availableSlots < 1)
				{
					await Task.Delay(10, combinedToken).ConfigureAwait(false);
					continue;
				}

				var batchSize = Math.Min(_configuration.ProducerBatchSize, availableSlots);
				var batch = await FetchBatchAsync(_skipCount, batchSize, combinedToken).ConfigureAwait(false) as ICollection<TRecord> ?? [];

				if (batch.Count == 0)
				{
					_logger.LogInformation("No more DataProcessor records. Producer exiting.");
					break;
				}

				_logger.LogInformation("Enqueuing {BatchSize} DataProcessor records", batch.Count);

				foreach (var record in batch)
				{
					await _dataQueue.EnqueueAsync(record, combinedToken).ConfigureAwait(false);
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
		finally
		{
			_producerStopped = true;
			_dataQueue.CompleteWriter();

			_logger.LogInformation("DataProcessor Producer has completed execution. Channel marked as complete.");
		}
	}

	private async Task<long> ConsumerLoop(CancellationToken cancellationToken)
	{
		var totalProcessedCount = 0;

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (_disposedFlag == 1)
				{
					_logger.LogWarning("ConsumerLoop: disposal requested, exit now.");
					break;
				}

				if (_producerStopped && _dataQueue.IsEmpty())
				{
					_logger.LogInformation("No more DataProcessor records. Consumer is exiting.");
					break;
				}

				if (ShouldWaitForProducer)
				{
					_logger.LogInformation("DataProcessor Queue is empty. Waiting for producer...");

					var waitTime = 10;
					for (var i = 0; i < 10; i++)
					{
						await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
						waitTime = Math.Min(waitTime * 2, 100);
					}

					continue;
				}

				var batch = await _dataQueue.DequeueBatchAsync(_configuration.ConsumerBatchSize, cancellationToken).ConfigureAwait(false);
				_logger.LogInformation("DataProcessor processing batch of {BatchSize} records", batch.Count);

				foreach (var record in batch)
				{
					if (record is null)
					{
						continue;
					}

					try
					{
						// Ensures events are dispatched in order. Critical when using Mediator to publish domain events that must be
						// processed sequentially.
						await _orderedEventProcessor.ProcessAsync(async () =>
						{
							await ProcessRecordAsync(record, cancellationToken).ConfigureAwait(false);
						}).ConfigureAwait(false);
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
	private async Task OnApplicationStoppingAsync()
	{
		_logger.LogInformation("Application is stopping. Cancelling DataProcessor producer immediately.");

		_producerStopped = true;
		await _producerCancellationTokenSource.CancelAsync().ConfigureAwait(false);

		_logger.LogInformation("DataProcessor Producer cancellation requested.");
		_logger.LogInformation("Waiting for DataProcessor consumer to finish remaining work...");

		try
		{
			await DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error while disposing DataProcessor on application shutdown.");
		}
	}
}
