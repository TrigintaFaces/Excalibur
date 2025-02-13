using System.Collections.Concurrent;

using Excalibur.Core.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.DataAccess.SqlServer.Cdc;

public class CdcPosition
{
	public CdcPosition(byte[] lsn, byte[]? seqVal, DateTime commitTime)
	{
		LSN = lsn;
		SequenceValue = seqVal;
		CommitTime = commitTime;
	}

	public byte[] LSN { get; init; }
	public byte[]? SequenceValue { get; init; }
	public DateTime CommitTime { get; init; }
}

/// <summary>
///     Processes Change Data Capture (CDC) changes by reading from a database, managing state, and invoking a specified event handler.
/// </summary>
public class CdcProcessor : ICdcProcessor, IDisposable
{
	private readonly IHostApplicationLifetime _appLifetime;
	private readonly IDatabaseConfig _dbConfig;
	private readonly ICdcRepository _cdcRepository;
	private readonly ICdcStateStore _stateStore;
	private readonly ILogger<CdcProcessor> _logger;
	private readonly InMemoryDataQueue<CdcRow> _cdcQueue;
	private readonly ConcurrentDictionary<string, CdcPosition> _tracking = new();
	private int _totalRecords;
	private bool _disposed;
	private bool _producerCompleted;

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

		_appLifetime = appLifetime;
		_dbConfig = dbConfig;
		_cdcRepository = cdcRepository;
		_stateStore = stateStore;
		_logger = logger;
		_cdcQueue = new InMemoryDataQueue<CdcRow>(_dbConfig.QueueSize);

		_ = _appLifetime.ApplicationStopping.Register(OnApplicationStopping);
	}

	/// <summary>
	///     Finalizer for <see cref="CdcProcessor" /> to ensure cleanup.
	/// </summary>
	~CdcProcessor() => Dispose(false);

	/// <summary>
	///     Processes CDC changes asynchronously by producing changes from the database and consuming them with the provided handler.
	/// </summary>
	/// <param name="eventHandler"> A delegate that handles data change events. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The total number of events processed. </returns>
	/// <exception cref="ObjectDisposedException"> Thrown if the instance is already disposed. </exception>
	public async Task<int> ProcessCdcChangesAsync(Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);
		var linkedToken = linkedCts.Token;

		var processingStates = await _stateStore.GetLastProcessedPositionAsync(
			_dbConfig.DatabaseConnectionIdentifier,
			_dbConfig.DatabaseName,
			cancellationToken).ConfigureAwait(false);

		foreach (var processingState in processingStates)
		{
			var lastRow = new CdcPosition(processingState.LastProcessedLsn, processingState.LastProcessedSequenceValue,
				processingState.LastCommitTime);
			_ = _tracking.AddOrUpdate(processingState.TableName, lastRow, (_, _) => lastRow);
		}

		;

		var producerTask = Task.Run(() => ProducerLoop(linkedToken), linkedToken);
		var consumerTask = Task.Run(() => ConsumerLoop(eventHandler, linkedToken), linkedToken);

		await Task.WhenAll(producerTask, consumerTask).ConfigureAwait(false);

		_cdcQueue.Dispose();

		return _totalRecords;
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
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_logger.LogInformation("Disposing CdcProcessor resources.");
			_cdcQueue.Dispose();
		}

		_disposed = true;
	}

	/// <summary>
	///     Determines if a given LSN is empty (contains only zero bytes).
	/// </summary>
	/// <param name="lsn"> The LSN to check. </param>
	/// <returns> <c> true </c> if the LSN is empty; otherwise, <c> false </c>. </returns>
	private static bool IsEmptyLsn(IEnumerable<byte> lsn) => lsn.All(b => b == 0);

	/// <summary>
	///     Converts a byte array to a hexadecimal string representation.
	/// </summary>
	/// <param name="bytes"> The byte array to convert. </param>
	/// <returns> A hexadecimal string representation of the byte array. </returns>
	private static string ByteArrayToHex(byte[] bytes) =>
		$"0x{BitConverter.ToString(bytes).Replace("-", "", StringComparison.OrdinalIgnoreCase)}";

	/// <summary>
	///     Processes CDC rows into data change events.
	/// </summary>
	/// <param name="sortedCdcRows"> The sorted rows of CDC data to process. </param>
	/// <returns> An array of <see cref="DataChangeEvent" /> representing the processed changes. </returns>
	private DataChangeEvent[] GetDataChangeEvents(CdcRow[] sortedCdcRows)
	{
		var i = 0;
		var dataChangeEvents = new List<DataChangeEvent>();

		while (i < sortedCdcRows.Length)
		{
			var change = sortedCdcRows[i];

			switch (change.OperationCode)
			{
				case CdcOperationCodes.UpdateBefore:
					if (i + 1 < sortedCdcRows.Length && sortedCdcRows[i + 1].OperationCode == CdcOperationCodes.UpdateAfter)
					{
						var afterChange = sortedCdcRows[i + 1];
						dataChangeEvents.Add(DataChangeEvent.CreateUpdateEvent(change, afterChange));
						i += 2;
					}
					else
					{
						_logger.LogWarning("Missing UpdateAfter for UpdateBefore at Position {Position}, SequenceValue {SequenceValue}",
							change.Lsn, change.SeqVal);
						i++;
					}

					break;

				case CdcOperationCodes.Insert:
					dataChangeEvents.Add(DataChangeEvent.CreateInsertEvent(change));
					i++;
					break;

				case CdcOperationCodes.Delete:
					dataChangeEvents.Add(DataChangeEvent.CreateDeleteEvent(change));
					i++;
					break;

				case CdcOperationCodes.UpdateAfter:
					_logger.LogWarning(
						"Unexpected UpdateAfter without corresponding UpdateBefore at Position {Position}, SequenceValue {SequenceValue}",
						change.Lsn, change.SeqVal);
					i++;
					break;

				case CdcOperationCodes.Unknown:
				default:
					_logger.LogWarning("Unknown operation {OperationCode} at Position {Position}, SequenceValue {SequenceValue}",
						change.OperationCode, change.Lsn, change.SeqVal);
					i++;
					break;
			}
		}

		return [.. dataChangeEvents];
	}

	/// <summary>
	///     Executes the producer loop that reads CDC changes from the database and enqueues them for consumption.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	private async Task ProducerLoop(CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogDebug("Producer loop started with tracking state for {TableCount} tables", _tracking.Count);

			while (!cancellationToken.IsCancellationRequested)
			{
				if (_cdcQueue.Count >= _dbConfig.QueueSize - _dbConfig.ProducerBatchSize)
				{
					_logger.LogInformation("Queue is almost full. Producer is pausing...");
					await Task.Delay(100, cancellationToken).ConfigureAwait(false);
					continue;
				}

				var maxLsn = await _cdcRepository.GetMaxPositionAsync(cancellationToken).ConfigureAwait(false);
				var allChanges = new List<CdcRow>();
				var processingLastBatch = _dbConfig.CaptureInstances.Length <= 0;

				foreach (var captureInstance in _dbConfig.CaptureInstances)
				{
					var cdcPosition = _tracking.GetValueOrDefault(captureInstance);
					var startProcessingFrom = await GetInitialStartLsn(captureInstance, cdcPosition?.LSN, cancellationToken)
						.ConfigureAwait(false);
					var commitTime = cdcPosition != null && cdcPosition.CommitTime != default
						? cdcPosition.CommitTime
						: await _cdcRepository.GetLsnToTimeAsync(startProcessingFrom, cancellationToken).ConfigureAwait(false);

					if (startProcessingFrom.CompareLsn(maxLsn) > 0)
					{
						_logger.LogDebug("Skipping {CaptureInstance} - No new changes beyond max LSN.", captureInstance);
						continue;
					}

					byte[]? lastSequenceValue = null;
					var (fromLsn, toLsn, toLsnDate) = await GetLsnRange(commitTime.Value, maxLsn, cancellationToken).ConfigureAwait(false);

					if (cdcPosition != null && fromLsn.CompareLsn(cdcPosition.LSN) == 0)
					{
						lastSequenceValue = cdcPosition.SequenceValue;
					}

					_logger.LogInformation(
						"Fetching CDC changes: {TableName} - LSN {FromLsn} to {ToLsn}, SeqVal {SeqVal}", captureInstance,
						ByteArrayToHex(fromLsn), ByteArrayToHex(toLsn),
						lastSequenceValue != null ? ByteArrayToHex(lastSequenceValue) : "null");

					processingLastBatch = toLsn.CompareLsn(maxLsn) == 0;
					var changes = (await _cdcRepository.FetchChangesAsync(
						captureInstance,
						fromLsn,
						toLsn,
						lastSequenceValue,
						cancellationToken).ConfigureAwait(false)).ToArray();

					_logger.LogDebug("Fetched {RowCount} rows for {TableName}", changes.Length, captureInstance);

					if (changes.Length == 0)
					{
						_logger.LogInformation("No changes found for {TableName}, advancing LSN.", captureInstance);

						_ = _tracking.AddOrUpdate(captureInstance, new CdcPosition(toLsn, null, toLsnDate),
							(_, _) => new CdcPosition(toLsn, null, toLsnDate));
					}
					else
					{
						allChanges.AddRange(changes);
					}
				}

				var sortedChanges = allChanges.ToArray();
				Array.Sort(sortedChanges, new CdcRowComparer());

				//sortedChanges = allChanges
				//	.Take(_dbConfig.ProducerBatchSize)
				//	.ToArray();

				if (sortedChanges.Length == 0 && processingLastBatch)
				{
					_logger.LogInformation("No more CDC records. Producer exiting.");
					_producerCompleted = true;
					break;
				}

				_logger.LogInformation("Enqueuing {BatchSize} CDC rows", sortedChanges.Length);

				foreach (var cdcRow in sortedChanges)
				{
					await _cdcQueue.EnqueueAsync(cdcRow, cancellationToken).ConfigureAwait(false);

					var cdcPosition = new CdcPosition(cdcRow.Lsn, cdcRow.SeqVal, cdcRow.CommitTime);
					_ = _tracking.AddOrUpdate(cdcRow.TableName, cdcPosition, (_, _) => cdcPosition);
				}

				_logger.LogInformation("Successfully enqueued {EnqueuedRowCount} CDC rows", sortedChanges.Length);
			}
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
	}

	/// <summary>
	///     Executes the consumer loop that processes CDC events in batches.
	/// </summary>
	/// <param name="eventHandler"> The handler to process data change events. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The total number of events processed. </returns>
	private async Task<int> ConsumerLoop(Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		const int MaxEmptyCycles = 100;
		var batchProcessedCount = 0;
		var buffer = new List<CdcRow>();
		var emptyCycles = 0;

		try
		{
			_logger.LogInformation("CDC Consumer loop started...");

			while (!cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Attempting to dequeue CDC messages...");
				var stopwatch = ValueStopwatch.StartNew();
				var batch = await _cdcQueue.DequeueBatchAsync(_dbConfig.ConsumerBatchSize, cancellationToken).ConfigureAwait(false);
				_logger.LogDebug("Dequeued {BatchSize} messages in {ElapsedMs}ms", batch.Count, stopwatch.Elapsed.TotalMilliseconds);

				if (batch.Count == 0)
				{
					_logger.LogInformation("CDC Queue is empty. Waiting for producer...");

					emptyCycles++;
					await Task.Delay(10, cancellationToken).ConfigureAwait(false);

					if (_producerCompleted && _cdcQueue.IsEmpty() && emptyCycles >= MaxEmptyCycles)
					{
						_logger.LogInformation("No more CDC records. Consumer is exiting.");
						break;
					}

					continue;
				}

				buffer.AddRange(batch);

				batch.Clear();

				_logger.LogInformation("Processing CDC batch of {BatchSize} events", batch.Count);

				var (remainingRows, batchEventCount) = await ProcessLsnChunk(buffer, eventHandler, cancellationToken).ConfigureAwait(false);
				batchProcessedCount += batchEventCount;
				_totalRecords += batchEventCount;

				buffer = remainingRows;

				if (buffer.Count > 0 && buffer.Last().OperationCode == CdcOperationCodes.UpdateBefore)
				{
					_logger.LogDebug("Buffer contains unpaired UpdateBefore.");
				}

				if (buffer.Count > 0)
				{
					batchProcessedCount += await ProcessLsnGroup(buffer, eventHandler, cancellationToken).ConfigureAwait(false);
				}

				_logger.LogInformation("Completed CDC processing, total events processed: {TotalEvents}", batchProcessedCount);

				emptyCycles = 0;
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

		return batchProcessedCount;
	}

	private async Task<(List<CdcRow> remainingRows, int eventCount)> ProcessLsnChunk(
		List<CdcRow> cdcRows,
		Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		var processCount = cdcRows.Count;
		var eventCount = 0;

		// Exclude the last row if it is an UpdateBefore to avoid splitting transactions
		if (processCount > 0 && cdcRows[^1].OperationCode == CdcOperationCodes.UpdateBefore)
		{
			processCount--;
		}

		if (processCount <= 0)
		{
			return (cdcRows, eventCount);
		}

		var safeChunk = cdcRows.Take(processCount).ToList();
		eventCount = await ProcessLsnGroup(safeChunk, eventHandler, cancellationToken).ConfigureAwait(false);

		// Remove processed rows from the original list
		cdcRows.RemoveRange(0, processCount);

		return (cdcRows, eventCount);
	}

	private async Task<int> ProcessLsnGroup(
		List<CdcRow> cdcRows,
		Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		var sortedCdcRows = cdcRows.OrderBy(c => c, new CdcRowComparer()).ToArray();
		var events = GetDataChangeEvents(sortedCdcRows);

		// Call the event handler with the changes
		var eventCount = await eventHandler(events, cancellationToken).ConfigureAwait(false);

		foreach (var group in sortedCdcRows.GroupBy(c => c.TableName))
		{
			var lastRow = group.Last();

			await _stateStore.UpdateLastProcessedPositionAsync(
				_dbConfig.DatabaseConnectionIdentifier,
				_dbConfig.DatabaseName,
				group.Key,
				lastRow.Lsn,
				lastRow.SeqVal,
				lastRow.CommitTime,
				cancellationToken
			).ConfigureAwait(false);

			_logger.LogInformation("Updated state for {TableName} with LSN: {Lsn}, SeqVal: {SeqVal}",
				group.Key, ByteArrayToHex(lastRow.Lsn), ByteArrayToHex(lastRow.SeqVal));
		}

		return eventCount;
	}

	/// <summary>
	///     Retrieves the initial starting LSN for processing CDC changes.
	/// </summary>
	/// <param name="captureInstance"> The table to get the starting point for </param>
	/// <param name="startLsn"> The last processed/enqueued LSN, if any. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The starting LSN as a byte array. </returns>
	/// <exception cref="InvalidOperationException"> Thrown if no valid minimum LSN is found in CDC tables. </exception>
	private async Task<byte[]> GetInitialStartLsn(string captureInstance, byte[]? startLsn, CancellationToken cancellationToken)
	{
		var captureMinLsn = await _cdcRepository.GetMinPositionAsync(captureInstance, cancellationToken).ConfigureAwait(false);

		if (captureMinLsn == null)
		{
			throw new InvalidOperationException("Cannot start processing; no valid minimum LSN found in CDC tables.");
		}

		if (startLsn == null || IsEmptyLsn(startLsn) || startLsn.CompareLsn(captureMinLsn) < 0)
		{
			return captureMinLsn;
		}

		return startLsn;
	}

	/// <summary>
	///     Determines the range of LSNs to process in a single batch.
	/// </summary>
	/// <param name="lastCommitTime"> The last commit time. </param>
	/// <param name="maxLsn"> The maximum LSN. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> A tuple containing the starting LSN, ending LSN, and ending LSN date. </returns>
	private async Task<(byte[] fromLsn, byte[] toLsn, DateTime toLsnDate)> GetLsnRange(DateTime lastCommitTime, byte[] maxLsn,
		CancellationToken cancellationToken)
	{
		var fromLsn = await _cdcRepository.GetTimeToLsnAsync(lastCommitTime, "largest less than or equal", cancellationToken)
			.ConfigureAwait(false);
		ArgumentNullException.ThrowIfNull(fromLsn);

		var fromLsnDate = await _cdcRepository.GetLsnToTimeAsync(fromLsn, cancellationToken).ConfigureAwait(false);
		ArgumentNullException.ThrowIfNull(fromLsnDate);

		// Calculate the 'toLsn' based on batch time interval
		var toLsnDate = fromLsnDate.Value.AddMilliseconds(_dbConfig.BatchTimeInterval);
		var toLsn =
			await _cdcRepository.GetTimeToLsnAsync(toLsnDate, "largest less than or equal", cancellationToken).ConfigureAwait(false) ??
			maxLsn;

		return (fromLsn, toLsn, toLsnDate);
	}

	/// <summary>
	///     Handles cleanup when the application is stopping.
	/// </summary>
	private void OnApplicationStopping()
	{
		_logger.LogInformation("Application is stopping. Ensuring CDC processing cleanup.");
		Dispose();
	}
}
