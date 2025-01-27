using System.Numerics;

using Microsoft.Extensions.Logging;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Processes Change Data Capture (CDC) changes by reading from a database, managing state, and invoking a specified event handler.
/// </summary>
public class CdcProcessor(
	IDatabaseConfig dbConfig,
	ICdcRepository cdcRepository,
	ICdcStateStore stateStore,
	ILogger<CdcProcessor> logger) : ICdcProcessor
{
	private readonly IDataQueue<CdcRow> _cdcQueue = new InMemoryDataQueue<CdcRow>();

	/// <summary>
	///     Processes CDC changes asynchronously by producing changes from the database and consuming them with the provided handler.
	/// </summary>
	/// <param name="eventHandler"> A delegate that handles the data change events. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The total number of events processed. </returns>
	public async Task<int> ProcessCdcChangesAsync(Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		var totalEvents = 0;
		var processingState = await stateStore.GetLastProcessedPositionAsync(
			dbConfig.DatabaseConnectionIdentifier,
			dbConfig.DatabaseName, cancellationToken).ConfigureAwait(false) ?? new CdcProcessingState();

		var maxLsn = await cdcRepository.GetMaxPositionAsync(cancellationToken).ConfigureAwait(false);

		var producerTask = Task.Run(() => ProducerLoop(processingState, maxLsn, cancellationToken), cancellationToken);

		const int MaxDegreeOfParallelism = 1;
		var consumerTasks = Enumerable.Range(0, MaxDegreeOfParallelism)
			.Select(_ => Task.Run(() => ConsumerLoop(eventHandler, cancellationToken), cancellationToken))
			.ToArray();

		await producerTask.ConfigureAwait(false);

		if (_cdcQueue is IDisposable disposable)
		{
			disposable.Dispose();
		}

		var results = await Task.WhenAll(consumerTasks).ConfigureAwait(false);

		totalEvents = results.Sum();

		return totalEvents;
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
						logger.LogWarning("Missing UpdateAfter for UpdateBefore at Position {Position}, SequenceValue {SequenceValue}",
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
					logger.LogWarning(
						"Unexpected UpdateAfter without corresponding UpdateBefore at Position {Position}, SequenceValue {SequenceValue}",
						change.Lsn, change.SeqVal);
					i++;
					break;

				case CdcOperationCodes.Unknown:
				default:
					logger.LogWarning("Unknown operation {OperationCode} at Position {Position}, SequenceValue {SequenceValue}",
						change.OperationCode, change.Lsn, change.SeqVal);
					i++;
					break;
			}
		}

		return [.. dataChangeEvents];
	}

	private async Task ProducerLoop(CdcProcessingState processingState, byte[] maxLsn, CancellationToken cancellationToken)
	{
		try
		{
			var startProcessingFrom = await GetInitialStartLsn(processingState.LastProcessedLsn, cancellationToken).ConfigureAwait(false);
			var commitTime = processingState.LastCommitTime != default
				? processingState.LastCommitTime
				: await cdcRepository.GetLsnToTimeAsync(startProcessingFrom, cancellationToken).ConfigureAwait(false);

			ArgumentNullException.ThrowIfNull(commitTime);

			while (CompareLsn(startProcessingFrom, maxLsn) < 0 && !cancellationToken.IsCancellationRequested)
			{
				var (fromLsn, toLsn, toLsnDate) = await GetLsnRange(commitTime.Value, maxLsn, cancellationToken).ConfigureAwait(false);

				var fromLsnString = ByteArrayToHex(fromLsn);
				var toLsnString = ByteArrayToHex(toLsn);
				logger.LogDebug("Producer chunk from LSN {From} to {To}", ByteArrayToHex(fromLsn), ByteArrayToHex(toLsn));

				var cdcRowCount = 0;
				await foreach (var cdcRow in cdcRepository.FetchChangesAsync(
								   fromLsn,
								   toLsn,
								   processingState.LastProcessedSequenceValue,
								   dbConfig.CaptureInstances,
								   cancellationToken).ConfigureAwait(false))
				{
					cancellationToken.ThrowIfCancellationRequested();

					await _cdcQueue.EnqueueAsync(cdcRow, cancellationToken).ConfigureAwait(false);
					cdcRowCount++;
				}

				logger.LogInformation("Enqueued {Count} CDC rows for range {From} - {To}", cdcRowCount, ByteArrayToHex(fromLsn),
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
			logger.LogDebug("Producer canceled");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error in ProducerLoop");
			throw;
		}
	}

	private async Task<int> ConsumerLoop(Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		var localEventsProcessed = 0;

		try
		{
			var buffer = new List<CdcRow>();
			BigInteger? currentLsn = null;

			// Dequeue rows until the queue is complete
			await foreach (var cdcRow in _cdcQueue.DequeueAllAsync(cancellationToken).ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();

				var rowLsn = new BigInteger(cdcRow.Lsn.Reverse().ToArray());

				if (currentLsn == null || rowLsn == currentLsn)
				{
					buffer.Add(cdcRow);
					currentLsn ??= rowLsn;

					if (buffer.Count < 1000)
					{
						continue;
					}

					var (remainingRows, eventsProcessed) =
						await ProcessLsnChunk(buffer, eventHandler, cancellationToken).ConfigureAwait(false);
					buffer = remainingRows;
					localEventsProcessed += eventsProcessed;
				}
				else
				{
					// new LSN => process old buffer
					localEventsProcessed += await ProcessLsnGroup(buffer, eventHandler, cancellationToken).ConfigureAwait(false);

					buffer.Clear();
					buffer.Add(cdcRow);
					currentLsn = rowLsn;
				}
			}

			// final flush
			if (buffer.Count > 0)
			{
				localEventsProcessed += await ProcessLsnGroup(buffer, eventHandler, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			logger.LogDebug("Consumer canceled");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error in ConsumerLoop");
			throw;
		}

		// Return the total events processed by THIS consumer
		return localEventsProcessed;
	}

	private async Task<(List<CdcRow> cdcRows, int eventCount)> ProcessLsnChunk(
		List<CdcRow> cdcRows,
		Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		var processCount = cdcRows.Count;
		var eventCount = 0;
		if (processCount > 0 && cdcRows[processCount - 1].OperationCode == CdcOperationCodes.UpdateBefore)
		{
			processCount--;
		}

		if (processCount > 0)
		{
			var safeChunk = cdcRows.Take(processCount).ToList();
			eventCount = await ProcessLsnGroup(safeChunk, eventHandler, cancellationToken).ConfigureAwait(false);

			cdcRows.RemoveRange(0, processCount);
		}

		return (cdcRows, eventCount);
	}

	private Task<int> ProcessLsnGroup(
		List<CdcRow> cdcRows,
		Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken)
	{
		// Update the state store with the last processed LSN
		var lastRow = cdcRows[^1];
		return ProcessLsnGroup(cdcRows, eventHandler, lastRow.Lsn, lastRow.SeqVal, lastRow.CommitTime, cancellationToken);
	}

	private async Task<int> ProcessLsnGroup(
		IEnumerable<CdcRow> cdcRows,
		Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		byte[] position,
		byte[]? sequenceValue,
		DateTime commitTime,
		CancellationToken cancellationToken)
	{
		var events = GetDataChangeEvents(cdcRows);

		// Call the event handler with the changes
		var eventCount = await eventHandler(events, cancellationToken).ConfigureAwait(false);

		// Update the state store with the last processed LSN
		await stateStore.UpdateLastProcessedPositionAsync(
			dbConfig.DatabaseConnectionIdentifier,
			dbConfig.DatabaseName,
			position,
			sequenceValue,
			commitTime,
			cancellationToken).ConfigureAwait(false);

		return eventCount;
	}

	private async Task<byte[]> GetInitialStartLsn(byte[]? startLsn, CancellationToken cancellationToken)
	{
		byte[]? captureMinLsn = null;
		// Find the lowest LSN across all capture instances
		foreach (var captureInstance in dbConfig.CaptureInstances)
		{
			var minPos = await cdcRepository.GetMinPositionAsync(captureInstance, cancellationToken).ConfigureAwait(false);
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

	private async Task<(byte[] fromLsn, byte[] toLsn, DateTime toLsnDate)> GetLsnRange(DateTime lastCommitTime, byte[] maxLsn,
		CancellationToken cancellationToken)
	{
		var fromLsn = await cdcRepository.GetTimeToLsnAsync(lastCommitTime, "smallest greater than", cancellationToken)
			.ConfigureAwait(false);
		ArgumentNullException.ThrowIfNull(fromLsn);

		var fromLsnDate = await cdcRepository.GetLsnToTimeAsync(fromLsn, cancellationToken).ConfigureAwait(false);
		ArgumentNullException.ThrowIfNull(fromLsnDate);

		// Calculate the 'toLsn' based on batch time interval
		var toLsnDate = fromLsnDate.Value.AddMilliseconds(dbConfig.BatchTimeInterval);
		var toLsn =
			await cdcRepository.GetTimeToLsnAsync(toLsnDate, "largest less than or equal", cancellationToken).ConfigureAwait(false) ??
			maxLsn;

		return (fromLsn, toLsn, toLsnDate);
	}
}
