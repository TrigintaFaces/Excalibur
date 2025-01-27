using Excalibur.Core.Diagnostics;
using Excalibur.DataAccess;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Outbox;

/// <summary>
///     Manages the dispatch of outbox messages using a producer-consumer pattern. Handles the reservation, processing, and cleanup of
///     outbox records.
/// </summary>
public class OutboxManager : IOutboxManager
{
	private const int MaxDegreeOfParallelism = 1;
	private const int BatchSize = 10;

	private readonly IDataQueue<OutboxRecord> _outboxQueue = new InMemoryDataQueue<OutboxRecord>();
	private readonly IOutbox _outbox;
	private readonly ILogger<OutboxManager> _logger;
	private readonly IHostApplicationLifetime _appLifetime;
	private readonly TelemetryClient? _telemetryClient;

	/// <summary>
	///     Initializes a new instance of the <see cref="OutboxManager" /> class.
	/// </summary>
	/// <param name="outbox"> The outbox instance for managing records. </param>
	/// <param name="appLifetime">
	///     An instance of <see cref="IHostApplicationLifetime" /> that allows the application to perform actions during the application's
	///     lifecycle events, such as startup, shutdown, or when the application is stopping. This parameter is used to gracefully manage
	///     tasks that need to respond to application lifecycle events.
	/// </param>
	/// <param name="logger"> The logger instance for logging events. </param>
	/// <param name="telemetryClient">
	///     The Application Insights TelemetryClient used to record metrics, events, and exceptions for monitoring and analysis.
	/// </param>
	public OutboxManager(IOutbox outbox, IHostApplicationLifetime appLifetime, ILogger<OutboxManager> logger,
		TelemetryClient? telemetryClient = null)
	{
		ArgumentNullException.ThrowIfNull(outbox);
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(logger);

		_outbox = outbox;
		_appLifetime = appLifetime;
		_logger = logger;
		_telemetryClient = telemetryClient;
	}

	/// <inheritdoc />
	public async Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken = default)
	{
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);

		await _outbox.TryUnReserveOneRecordsAsync(dispatcherId, linkedCts.Token).ConfigureAwait(false);

		var producerTask = Task.Run(() => ProducerLoop(dispatcherId, linkedCts.Token), linkedCts.Token);

		var consumerTasks = Enumerable.Range(0, MaxDegreeOfParallelism)
			.Select(_ => Task.Run(() => ConsumerLoop(dispatcherId, linkedCts.Token), linkedCts.Token))
			.ToArray();

		await producerTask.ConfigureAwait(false);

		if (_outboxQueue is IDisposable disposable)
		{
			disposable.Dispose();
		}

		var results = await Task.WhenAll(consumerTasks).ConfigureAwait(false);

		return results.Sum();
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
				var batch = await ReserveBatchRecords(dispatcherId, BatchSize, cancellationToken).ConfigureAwait(false);
				var count = batch.Count();

				_telemetryClient?.GetMetric("Outbox.BatchSize").TrackValue(count);

				foreach (var record in batch)
				{
					cancellationToken.ThrowIfCancellationRequested();
					await _outboxQueue.EnqueueAsync(record, cancellationToken).ConfigureAwait(false);
				}

				if (count != 0)
				{
					continue;
				}

				_logger.LogInformation("No outbox records found. Producer is idle.");
				break;
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogDebug("Producer canceled");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in ProducerLoop");
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
		var localCount = 0;
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var batch = await _outboxQueue.DequeueBatchAsync(10, cancellationToken).ConfigureAwait(false);

				foreach (var record in batch)
				{
					var stopwatch = ValueStopwatch.StartNew();
					try
					{
						var result = await _outbox.DispatchReservedRecordAsync(dispatcherId, record).ConfigureAwait(false);
						localCount += result;

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

				if (batch.Count != 0 || _outboxQueue.HasPendingItems())
				{
					continue;
				}

				_logger.LogInformation("Queue is empty. Consumer is idle.");
				break;
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

		_telemetryClient?.GetMetric("Outbox.RecordsProcessed").TrackValue(localCount);

		return localCount;
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
}
