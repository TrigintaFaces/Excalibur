using System.Collections.Concurrent;

using Excalibur.Core.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.DataAccess.SqlServer.Cdc;

public class CdcPosition
{
	public CdcPosition(byte[] lsn, byte[]? seqVal)
	{
		Lsn = lsn;
		SequenceValue = seqVal;
	}

	public byte[] Lsn { get; init; }

	public byte[]? SequenceValue { get; init; }
}

/// <summary>
///     Processes Change Data Capture (CDC) changes by reading from a database, managing state, and invoking a specified event handler.
/// </summary>
public class CdcProcessor : ICdcProcessor
{
	private readonly IDatabaseConfig _dbConfig;

	private readonly ICdcRepository _cdcRepository;

	private readonly ICdcStateStore _stateStore;

	private readonly ILogger<CdcProcessor> _logger;

	private readonly InMemoryDataQueue<CdcRow> _cdcQueue;

	private readonly ConcurrentDictionary<string, CdcPosition> _tracking = new();

	private readonly SemaphoreSlim _queueSpaceAvailable;

	private volatile bool _isFlushing;

	private int _disposedFlag;

	private int _producerCompleted;

	private Task? _producerTask;

	private Task<int>? _consumerTask;

	/// <summary>
	///     Initializes a new instance of the <see cref="CdcProcessor" /> class.
	/// </summary>
	/// <param name="appLifetime"> Provides notifications about application lifetime events. </param>
	/// <param name="dbConfig"> The database configuration for CDC processing. </param>
	/// <param name="cdcRepository"> The repository for querying CDC data. </param>
	/// <param name="stateStore"> The state store for persisting CDC processing progress. </param>
	/// <param name="logger"> The logger used to log diagnostics and operational information. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="appLifetime" /> is null. </exception>
	public CdcProcessor(
		IHostApplicationLifetime appLifetime,
		IDatabaseConfig dbConfig,
		ICdcRepository cdcRepository,
		ICdcStateStore stateStore,
		ILogger<CdcProcessor> logger)
	{
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(cdcRepository);
		ArgumentNullException.ThrowIfNull(stateStore);
		ArgumentNullException.ThrowIfNull(logger);

		_dbConfig = dbConfig;
		_cdcRepository = cdcRepository;
		_stateStore = stateStore;
		_logger = logger;
		_cdcQueue = new InMemoryDataQueue<CdcRow>(_dbConfig.QueueSize);
		_queueSpaceAvailable = new SemaphoreSlim(_dbConfig.QueueSize, _dbConfig.QueueSize);

		_ = appLifetime.ApplicationStopping.Register(OnApplicationStopping);
	}

	/// <summary>
	///     Processes CDC changes asynchronously by producing changes from the database and consuming them with the provided handler.
	/// </summary>
	/// <param name="eventHandler"> A delegate that handles data change events. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The total number of events processed. </returns>
	/// <exception cref="ObjectDisposedException"> Thrown if the instance is already disposed. </exception>
	public async Task<int> ProcessCdcChangesAsync(
		Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		var lowestStartLsn = await InitializeTrackingAsync(cancellationToken).ConfigureAwait(false);

		if (lowestStartLsn == null)
		{
			throw new InvalidOperationException("Cannot start processing; no valid minimum LSN found in CDC tables.");
		}

		_isFlushing = false;
		_producerTask = Task.Run(() => ProducerLoop(lowestStartLsn, cancellationToken), cancellationToken);
		_consumerTask = Task.Run(() => ConsumerLoop(eventHandler, cancellationToken), cancellationToken);

		var consumerResult = await _consumerTask.ConfigureAwait(false);
		await _producerTask.ConfigureAwait(false);

		return consumerResult;
	}

	public async Task FlushAsync()
	{
		if (_disposedFlag == 1)
		{
			return;
		}

		_isFlushing = true;
		_ = Interlocked.Exchange(ref _producerCompleted, 1);

		if (_producerTask != null)
		{
			try
			{
				await _producerTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error finishing ProducerLoop in FlushAsync.");
				throw;
			}
		}

		if (_consumerTask != null)
		{
			try
			{
				_ = await _consumerTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error finishing ConsumerLoop in FlushAsync.");
				throw;
			}
		}

		_isFlushing = false;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Disposes of resources used by the <see cref="CdcProcessor" />.
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
			_logger.LogInformation("Disposing CdcProcessor resources.");
			_tracking.Clear();
			_cdcQueue.Dispose();
			_queueSpaceAvailable.Dispose();
		}
	}

	/// <summary>
	///     Determines if a given LSN is empty (contains only zero bytes).
	/// </summary>
	/// <param name="lsn"> The LSN to check. </param>
	/// <returns> <c> true </c> if the LSN is empty; otherwise, <c> false </c>. </returns>
	private static bool IsEmptyLsn(IEnumerable<byte> lsn) => lsn.All((byte b) => b == 0);

	/// <summary>
	///     Converts a byte array to a hexadecimal string representation.
	/// </summary>
	/// <param name="bytes"> The byte array to convert. </param>
	/// <returns> A hexadecimal string representation of the byte array. </returns>
	private static string ByteArrayToHex(byte[] bytes) =>
		$"0x{BitConverter.ToString(bytes).Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)}";

	/// <summary>
	///     Processes CDC rows into data change events.
	/// </summary>
	/// <param name="cdcRows"> The rows of CDC data to process. </param>
	/// <returns> An array of <see cref="DataChangeEvent" /> representing the processed changes. </returns>
	private DataChangeEvent[] GetDataChangeEvents(IList<CdcRow> cdcRows)
	{
		if (cdcRows == null || cdcRows.Count == 0)
		{
			_logger.LogWarning("GetDataChangeEvents received a null or empty cdcRows array.");
			return [];
		}

		var dataChangeEvents = new DataChangeEvent[cdcRows.Count];
		var index = 0;

		for (var i = 0; i < cdcRows.Count; i++)
		{
			var cdcRow = cdcRows[i];

			if (cdcRow is null)
			{
				_logger.LogWarning("Skipping null CdcRow in GetDataChangeEvents.");
				continue;
			}

			switch (cdcRow.OperationCode)
			{
				case CdcOperationCodes.UpdateBefore:
					var updateAfterRow = cdcRows[i + 1]?.OperationCode == CdcOperationCodes.UpdateAfter ? cdcRows[i + 1] : null;
					if (i + 1 < cdcRows.Count && updateAfterRow != null)
					{
						dataChangeEvents[index++] = DataChangeEvent.CreateUpdateEvent(cdcRow, updateAfterRow);
						i++;
					}
					else
					{
						_logger.LogWarning(
							"Missing UpdateAfter for UpdateBefore at Position {Position}, SequenceValue {SequenceValue}",
							cdcRow.Lsn,
							cdcRow.SeqVal);
					}

					break;

				case CdcOperationCodes.Insert:
					dataChangeEvents[index++] = DataChangeEvent.CreateInsertEvent(cdcRow);
					break;

				case CdcOperationCodes.Delete:
					dataChangeEvents[index++] = DataChangeEvent.CreateDeleteEvent(cdcRow);
					break;

				case CdcOperationCodes.UpdateAfter:
					_logger.LogWarning(
						"Unexpected UpdateAfter without corresponding UpdateBefore at Position {Position}, SequenceValue {SequenceValue}",
						cdcRow.Lsn,
						cdcRow.SeqVal);
					break;

				case CdcOperationCodes.Unknown:
				default:
					_logger.LogWarning(
						"Unknown operation {OperationCode} at Position {Position}, SequenceValue {SequenceValue}",
						cdcRow.OperationCode,
						cdcRow.Lsn,
						cdcRow.SeqVal);
					break;
			}
		}

		return dataChangeEvents;
	}

	/// <summary>
	///     Executes the producer loop that reads CDC changes from the database and enqueues them for consumption.
	/// </summary>
	/// <param name="lowestStartLsn"> The lowest LSN from all the configured capture instances. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	private async Task ProducerLoop(byte[] lowestStartLsn, CancellationToken cancellationToken)
	{
		try
		{
			var currentGlobalLsn = lowestStartLsn;
			var maxLsn = await _cdcRepository.GetMaxPositionAsync(cancellationToken).ConfigureAwait(false);
			_logger.LogDebug("Producer loop started with tracking state for {TableCount} tables", _tracking.Count);

			while (currentGlobalLsn.CompareLsn(maxLsn) <= 0)
			{
				if (_producerCompleted == 1)
				{
					_logger.LogInformation("ProducerLoop: _producerCompleted == 1, exiting early.");
					break;
				}

				cancellationToken.ThrowIfCancellationRequested();

				foreach (var captureInstance in _dbConfig.CaptureInstances)
				{
					var tableTracking = _tracking[captureInstance];

					if (tableTracking.Lsn.CompareLsn(currentGlobalLsn) == 0)
					{
						var totalRowsReadInThisLsn = 0;

						while (!cancellationToken.IsCancellationRequested)
						{
							_logger.LogDebug(
								"Fetching CDC changes: {TableName} - LSN {Lsn}, SeqVal {SeqVal}",
								captureInstance,
								ByteArrayToHex(tableTracking.Lsn),
								tableTracking.SequenceValue != null ? ByteArrayToHex(tableTracking.SequenceValue) : "null");

							var changes = await _cdcRepository.FetchChangesAsync(
											  captureInstance,
											  _dbConfig.ProducerBatchSize,
											  tableTracking.Lsn,
											  tableTracking.SequenceValue,
											  cancellationToken).ConfigureAwait(false) as IList<CdcRow> ?? [];

							if (changes.Count == 0)
							{
								break;
							}

							totalRowsReadInThisLsn += changes.Count;

							foreach (var record in changes)
							{
								await _queueSpaceAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
								await _cdcQueue.EnqueueAsync(record, cancellationToken).ConfigureAwait(false);
							}

							var lastRow = changes[^1];
							_tracking[captureInstance] = new CdcPosition(lastRow.Lsn, lastRow.SeqVal);

							if (changes.Count < _dbConfig.ProducerBatchSize)
							{
								break;
							}
						}

						if (totalRowsReadInThisLsn > 0)
						{
							_logger.LogDebug(
								"Successfully enqueued {EnqueuedRowCount} CDC rows for {TableName}, advancing LSN.",
								totalRowsReadInThisLsn,
								captureInstance);
						}
						else
						{
							_logger.LogInformation("No changes found for {TableName}, advancing LSN.", captureInstance);
						}

						var nextLsn = await _cdcRepository.GetNextLsnAsync(captureInstance, tableTracking.Lsn, cancellationToken)
										  .ConfigureAwait(false);

						_logger.LogDebug(
							"Table {CaptureInstance}: currentLSN={CurrentLsn}, nextLSN={NextLsn}, maxLSN={MaxLsn}",
							captureInstance,
							ByteArrayToHex(tableTracking.Lsn),
							nextLsn != null ? ByteArrayToHex(nextLsn) : "null",
							ByteArrayToHex(maxLsn));

						if (nextLsn != null && nextLsn.CompareLsn(maxLsn) < 0)
						{
							_tracking[captureInstance] = new CdcPosition(nextLsn, null);
						}
					}
				}

				if (_producerCompleted == 1)
				{
					_logger.LogInformation("ProducerLoop: _producerCompleted == 1, exiting after capturing some tables.");
					break;
				}

				currentGlobalLsn = await _cdcRepository.GetNextLsnAsync(currentGlobalLsn, cancellationToken).ConfigureAwait(false);
			}

			_logger.LogInformation("No more CDC records. Producer exiting.");
			_ = Interlocked.Exchange(ref _producerCompleted, 1);
		}
		catch (OperationCanceledException)
		{
			_logger.LogDebug("CdcProcessor Producer canceled");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in CdcProcessor ProducerLoop");
			throw;
		}

		if (_producerCompleted == 1)
		{
			_logger.LogInformation("CdcProcessor Producer has completed execution.");
		}
	}

	/// <summary>
	///     Executes the consumer loop that processes CDC events in batches.
	/// </summary>
	/// <param name="eventHandler"> The handler to process data change events. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The total number of events processed. </returns>
	private async Task<int> ConsumerLoop(
		Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventHandler);

		_logger.LogInformation("CDC Consumer loop started...");

		var totalProcessedCount = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			if (_disposedFlag == 1)
			{
				_logger.LogWarning("ConsumerLoop: disposal requested, exit now.");
				break;
			}

			if (_producerCompleted == 1 && _cdcQueue.IsEmpty())
			{
				_logger.LogInformation("No more CDC records. Consumer is exiting gracefully.");
				break;
			}

			if (_producerCompleted == 0 && _cdcQueue.IsEmpty())
			{
				_logger.LogInformation("CDC Queue is empty. Waiting for producer...");

				var waitTime = 10;
				for (var i = 0; i < 10; i++)
				{
					if (!_cdcQueue.IsEmpty())
					{
						break;
					}

					await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
					waitTime = Math.Min(waitTime * 2, 100);
				}

				continue;
			}

			try
			{
				_logger.LogDebug("Attempting to dequeue CDC messages...");
				var stopwatch = ValueStopwatch.StartNew();

				var batchList = new List<CdcRow>(_dbConfig.ConsumerBatchSize);
				await foreach (var cdcRow in _cdcQueue.DequeueAllAsync(cancellationToken).ConfigureAwait(false))
				{
					if (cdcRow is null)
					{
						continue;
					}

					if (_queueSpaceAvailable.CurrentCount < _dbConfig.QueueSize)
					{
						_ = _queueSpaceAvailable.Release();
					}

					if (batchList.Count < _dbConfig.ConsumerBatchSize)
					{
						batchList.Add(cdcRow);
					}
					else if (cdcRow.OperationCode != CdcOperationCodes.UpdateBefore)
					{
						batchList.Add(cdcRow);

						totalProcessedCount += await CompleteBatch(batchList, stopwatch).ConfigureAwait(false);
						batchList.Clear();
					}
					else
					{
						totalProcessedCount += await CompleteBatch(batchList, stopwatch).ConfigureAwait(false);
						batchList.Clear();

						batchList.Add(cdcRow);
					}
				}

				if (batchList.Count > 0)
				{
					totalProcessedCount += await CompleteBatch(batchList, stopwatch).ConfigureAwait(false);
					batchList.Clear();
				}

				batchList = null;
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
		}

		_logger.LogInformation("Completed CDC processing, total events processed: {TotalEvents}", totalProcessedCount);

		return totalProcessedCount;

		async Task<int> CompleteBatch(IList<CdcRow> batch, ValueStopwatch stopwatch)
		{
			_logger.LogDebug("Processing batch of {BatchSize} CDC records", batch.Count);

			await ProcessBatch(batch, eventHandler, cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Processed {BatchSize} CDC records in {ElapsedMs}ms", batch.Count, stopwatch.Elapsed.TotalMilliseconds);

			return batch.Count;
		}
	}

	private async Task ProcessBatch(
		IList<CdcRow> batch,
		Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(batch);
		ArgumentNullException.ThrowIfNull(eventHandler);

		var events = GetDataChangeEvents(batch);
		_ = await eventHandler(events, cancellationToken).ConfigureAwait(false);
		Array.Clear(events);

		var updateTasks = new List<Task>();

		for (var i = 0; i < batch.Count; i++)
		{
			var currentRow = batch[i];

			if (currentRow is null)
			{
				continue;
			}

			try
			{
				if (i == batch.Count - 1 || batch[i + 1].TableName != currentRow.TableName)
				{
					updateTasks.Add(
						_stateStore.UpdateLastProcessedPositionAsync(
							_dbConfig.DatabaseConnectionIdentifier,
							_dbConfig.DatabaseName,
							currentRow.TableName,
							currentRow.Lsn,
							currentRow.SeqVal,
							currentRow.CommitTime,
							cancellationToken));

					_logger.LogInformation("Updated state for {TableName}", currentRow.TableName);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in updating the last processed position for {TableName}", currentRow.TableName);
				throw;
			}
			finally
			{
				currentRow.Dispose();
			}
		}

		await Task.WhenAll(updateTasks).ConfigureAwait(false);
		updateTasks.Clear();
	}

	private async Task<byte[]?> InitializeTrackingAsync(CancellationToken cancellationToken)
	{
		byte[]? lowestStartLsn = null;

		var processingStates = await _stateStore.GetLastProcessedPositionAsync(
								   _dbConfig.DatabaseConnectionIdentifier,
								   _dbConfig.DatabaseName,
								   cancellationToken).ConfigureAwait(false) as ICollection<CdcProcessingState> ?? [];

		foreach (var captureInstance in _dbConfig.CaptureInstances)
		{
			var state = processingStates.FirstOrDefault(
				(CdcProcessingState x) => x.TableName.Equals(captureInstance, StringComparison.OrdinalIgnoreCase));

			byte[] startLsn;
			byte[]? seqVal = null;

			if (state == null)
			{
				startLsn = await _cdcRepository.GetMinPositionAsync(captureInstance, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				startLsn = state.LastProcessedLsn;
				seqVal = state.LastProcessedSequenceValue;

				if (IsEmptyLsn(startLsn))
				{
					startLsn = await _cdcRepository.GetMinPositionAsync(captureInstance, cancellationToken).ConfigureAwait(false);
				}
			}

			_tracking[captureInstance] = new CdcPosition(startLsn, seqVal);

			if (lowestStartLsn == null || startLsn.CompareLsn(lowestStartLsn) < 0)
			{
				lowestStartLsn = startLsn;
			}
		}

		_logger.LogDebug("Starting new run at LSN {lowestStartLsn}", lowestStartLsn);

		return lowestStartLsn;
	}

	/// <summary>
	///     Handles cleanup when the application is stopping.
	/// </summary>
	private async void OnApplicationStopping()
	{
		try
		{
			if (!_isFlushing)
			{
				_logger.LogInformation("Application is stopping. Attempting a graceful flush.");

				await FlushAsync().ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error while gracefully flushing CDC queue on shutdown.");
		}
		finally
		{
			Dispose();
		}
	}
}
