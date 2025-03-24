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
public class OutboxManager : IOutboxManager
{
	private readonly OutboxConfiguration _configuration;

	private readonly InMemoryDataQueue<OutboxRecord> _outboxQueue;

	private readonly IOutbox _outbox;

	private readonly ILogger<OutboxManager> _logger;

	private readonly TelemetryClient? _telemetryClient;

	private readonly CancellationTokenSource _producerCancellationTokenSource = new();

	private readonly OrderedEventProcessor _orderedEventProcessor = new();

	private int _disposedFlag;

	private Task? _producerTask;

	private Task<int>? _consumerTask;

	private volatile bool _producerStopped;

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
	public OutboxManager(
		IOutbox outbox,
		IOptions<OutboxConfiguration> configuration,
		IHostApplicationLifetime appLifetime,
		ILogger<OutboxManager> logger,
		TelemetryClient? telemetryClient = null)
	{
		ArgumentNullException.ThrowIfNull(outbox);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(logger);

		_outbox = outbox;
		_configuration = configuration.Value;
		_logger = logger;
		_telemetryClient = telemetryClient;
		_outboxQueue = new InMemoryDataQueue<OutboxRecord>(_configuration.QueueSize);

		_ = appLifetime.ApplicationStopping.Register(() => Task.Run(OnApplicationStoppingAsync));
	}

	private CancellationToken ProducerCancellationToken => _producerCancellationTokenSource.Token;

	private bool ShouldWaitForProducer => !_producerStopped && !(_producerTask?.IsCompleted ?? true) && _outboxQueue.IsEmpty();

	/// <inheritdoc />
	public async Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		await _outbox.TryUnReserveOneRecordsAsync(dispatcherId, cancellationToken).ConfigureAwait(false);

		_producerTask = Task.Run(() => ProducerLoop(dispatcherId, cancellationToken), cancellationToken);
		_consumerTask = Task.Run(() => ConsumerLoop(dispatcherId, cancellationToken), cancellationToken);

		await _producerTask.ConfigureAwait(false);
		var consumerResult = await _consumerTask.ConfigureAwait(false);

		return consumerResult;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Disposes of resources used by the <see cref="OutboxManager" />.
	/// </summary>
	protected virtual async ValueTask DisposeAsyncCore()
	{
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		_logger.LogInformation("Disposing OutboxManager resources asynchronously.");

		if (_consumerTask is { IsCompleted: false })
		{
			_logger.LogWarning("Disposing OutboxManager but Consumer has not completed.");
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

		await _outboxQueue.DisposeAsync().ConfigureAwait(false);
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

	/// <summary>
	///     The producer loop responsible for reserving batches of outbox records and enqueueing them for processing.
	/// </summary>
	/// <param name="dispatcherId"> The ID of the dispatcher. </param>
	/// <param name="cancellationToken"> A token to observe for cancellation requests. </param>
	private async Task ProducerLoop(string dispatcherId, CancellationToken cancellationToken)
	{
		try
		{
			using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ProducerCancellationToken);
			var combinedToken = combinedTokenSource.Token;

			while (!combinedToken.IsCancellationRequested)
			{
				var availableSlots = Math.Max(0, _configuration.QueueSize - _outboxQueue.Count);
				if (availableSlots < 1)
				{
					await Task.Delay(10, combinedToken).ConfigureAwait(false);
					continue;
				}

				var batchSize = Math.Min(_configuration.ProducerBatchSize, availableSlots);
				var batch =
					await ReserveBatchRecords(dispatcherId, batchSize, combinedToken).ConfigureAwait(false) as ICollection<OutboxRecord>
					?? [];

				if (batch.Count == 0)
				{
					_logger.LogInformation("No more outbox records. Producer exiting.");
					break;
				}

				_telemetryClient?.GetMetric("Outbox.BatchSize").TrackValue(batch.Count);
				_logger.LogInformation("Enqueuing {BatchSize} outbox records", batch.Count);

				foreach (var record in batch)
				{
					await _outboxQueue.EnqueueAsync(record, combinedToken).ConfigureAwait(false);
				}

				_logger.LogInformation("Successfully enqueued {EnqueuedRowCount} outbox records", batch.Count);
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
		finally
		{
			_producerStopped = true;
			_outboxQueue.CompleteWriter();

			_logger.LogInformation("Outbox Producer has completed execution. Channel marked as complete.");
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
		ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherId);

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

				if (_producerStopped && _outboxQueue.IsEmpty())
				{
					_logger.LogInformation("No more Outbox records. Consumer is exiting.");
					break;
				}

				if (ShouldWaitForProducer)
				{
					_logger.LogInformation("Outbox Queue is empty. Waiting for producer...");

					var waitTime = 10;
					for (var i = 0; i < 10; i++)
					{
						await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
						waitTime = Math.Min(waitTime * 2, 100);
					}

					continue;
				}

				var batch = await _outboxQueue.DequeueBatchAsync(_configuration.ConsumerBatchSize, cancellationToken).ConfigureAwait(false);
				_logger.LogInformation("Outbox processing batch of {BatchSize} records", batch.Count);

				foreach (var record in batch)
				{
					var stopwatch = ValueStopwatch.StartNew();

					try
					{
						// Ensures events are dispatched in order. Critical when using Mediator to publish domain events that must be
						// processed sequentially.
						await _orderedEventProcessor.ProcessAsync(async () =>
						{
							_ = await _outbox.DispatchReservedRecordAsync(dispatcherId, record).ConfigureAwait(false);
						}).ConfigureAwait(false);

						_telemetryClient?.TrackTrace(
							$"Dispatched OutboxRecord {record.OutboxId}",
							SeverityLevel.Information,
							new Dictionary<string, string> { ["OutboxId"] = record.OutboxId.ToString(), ["DispatcherId"] = dispatcherId });
					}
					catch (Exception ex)
					{
						_telemetryClient?.TrackException(
							new ExceptionTelemetry(ex) { Message = "Error in Outbox processing", SeverityLevel = SeverityLevel.Error });

						_logger.LogError(
							ex,
							"Error dispatching OutboxRecord with Id {OutboxId} from dispatcher {DispatcherId} in OutboxManager",
							record.OutboxId,
							record.DispatcherId);
					}

					_telemetryClient?.GetMetric("Outbox.ProcessingTime").TrackValue(stopwatch.Elapsed.TotalMilliseconds);
				}

				totalProcessedCount += batch.Count;
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

		_logger.LogInformation("Completed Outbox processing, total events processed: {TotalEvents}", totalProcessedCount);

		_telemetryClient?.GetMetric("Outbox.RecordsProcessed").TrackValue(totalProcessedCount);

		return totalProcessedCount;
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
		var records = await _outbox.TryReserveOneRecordsAsync(dispatcherId, batchSize, cancellationToken).ConfigureAwait(false) ?? [];

		if (!records.Any())
		{
			_logger.LogInformation("Dispatcher {DispatcherId} idle. No outbox record found.", dispatcherId);
		}

		return records;
	}

	private async Task OnApplicationStoppingAsync()
	{
		_logger.LogInformation("Application is stopping. Cancelling OutboxManager producer immediately.");

		_producerStopped = true;
		await _producerCancellationTokenSource.CancelAsync().ConfigureAwait(false);

		_logger.LogInformation("OutboxManager Producer cancellation requested.");
		_logger.LogInformation("Waiting for OutboxManager consumer to finish remaining work...");

		try
		{
			await DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error while disposing OutboxManager on application shutdown.");
		}
	}
}
