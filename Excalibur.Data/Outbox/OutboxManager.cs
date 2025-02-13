using Excalibur.Core.Diagnostics;
using Excalibur.DataAccess;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Outbox;

/// <summary>
///     Manages the dispatch of outbox messages using a producer-consumer pattern. Handles the reservation, processing, and cleanup of
///     outbox records.
/// </summary>
public class OutboxManager : IOutboxManager, IDisposable
{
	private readonly OutboxConfiguration _configuration;
	private readonly InMemoryDataQueue<OutboxRecord> _outboxQueue;
	private readonly IOutbox _outbox;
	private readonly ILogger<OutboxManager> _logger;
	private readonly IHostApplicationLifetime _appLifetime;
	private readonly TelemetryClient? _telemetryClient;
	private int _totalRecords;
	private bool _disposed;
	private bool _producerCompleted;

	/// <summary>
	///     Initializes a new instance of the <see cref="OutboxManager" /> class.
	/// </summary>
	/// <param name="outbox"> The outbox instance for managing records. </param>
	/// <param name="configuration"> The outbox configuration options. </param>
	/// <param name="appLifetime">
	///     An instance of <see cref="IHostApplicationLifetime" /> that allows the application to perform actions during the application's
	///     lifecycle events, such as startup, shutdown, or when the application is stopping. This parameter is used to gracefully manage
	///     tasks that need to respond to application lifecycle events.
	/// </param>
	/// <param name="logger"> The logger instance for logging events. </param>
	/// <param name="telemetryClient">
	///     The Application Insights TelemetryClient used to record metrics, events, and exceptions for monitoring and analysis.
	/// </param>
	public OutboxManager(IOutbox outbox, IOptions<OutboxConfiguration> configuration, IHostApplicationLifetime appLifetime,
		ILogger<OutboxManager> logger,
		TelemetryClient? telemetryClient = null)
	{
		ArgumentNullException.ThrowIfNull(outbox);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(logger);

		_outbox = outbox;
		_configuration = configuration.Value;
		_appLifetime = appLifetime;
		_logger = logger;
		_telemetryClient = telemetryClient;
		_outboxQueue = new InMemoryDataQueue<OutboxRecord>(_configuration.QueueSize);

		_ = _appLifetime.ApplicationStopping.Register(OnApplicationStopping);
	}

	/// <summary>
	///     Finalizer for <see cref="OutboxManager" /> to ensure cleanup.
	/// </summary>
	~OutboxManager() => Dispose(false);

	/// <inheritdoc />
	public async Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);
		var linkedToken = linkedCts.Token;

		await _outbox.TryUnReserveOneRecordsAsync(dispatcherId, linkedToken).ConfigureAwait(false);

		var producerTask = Task.Run(() => ProducerLoop(dispatcherId, linkedToken), linkedToken);
		var consumerTask = Task.Run(() => ConsumerLoop(dispatcherId, linkedToken), linkedToken);

		await Task.WhenAll(producerTask, consumerTask).ConfigureAwait(false);

		_outboxQueue.Dispose();

		return _totalRecords;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Disposes of resources used by the <see cref="OutboxManager" />.
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
			_logger.LogInformation("Disposing OutboxManager resources.");
			_outboxQueue.Dispose();
		}

		_disposed = true;
	}

	/// <summary>
	///     The producer loop responsible for reserving batches of outbox records and enqueueing them for processing.
	/// </summary>
	/// <param name="dispatcherId"> The ID of the dispatcher. </param>
	/// <param name="cancellationToken"> A token to observe for cancellation requests. </param>
	private async Task ProducerLoop(string dispatcherId, CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (_outboxQueue.Count >= _configuration.QueueSize - _configuration.ProducerBatchSize)
				{
					_logger.LogInformation("Outbox Queue is almost full. Producer is pausing...");
					await Task.Delay(100, cancellationToken).ConfigureAwait(false);
					continue;
				}

				var batch = (await ReserveBatchRecords(dispatcherId, _configuration.ProducerBatchSize, cancellationToken)
						.ConfigureAwait(false))
					.ToArray();

				if (batch.Length == 0)
				{
					_logger.LogInformation("No more outbox records. Producer exiting.");
					_producerCompleted = true;
					break;
				}

				_telemetryClient?.GetMetric("Outbox.BatchSize").TrackValue(batch.Length);
				_logger.LogInformation("Enqueuing {BatchSize} outbox records", batch.Length);

				await _outboxQueue.EnqueueBatchAsync(batch, cancellationToken).ConfigureAwait(false);

				_logger.LogInformation("Successfully enqueued {EnqueuedRowCount} outbox records", batch.Length);
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogDebug("Outbox Producer canceled");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in Outbox ProducerLoop");
			throw;
		}
	}

	/// <summary>
	///     The consumer loop responsible for processing records from the queue and dispatching them.
	/// </summary>
	/// <param name="dispatcherId"> The ID of the dispatcher. </param>
	/// <param name="cancellationToken"> A token to observe for cancellation requests. </param>
	/// <returns> The total number of successfully dispatched records. </returns>
	private async Task<int> ConsumerLoop(string dispatcherId, CancellationToken cancellationToken)
	{
		const int MaxEmptyCycles = 100;
		var batchProcessedCount = 0;
		var emptyCycles = 0;

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var batch = await _outboxQueue.DequeueBatchAsync(_configuration.ConsumerBatchSize, cancellationToken).ConfigureAwait(false);

				if (batch.Count == 0)
				{
					_logger.LogInformation("Outbox Queue is empty. Waiting for producer...");

					emptyCycles++;
					await Task.Delay(10, cancellationToken).ConfigureAwait(false);

					if (_producerCompleted && _outboxQueue.IsEmpty() && emptyCycles >= MaxEmptyCycles)
					{
						_logger.LogInformation("No more Outbox records. Consumer is exiting.");
						break;
					}

					continue;
				}

				_logger.LogInformation("Processing Outbox batch of {BatchSize} records", batch.Count);

				foreach (var record in batch)
				{
					var stopwatch = ValueStopwatch.StartNew();
					try
					{
						var result = await _outbox.DispatchReservedRecordAsync(dispatcherId, record).ConfigureAwait(false);
						batchProcessedCount += result;
						_ = Interlocked.Add(ref _totalRecords, result);

						_telemetryClient?.GetMetric("Outbox.ProcessingTime").TrackValue(stopwatch.Elapsed.TotalMilliseconds);
					}
					catch (Exception ex)
					{
						_telemetryClient?.TrackException(new ExceptionTelemetry(ex)
						{
							Message = "Error in Outbox processing",
							SeverityLevel = SeverityLevel.Error
						});

						_logger.LogError(ex, "Error processing record {RecordId} in dispatcher {DispatcherId}", record.OutboxId,
							dispatcherId);
					}
				}

				_logger.LogInformation("Completed batch processing, total records processed so far: {TotalRecords}", _totalRecords);

				emptyCycles = 0;

				batch.Clear();
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogDebug("Consumer canceled normally.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in ConsumerLoop");
			throw;
		}

		_telemetryClient?.GetMetric("Outbox.RecordsProcessed").TrackValue(_totalRecords);

		return batchProcessedCount;
	}

	/// <summary>
	///     Reserves a batch of outbox records for processing.
	/// </summary>
	/// <param name="dispatcherId"> The ID of the dispatcher. </param>
	/// <param name="batchSize"> The number of records to reserve in the batch. </param>
	/// <param name="cancellationToken"> A token to observe for cancellation requests. </param>
	/// <returns> A collection of reserved outbox records. </returns>
	private async Task<IEnumerable<OutboxRecord>> ReserveBatchRecords(
		string dispatcherId,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var records = (await _outbox.TryReserveOneRecordsAsync(dispatcherId, batchSize, cancellationToken)
			.ConfigureAwait(false)).ToArray();

		if (records.Length == 0)
		{
			_logger.LogInformation("Dispatcher {DispatcherId} idle. No outbox record found.", dispatcherId);
		}

		return records;
	}

	private void OnApplicationStopping()
	{
		_logger.LogInformation("Application is stopping. Ensuring Outbox processing cleanup.");
		Dispose();
	}
}
