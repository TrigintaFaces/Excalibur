using Excalibur.DataAccess;

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

	/// <summary>
	///     Initializes a new instance of the <see cref="OutboxManager" /> class.
	/// </summary>
	/// <param name="outbox"> The outbox instance for managing records. </param>
	/// <param name="logger"> The logger instance for logging events. </param>
	public OutboxManager(IOutbox outbox, ILogger<OutboxManager> logger)
	{
		ArgumentNullException.ThrowIfNull(outbox);
		ArgumentNullException.ThrowIfNull(logger);

		_outbox = outbox;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken = default)
	{
		await _outbox.TryUnReserveOneRecordsAsync(dispatcherId, cancellationToken).ConfigureAwait(false);

		var producerTask = Task.Run(() => ProducerLoop(dispatcherId, cancellationToken), cancellationToken);

		var consumerTasks = Enumerable.Range(0, MaxDegreeOfParallelism)
			.Select(_ => Task.Run(() => ConsumerLoop(dispatcherId, cancellationToken), cancellationToken))
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
				var count = 0;

				foreach (var record in batch)
				{
					cancellationToken.ThrowIfCancellationRequested();

					count++;
					await _outboxQueue.EnqueueAsync(record, cancellationToken).ConfigureAwait(false);
				}

				if (count == 0)
				{
					_logger.LogInformation("No outbox records found. Producer is idle.");
					break;
				}
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
			await foreach (var record in _outboxQueue.DequeueAllAsync(cancellationToken).ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();

				var result = await _outbox.DispatchReservedRecordAsync(dispatcherId, record).ConfigureAwait(false);

				localCount += result;
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
