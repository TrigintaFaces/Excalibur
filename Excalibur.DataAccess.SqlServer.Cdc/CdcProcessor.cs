using System.Buffers;
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

	private readonly SemaphoreSlim _queueSpaceAvailable;

	private int _disposedFlag;

	private int _producerCompleted;

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
		_queueSpaceAvailable = new SemaphoreSlim(_dbConfig.QueueSize, _dbConfig.QueueSize);

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
	public async Task<int> ProcessCdcChangesAsync(
		Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);
		var linkedToken = linkedCts.Token;

		var processingStates = await _stateStore.GetLastProcessedPositionAsync(
								   _dbConfig.DatabaseConnectionIdentifier,
								   _dbConfig.DatabaseName,
								   cancellationToken).ConfigureAwait(false);

		foreach (var processingState in processingStates)
		{
			var lastRow = new CdcPosition(
				processingState.LastProcessedLsn,
				processingState.LastProcessedSequenceValue,
				processingState.LastCommitTime);
			_ = _tracking.AddOrUpdate(processingState.TableName, lastRow, (string _, CdcPosition _) => lastRow);
		}

		var producerTask = Task.Run(() => ProducerLoop(linkedToken), linkedToken);
		var consumerTasks = Enumerable.Range(0, 1)
			.Select((int _) => Task.Run(() => ConsumerLoop(eventHandler, cancellationToken), cancellationToken)).ToArray();

		await producerTask.ConfigureAwait(false);
		var results = await Task.WhenAll(consumerTasks).ConfigureAwait(false);

		_cdcQueue.Dispose();
		_queueSpaceAvailable.Dispose();
		await linkedCts.CancelAsync().ConfigureAwait(false);

		return results.Sum();
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
	/// <param name="sortedCdcRows"> The sorted rows of CDC data to process. </param>
	/// <returns> An array of <see cref="DataChangeEvent" /> representing the processed changes. </returns>
	private DataChangeEvent[] GetDataChangeEvents(CdcRow[] sortedCdcRows)
	{
		if (sortedCdcRows == null || sortedCdcRows.Length == 0)
		{
			_logger.LogWarning("GetDataChangeEvents received a null or empty sortedCdcRows array.");
			return [];
		}

		Array.Sort(sortedCdcRows, new CdcRowComparer());

		var dataChangeEvents = new DataChangeEvent[sortedCdcRows.Length];
		var index = 0;

		for (var i = 0; i < sortedCdcRows.Length; i++)
		{
			var cdcRow = sortedCdcRows[i];

			if (cdcRow is null)
			{
				_logger.LogWarning("Skipping null CdcRow in GetDataChangeEvents.");
				continue;
			}

			switch (cdcRow.OperationCode)
			{
				case CdcOperationCodes.UpdateBefore:
					if (i + 1 < sortedCdcRows.Length && sortedCdcRows[i + 1].OperationCode == CdcOperationCodes.UpdateAfter)
					{
						dataChangeEvents[index++] = DataChangeEvent.CreateUpdateEvent(cdcRow, sortedCdcRows[i + 1]);
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
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	private async Task ProducerLoop(CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogDebug("Producer loop started with tracking state for {TableCount} tables", _tracking.Count);

			while (!cancellationToken.IsCancellationRequested)
			{
				var maxLsn = await _cdcRepository.GetMaxPositionAsync(cancellationToken).ConfigureAwait(false);
				var processingLastBatch = _dbConfig.CaptureInstances.Length <= 0;

				foreach (var captureInstance in _dbConfig.CaptureInstances)
				{
					var cdcPosition = _tracking.GetValueOrDefault(captureInstance);
					var startProcessingFrom = await GetInitialStartLsn(captureInstance, cdcPosition?.LSN, cancellationToken)
												  .ConfigureAwait(false);
					var commitTime = cdcPosition != null && cdcPosition.CommitTime != default
										 ? cdcPosition.CommitTime
										 : await _cdcRepository.GetLsnToTimeAsync(startProcessingFrom, cancellationToken)
											   .ConfigureAwait(false);

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
						"Fetching CDC changes: {TableName} - LSN {FromLsn} to {ToLsn}, SeqVal {SeqVal}",
						captureInstance,
						ByteArrayToHex(fromLsn),
						ByteArrayToHex(toLsn),
						lastSequenceValue != null ? ByteArrayToHex(lastSequenceValue) : "null");

					processingLastBatch = toLsn.CompareLsn(maxLsn) == 0;
					var changes = await _cdcRepository.FetchChangesAsync(
									  captureInstance,
									  fromLsn,
									  toLsn,
									  lastSequenceValue,
									  cancellationToken).ConfigureAwait(false);

					_logger.LogDebug("Fetched {RowCount} rows for {TableName}", changes.Count(), captureInstance);

					if (!changes.Any())
					{
						_logger.LogInformation("No changes found for {TableName}, advancing LSN.", captureInstance);

						var noChangesPosition = new CdcPosition(toLsn, null, toLsnDate);
						_ = _tracking.AddOrUpdate(captureInstance, noChangesPosition, (string _, CdcPosition _) => noChangesPosition);
					}
					else
					{
						foreach (var record in changes)
						{
							cancellationToken.ThrowIfCancellationRequested();

							await _queueSpaceAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
							await _cdcQueue.EnqueueAsync(record, cancellationToken).ConfigureAwait(false);
						}

						var lastRow = changes.Last();
						var lastRowPosition = new CdcPosition(lastRow.Lsn, lastRow.SeqVal, lastRow.CommitTime);

						_ = _tracking.AddOrUpdate(captureInstance, lastRowPosition, (string _, CdcPosition _) => lastRowPosition);

						_logger.LogInformation(
							"Successfully enqueued {EnqueuedRowCount} CDC rows for {TableName}",
							changes.Count(),
							captureInstance);
					}
				}

				if (processingLastBatch && _cdcQueue.IsEmpty())
				{
					_logger.LogInformation("No more CDC records. Producer exiting.");
					_ = Interlocked.Exchange(ref _producerCompleted, 1);
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

		var totalProcessedCount = 0;
		var batchOffset = 0;
		var batch = ArrayPool<CdcRow>.Shared.Rent(_dbConfig.ConsumerBatchSize + 1);

		try
		{
			_logger.LogInformation("CDC Consumer loop started...");

			while (!cancellationToken.IsCancellationRequested)
			{
				if (_producerCompleted == 1 && _cdcQueue.IsEmpty())
				{
					_logger.LogInformation("No more CDC records. Consumer is exiting.");
					break;
				}

				_logger.LogDebug("Attempting to dequeue CDC messages...");
				var stopwatch = ValueStopwatch.StartNew();
				var batchMemory = await _cdcQueue.DequeueBatchAsync(_dbConfig.ConsumerBatchSize, cancellationToken).ConfigureAwait(false);
				_logger.LogDebug("Dequeued {BatchSize} messages in {ElapsedMs}ms", batchMemory.Length, stopwatch.Elapsed.TotalMilliseconds);

				if (_producerCompleted == 0 && batchMemory.IsEmpty)
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

				for (var i = 0; i < batchMemory.Length; i++)
				{
					var cdcRow = batchMemory.Span[i];

					if (cdcRow is null)
					{
						continue;
					}

					if (i + batchOffset == _dbConfig.ConsumerBatchSize)
					{
						if (cdcRow.OperationCode == CdcOperationCodes.UpdateBefore)
						{
							await ProcessBatch(batch, eventHandler, cancellationToken).ConfigureAwait(false);
							Array.Clear(batch, 0, _dbConfig.ConsumerBatchSize);

							batch[0] = cdcRow;
							batchOffset = 1;
						}
						else
						{
							await ProcessBatch(batch, eventHandler, cancellationToken).ConfigureAwait(false);
							Array.Clear(batch, 0, _dbConfig.ConsumerBatchSize);
							batchOffset = 0;
						}
					}
					else
					{
						batch[i + batchOffset] = cdcRow;
					}
				}

				totalProcessedCount += batch.Length;

				_logger.LogDebug("Completed CDC batch of {BatchSize} events", batch.Length);

				GC.Collect();
				GC.WaitForPendingFinalizers();
			}

			if (batch.Length > 0)
			{
				await ProcessBatch(batch, eventHandler, cancellationToken).ConfigureAwait(false);
				Array.Clear(batch, 0, _dbConfig.ConsumerBatchSize);
			}

			ArrayPool<CdcRow>.Shared.Return(batch, clearArray: true);

			_logger.LogInformation("Completed CDC processing, total events processed: {TotalEvents}", totalProcessedCount);
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
		finally
		{
			ArrayPool<CdcRow>.Shared.Return(batch, clearArray: true);
		}

		return totalProcessedCount;
	}

	private async Task ProcessBatch(
		CdcRow[] batch,
		Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(batch);
		ArgumentNullException.ThrowIfNull(eventHandler);

		var events = GetDataChangeEvents(batch);
		_ = await eventHandler(events, cancellationToken).ConfigureAwait(false);

		var updateTasks = new List<Task>();

		for (var i = 0; i < batch.Length; i++)
		{
			var currentRow = batch[i];

			if (currentRow is null)
			{
				continue;
			}

			try
			{
				if (i == batch.Length - 1 || batch[i + 1].TableName != currentRow.TableName)
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
				_ = _queueSpaceAvailable.Release();
			}
		}

		await Task.WhenAll(updateTasks).ConfigureAwait(false);
		updateTasks.Clear();
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
	private async Task<(byte[] fromLsn, byte[] toLsn, DateTime toLsnDate)> GetLsnRange(
		DateTime lastCommitTime,
		byte[] maxLsn,
		CancellationToken cancellationToken)
	{
		var fromLsn = await _cdcRepository.GetTimeToLsnAsync(lastCommitTime, "largest less than or equal", cancellationToken)
						  .ConfigureAwait(false);
		ArgumentNullException.ThrowIfNull(fromLsn);

		var fromLsnDate = await _cdcRepository.GetLsnToTimeAsync(fromLsn, cancellationToken).ConfigureAwait(false);
		ArgumentNullException.ThrowIfNull(fromLsnDate);

		// Calculate the 'toLsn' based on batch time interval
		var toLsnDate = fromLsnDate.Value.AddMilliseconds(_dbConfig.BatchTimeInterval);
		var toLsn = await _cdcRepository.GetTimeToLsnAsync(toLsnDate, "largest less than or equal", cancellationToken).ConfigureAwait(false)
					?? maxLsn;

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
