using System.Numerics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Processes Change Data Capture (CDC) changes by reading from a database, managing state, and invoking a specified event handler.
/// </summary>
public class CdcProcessor : ICdcProcessor, IDisposable
{
	private readonly InMemoryDataQueue<CdcRow> _cdcQueue;
	private readonly IDatabaseConfig _dbConfig;
	private readonly ICdcRepository _cdcRepository;
	private readonly ICdcStateStore _stateStore;
	private readonly IHostApplicationLifetime _appLifetime;
	private readonly ILogger<CdcProcessor> _logger;
	private bool _disposed;

	/// <summary>
	///     Initializes a new instance of the <see cref="CdcProcessor" /> class.
	/// </summary>
	/// <param name="dbConfig"> The database configuration for CDC processing. </param>
	/// <param name="cdcRepository"> The repository for querying CDC data. </param>
	/// <param name="stateStore"> The state store for persisting CDC processing progress. </param>
	/// <param name="appLifetime"> Provides notifications about application lifetime events. </param>
	/// <param name="logger"> The logger used to log diagnostics and operational information. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="appLifetime" /> is null. </exception>
	public CdcProcessor(
		IDatabaseConfig dbConfig,
		ICdcRepository cdcRepository,
		ICdcStateStore stateStore,
		IHostApplicationLifetime appLifetime,
		ILogger<CdcProcessor> logger)
	{
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(cdcRepository);
		ArgumentNullException.ThrowIfNull(stateStore);
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(logger);

		_dbConfig = dbConfig;
		_cdcRepository = cdcRepository;
		_stateStore = stateStore;
		_appLifetime = appLifetime;
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

		var processingState = await _stateStore.GetLastProcessedPositionAsync(
			_dbConfig.DatabaseConnectionIdentifier,
			_dbConfig.DatabaseName,
			linkedToken).ConfigureAwait(false) ?? new CdcProcessingState();

		var maxLsn = await _cdcRepository.GetMaxPositionAsync(linkedToken).ConfigureAwait(false);

		var producerTask = Task.Run(() => ProducerLoop(processingState, maxLsn, linkedToken), linkedToken);

		var consumerTask = Task.Run(() => ConsumerLoop(eventHandler, linkedToken), linkedToken);

		await producerTask.ConfigureAwait(false);

		if (_cdcQueue is IDisposable disposable)
		{
			disposable.Dispose();
		}

		var totalEvents = await consumerTask.ConfigureAwait(false);

		return totalEvents;
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
	///     Compares two Log Sequence Numbers (LSNs) as byte arrays.
	/// </summary>
	/// <param name="lsn1"> The first LSN. </param>
	/// <param name="lsn2"> The second LSN. </param>
	/// <returns> An integer indicating the relative order of the two LSNs. </returns>
	private static int CompareLsn(byte[] lsn1, byte[] lsn2)
	{
		var lsnInt1 = new BigInteger(lsn1.Reverse().ToArray());
		var lsnInt2 = new BigInteger(lsn2.Reverse().ToArray());

		return lsnInt1.CompareTo(lsnInt2);
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
	/// <param name="cdcRows"> The rows of CDC data to process. </param>
	/// <returns> An array of <see cref="DataChangeEvent" /> representing the processed changes. </returns>
	private DataChangeEvent[] GetDataChangeEvents(IEnumerable<CdcRow> cdcRows)
	{
		var operations = cdcRows.OrderBy(c => new BigInteger(c.SeqVal.Reverse().ToArray())).ToList();
		var i = 0;
		var dataChangeEvents = new List<DataChangeEvent>();

		while (i < operations.Count)
		{
			var change = operations[i];

			switch (change.OperationCode)
			{
				case CdcOperationCodes.UpdateBefore:
					if (i + 1 < operations.Count && operations[i + 1].OperationCode == CdcOperationCodes.UpdateAfter)
					{
						var afterChange = operations[i + 1];
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
	/// <param name="processingState"> The current CDC processing state. </param>
	/// <param name="maxLsn"> The maximum LSN to process up to. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	private async Task ProducerLoop(CdcProcessingState processingState, byte[] maxLsn, CancellationToken cancellationToken)
	{
		try
		{
			var startProcessingFrom = await GetInitialStartLsn(processingState.LastProcessedLsn, cancellationToken).ConfigureAwait(false);
			var commitTime = processingState.LastCommitTime != default
				? processingState.LastCommitTime
				: await _cdcRepository.GetLsnToTimeAsync(startProcessingFrom, cancellationToken).ConfigureAwait(false);

			ArgumentNullException.ThrowIfNull(commitTime);

			while (CompareLsn(startProcessingFrom, maxLsn) < 0 && !cancellationToken.IsCancellationRequested)
			{
				var (fromLsn, toLsn, toLsnDate) = await GetLsnRange(commitTime.Value, maxLsn, cancellationToken).ConfigureAwait(false);

				_logger.LogDebug("Producer chunk from LSN {From} to {To}", ByteArrayToHex(fromLsn), ByteArrayToHex(toLsn));

				var cdcRowCount = 0;
				await foreach (var cdcRow in _cdcRepository.FetchChangesAsync(
								   fromLsn,
								   toLsn,
								   processingState.LastProcessedSequenceValue,
								   _dbConfig.ProducerBatchSize,
								   _dbConfig.CaptureInstances,
								   cancellationToken).ConfigureAwait(false))
				{
					cancellationToken.ThrowIfCancellationRequested();

					await _cdcQueue.EnqueueAsync(cdcRow, cancellationToken).ConfigureAwait(false);
					cdcRowCount++;
				}

				_logger.LogInformation("Enqueued {Count} CDC rows for range {From} - {To}", cdcRowCount, ByteArrayToHex(fromLsn),
					ByteArrayToHex(toLsn));

				startProcessingFrom = toLsn;
				commitTime = toLsnDate;

				if (cdcRowCount == 0)
				{
					break;
				}
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
		var totalEvents = 0;
		var buffer = new List<CdcRow>();

		try
		{
			await foreach (var batch in _cdcQueue.DequeueAllInBatchesAsync(_dbConfig.ConsumerBatchSize, cancellationToken)
							   .ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();

				buffer.AddRange(batch);

				var (remainingRows, batchEventCount) = await ProcessLsnChunk(buffer, eventHandler, cancellationToken).ConfigureAwait(false);
				buffer = remainingRows;

				totalEvents += batchEventCount;

				if (buffer.Count > 0 && buffer.Last().OperationCode == CdcOperationCodes.UpdateBefore)
				{
					_logger.LogDebug("Buffer contains unpaired UpdateBefore.");
				}
			}

			if (buffer.Count > 0)
			{
				totalEvents += await ProcessLsnGroup(buffer, eventHandler, cancellationToken).ConfigureAwait(false);
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
		var events = GetDataChangeEvents(cdcRows);

		// Call the event handler with the changes
		var eventCount = await eventHandler(events, cancellationToken).ConfigureAwait(false);

		// Update the state store with the last processed LSN
		var lastRow = cdcRows[^1];
		await _stateStore.UpdateLastProcessedPositionAsync(
			_dbConfig.DatabaseConnectionIdentifier,
			_dbConfig.DatabaseName,
			lastRow.Lsn,
			lastRow.SeqVal,
			lastRow.CommitTime,
			cancellationToken).ConfigureAwait(false);

		_logger.LogInformation("Updated state store with LSN: {Lsn}, SeqVal: {SeqVal}",
			ByteArrayToHex(lastRow.Lsn), ByteArrayToHex(lastRow.SeqVal));

		return eventCount;
	}

	/// <summary>
	///     Retrieves the initial starting LSN for processing CDC changes.
	/// </summary>
	/// <param name="startLsn"> The last processed LSN, if any. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The starting LSN as a byte array. </returns>
	/// <exception cref="InvalidOperationException"> Thrown if no valid minimum LSN is found in CDC tables. </exception>
	private async Task<byte[]> GetInitialStartLsn(byte[]? startLsn, CancellationToken cancellationToken)
	{
		byte[]? captureMinLsn = null;
		// Find the lowest LSN across all capture instances
		foreach (var captureInstance in _dbConfig.CaptureInstances)
		{
			var minPos = await _cdcRepository.GetMinPositionAsync(captureInstance, cancellationToken).ConfigureAwait(false);
			if (captureMinLsn == null || CompareLsn(minPos, captureMinLsn) < 0)
			{
				captureMinLsn = minPos;
			}
		}

		if (captureMinLsn == null)
		{
			throw new InvalidOperationException("Cannot start processing; no valid minimum LSN found in CDC tables.");
		}

		if (startLsn == null || IsEmptyLsn(startLsn) || CompareLsn(startLsn, captureMinLsn) < 0)
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
