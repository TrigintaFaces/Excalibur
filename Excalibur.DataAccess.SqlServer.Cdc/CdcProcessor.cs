// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in
// the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using System.Globalization;
using System.Text;

using Excalibur.Core.Diagnostics;
using Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.DataAccess.SqlServer.Cdc;

public delegate Task CdcFatalErrorHandler(Exception exception, DataChangeEvent failedEvent);

/// <summary>
///   Processes Change Data Capture (CDC) changes by reading from a database, managing state, and invoking a specified
///   event handler.
/// </summary>
public class CdcProcessor : ICdcProcessor
{
	private readonly IDatabaseConfig _dbConfig;

	private readonly ICdcRepository _cdcRepository;

	private readonly ICdcStateStore _stateStore;

	private readonly IDataAccessPolicyFactory _policyFactory;

	private readonly ILogger<CdcProcessor> _logger;

	private readonly InMemoryDataQueue<DataChangeEvent> _cdcQueue;

	private readonly OrderedEventProcessor _orderedEventProcessor = new();

	private readonly Dictionary<string, CdcPosition> _tracking = new();

	private readonly SortedSet<(byte[] Lsn, string TableName)> _minHeap = new(new MinHeapComparer());

	private readonly object _minHeapLock = new();

	private readonly CancellationTokenSource _producerCancellationTokenSource = new();

	private readonly CdcFatalErrorHandler? _onFatalError;

	private readonly SemaphoreSlim _executionLock = new(1, 1);

	private bool _isRunning;

	private int _disposedFlag;

	private Task? _producerTask;

	private Task<int>? _consumerTask;

	private volatile bool _producerStopped;

	/// <summary>
	///   Initializes a new instance of the <see cref="CdcProcessor" /> class.
	/// </summary>
	/// <param name="appLifetime"> Provides notifications about application lifetime events. </param>
	/// <param name="dbConfig"> The database configuration for CDC processing. </param>
	/// <param name="cdcConnection"> The SQL connection for interacting with CDC data. </param>
	/// <param name="stateStoreConnection"> The SQL connection for persisting CDC state. </param>
	/// <param name="logger"> The logger used to log diagnostics and operational information. </param>
	/// <param name="onFatalError">
	///   Optional delegate that is invoked when a non-recoverable exception occurs during CDC processing. If provided,
	///   this allows consumers to handle fatal failures (e.g., alerting or graceful shutdown). If not provided, the
	///   processor will rethrow the exception and stop processing.
	/// </param>
	/// <exception cref="ArgumentNullException"> Thrown if any required dependency is <c> null </c>. </exception>
	public CdcProcessor(
		IHostApplicationLifetime appLifetime,
		IDatabaseConfig dbConfig,
		SqlConnection cdcConnection,
		SqlConnection stateStoreConnection,
		IDataAccessPolicyFactory policyFactory,
		ILogger<CdcProcessor> logger,
		CdcFatalErrorHandler? onFatalError = null)
	{
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(cdcConnection);
		ArgumentNullException.ThrowIfNull(stateStoreConnection);
		ArgumentNullException.ThrowIfNull(policyFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_dbConfig = dbConfig;
		_cdcRepository = new CdcRepository(cdcConnection);
		_stateStore = new CdcStateStore(stateStoreConnection);
		_policyFactory = policyFactory;
		_logger = logger;
		_cdcQueue = new InMemoryDataQueue<DataChangeEvent>(_dbConfig.QueueSize);
		_onFatalError = onFatalError;

		_ = appLifetime.ApplicationStopping.Register(() => Task.Run(OnApplicationStoppingAsync));
	}

	private CancellationToken ProducerCancellationToken => _producerCancellationTokenSource.Token;

	private bool ShouldWaitForProducer => !_producerStopped && !(_producerTask?.IsCompleted ?? true) && _cdcQueue.IsEmpty();

	/// <summary>
	///   Processes CDC changes asynchronously by producing changes from the database and consuming them with the
	///   provided handler. Ensures events are processed in strict order to preserve consistency across related changes.
	/// </summary>
	/// <param name="eventHandler">
	///   A delegate that handles each <see cref="DataChangeEvent" />. This handler should be idempotent and
	///   thread-safe, and must handle its own exceptions appropriately.
	/// </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The total number of events processed. </returns>
	/// <exception cref="ObjectDisposedException"> Thrown if the instance is already disposed. </exception>
	/// <remarks>
	///   If an unhandled exception occurs during ordered event processing, the error is logged at <c> Critical </c>
	///   level and passed to the <paramref name="onFatalError" /> delegate, if supplied. If not supplied, the processor
	///   will rethrow and stop execution.
	/// </remarks>
	public async Task<int> ProcessCdcChangesAsync(
		Func<DataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			if (_isRunning)
			{
				throw new InvalidOperationException("CDC Processor is already running.");
			}

			_isRunning = true;

			await InitializeTrackingAsync(cancellationToken).ConfigureAwait(false);

			var lowestStartLsn = GetNextLsn();

			if (lowestStartLsn == null)
			{
				throw new InvalidOperationException("Cannot start processing; no valid minimum LSN found in CDC tables.");
			}

			_logger.LogDebug("Starting new run at LSN {lowestStartLsn}", ByteArrayToHex(lowestStartLsn));

			_producerTask = Task.Run(() => ProducerLoop(lowestStartLsn, cancellationToken), cancellationToken);
			_consumerTask = Task.Run(() => ConsumerLoop(eventHandler, cancellationToken), cancellationToken);

			await _producerTask.ConfigureAwait(false);
			var consumerResult = await _consumerTask.ConfigureAwait(false);

			return consumerResult;
		}
		finally
		{
			_isRunning = false;
			_executionLock.Release();
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///   Disposes of resources used by the <see cref="CdcProcessor" />.
	/// </summary>
	protected virtual async ValueTask DisposeAsyncCore()
	{
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		_logger.LogInformation("Disposing CdcProcessor resources asynchronously.");

		if (_consumerTask is { IsCompleted: false })
		{
			_logger.LogWarning("Disposing CdcProcessor but Consumer has not completed.");
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

		_tracking.Clear();
		await _cdcQueue.DisposeAsync().ConfigureAwait(false);
		await _cdcRepository.DisposeAsync().ConfigureAwait(false);
		await _stateStore.DisposeAsync().ConfigureAwait(false);
		await _orderedEventProcessor.DisposeAsync().ConfigureAwait(false);

		_producerCancellationTokenSource.Dispose();
		_executionLock.Dispose();

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
	///   Determines if a given LSN is empty (contains only zero bytes).
	/// </summary>
	/// <param name="lsn"> The LSN to check. </param>
	/// <returns> <c> true </c> if the LSN is empty; otherwise, <c> false </c>. </returns>
	private static bool IsEmptyLsn(IEnumerable<byte> lsn) => lsn.All((byte b) => b == 0);

	/// <summary>
	///   Converts a byte array to a hexadecimal string representation.
	/// </summary>
	/// <param name="bytes"> The byte array to convert. </param>
	/// <returns> A hexadecimal string representation of the byte array. </returns>
	private static string ByteArrayToHex(byte[] bytes) => $"0x{Convert.ToHexString(bytes)}";

	private static bool IsInvalidLsnException(SqlException ex) =>
		ex.Number is 201 or 313
		|| ex.Message.Contains("insufficient number of arguments", StringComparison.OrdinalIgnoreCase)
		|| ex.Message.Contains("invalid lsn", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	///   Executes the producer loop that reads CDC changes from the database and enqueues them for consumption.
	/// </summary>
	/// <param name="lowestStartLsn"> The lowest LSN from all the configured capture instances. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	private async Task ProducerLoop(byte[]? lowestStartLsn, CancellationToken cancellationToken)
	{
		try
		{
			using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ProducerCancellationToken);
			var combinedToken = combinedTokenSource.Token;
			var currentGlobalLsn = lowestStartLsn;
			var repoPolicy = _policyFactory.GetComprehensivePolicy();
			var maxLsn = await repoPolicy
				.ExecuteAsync(() => _cdcRepository.GetMaxPositionAsync(combinedToken))
				.ConfigureAwait(false);

			lock (_minHeapLock)
			{
				_logger.LogDebug("Producer loop started with tracking state for {TableCount} tables", _minHeap.Count);
			}

			while (currentGlobalLsn != null && currentGlobalLsn.CompareLsn(maxLsn) <= 0)
			{
				combinedToken.ThrowIfCancellationRequested();

				foreach (var tableName in _tracking.Keys)
				{
					if (_tracking.TryGetValue(tableName, out var tableTracking))
					{
						if (tableTracking.Lsn.CompareLsn(currentGlobalLsn) == 0)
						{
							await EnqueueTableChangesAsync(tableName, tableTracking.Lsn, tableTracking.SequenceValue, maxLsn, combinedToken)
								.ConfigureAwait(false);
						}
					}
				}

				currentGlobalLsn = GetNextLsn();
			}

			_logger.LogInformation("No more CDC records. Producer exiting.");
		}
		catch (OperationCanceledException)
		{
			_logger.LogDebug("CdcProcessor Producer canceled");
		}
		catch (SqlException ex)
		{
			_logger.LogError(ex, "SQL error in CdcProcessor ProducerLoop");
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected Error in CdcProcessor ProducerLoop");
			throw;
		}
		finally
		{
			_producerStopped = true;
			_cdcQueue.CompleteWriter();

			_logger.LogInformation("CDC Producer has completed execution. Channel marked as complete.");
		}
	}

	private async Task EnqueueTableChangesAsync(
		string tableName,
		byte[] lastLsn,
		byte[]? lastSequenceValue,
		byte[] maxLsn,
		CancellationToken combinedToken)
	{
		var totalRowsReadInThisLsn = 0;
		var producerBatchSize = Math.Min(_dbConfig.QueueSize - _cdcQueue.Count, _dbConfig.ProducerBatchSize);
		var lsn = lastLsn;
		var sequenceValue = lastSequenceValue;
		var lastOperation = CdcOperationCodes.Unknown;
		var pendingUpdateBefore = new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer());
		var pendingUpdateAfter = new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer());

		while (!combinedToken.IsCancellationRequested)
		{
			_logger.LogDebug(
				"Fetching CDC changes: {TableName} - LSN {Lsn}, SeqVal {SeqVal}",
				tableName,
				ByteArrayToHex(lsn),
				sequenceValue != null ? ByteArrayToHex(sequenceValue) : "null");

			IList<CdcRow> changes;
			try
			{
				var retryPolicy = _policyFactory.GetComprehensivePolicy();
				changes = await retryPolicy
						.ExecuteAsync(() => _cdcRepository.FetchChangesAsync(
								tableName,
								producerBatchSize,
								lsn,
								sequenceValue,
								lastOperation,
								combinedToken))
						.ConfigureAwait(false) as IList<CdcRow> ?? [];
			}
			catch (SqlException ex) when (IsInvalidLsnException(ex))
			{
				_logger.LogWarning(
						ex,
						"Invalid LSN {Lsn} for {TableName}, attempting to recover",
						ByteArrayToHex(lsn),
						tableName);

				var recoveryPolicy = _policyFactory.GetComprehensivePolicy();
				var nextValidLsn = await recoveryPolicy
								.ExecuteAsync(() => _cdcRepository.GetNextValidLsn(lsn, combinedToken))
								.ConfigureAwait(false)
								?? await recoveryPolicy
										.ExecuteAsync(() => _cdcRepository.GetMinPositionAsync(tableName, combinedToken))
										.ConfigureAwait(false);

				var commitTimePolicy = _policyFactory.GetComprehensivePolicy();
				var commitTime = await commitTimePolicy
								.ExecuteAsync(() => _cdcRepository.GetLsnToTimeAsync(nextValidLsn, combinedToken))
								.ConfigureAwait(false);

				var updatePolicy = _policyFactory.GetComprehensivePolicy();
				await updatePolicy
						.ExecuteAsync(() => UpdateTableLastProcessed(
								tableName,
								nextValidLsn,
								null,
								commitTime,
								combinedToken))
						.ConfigureAwait(false);

				UpdateLsnTracking(tableName, nextValidLsn, null);

				return;
			}

			if (changes.Count == 0)
			{
				if (totalRowsReadInThisLsn > 0)
				{
					_logger.LogDebug(
						"Successfully enqueued {EnqueuedRowCount} CDC rows for {TableName}, advancing LSN.",
						totalRowsReadInThisLsn,
						tableName);
				}
				else
				{
					_logger.LogInformation("No changes found for {TableName}, advancing LSN.", tableName);

					var commitTimePolicy = _policyFactory.GetComprehensivePolicy();
					var commitTime = await commitTimePolicy
						.ExecuteAsync(() => _cdcRepository.GetLsnToTimeAsync(lsn, combinedToken))
						.ConfigureAwait(false);

					var updatePolicy = _policyFactory.GetComprehensivePolicy();
					await updatePolicy
						.ExecuteAsync(() => UpdateTableLastProcessed(
							tableName,
							lsn,
							sequenceValue,
							commitTime,
							combinedToken))
						.ConfigureAwait(false);
				}

				break;
			}

			totalRowsReadInThisLsn += changes.Count;

			var events = new List<DataChangeEvent>();
			foreach (var record in changes)
			{
				switch (record.OperationCode)
				{
					case CdcOperationCodes.Delete:
						events.Add(DataChangeEvent.CreateDeleteEvent(record));
						break;

					case CdcOperationCodes.Insert:
						events.Add(DataChangeEvent.CreateInsertEvent(record));
						break;

					case CdcOperationCodes.UpdateBefore when pendingUpdateAfter.TryGetValue(record.SeqVal, out var afterRecord):
						events.Add(DataChangeEvent.CreateUpdateEvent(record, afterRecord));
						pendingUpdateAfter.Remove(record.SeqVal);
						break;

					case CdcOperationCodes.UpdateBefore:
						pendingUpdateBefore[record.SeqVal] = record;
						break;

					case CdcOperationCodes.UpdateAfter when pendingUpdateBefore.TryGetValue(record.SeqVal, out var beforeRecord):
						events.Add(DataChangeEvent.CreateUpdateEvent(beforeRecord, record));
						pendingUpdateBefore.Remove(record.SeqVal);
						break;

					case CdcOperationCodes.UpdateAfter:
						pendingUpdateAfter[record.SeqVal] = record;
						break;

					case CdcOperationCodes.Unknown:
					default:
						_logger.LogWarning(
							"Unknown operation {OperationCode} at Position {Position}, SequenceValue {SequenceValue}",
							record.OperationCode,
							record.Lsn,
							record.SeqVal);
						break;
				}

				lsn = record.Lsn;
				sequenceValue = record.SeqVal;
				lastOperation = record.OperationCode;
			}

			// There shouldn't be any matching pairs here just ones with pairs in the next batch but just in case
			foreach (var seqVal in pendingUpdateBefore.Keys.ToList())
			{
				if (pendingUpdateAfter.TryGetValue(seqVal, out var afterRecord))
				{
					events.Add(DataChangeEvent.CreateUpdateEvent(pendingUpdateBefore[seqVal], afterRecord));
					pendingUpdateBefore.Remove(seqVal);
					pendingUpdateAfter.Remove(seqVal);
				}
			}

			if (events.Count > 0)
			{
				await _cdcQueue.EnqueueBatchAsync(events, combinedToken).ConfigureAwait(false);
			}

			events.Clear();
			changes.Clear();
			changes = null;
			events = null;
		}

		if (pendingUpdateBefore.Count > 0 || pendingUpdateAfter.Count > 0)
		{
			var unmatchedUpdates = new StringBuilder();
			foreach (var kvp in pendingUpdateBefore)
			{
				unmatchedUpdates.Append(
					CultureInfo.CurrentCulture,
					$"Unmatched UpdateBefore at LSN {ByteArrayToHex(lsn)}, SeqVal {ByteArrayToHex(kvp.Key)}").Append(Environment.NewLine);
			}

			foreach (var kvp in pendingUpdateAfter)
			{
				unmatchedUpdates.Append(
					CultureInfo.CurrentCulture,
					$"Unmatched UpdateAfter at LSN {ByteArrayToHex(lsn)}, SeqVal {ByteArrayToHex(kvp.Key)}").Append(Environment.NewLine);
			}

			_logger.LogError(
				"Unmatched UpdateBefore/UpdateAfter pairs detected at the end of LSN processing:" + Environment.NewLine + unmatchedUpdates);

			throw new UnmatchedUpdateRecordsException(lsn);
		}

		var nextLsnPolicy = _policyFactory.GetComprehensivePolicy();
		var nextLsn = await nextLsnPolicy
			.ExecuteAsync(() => _cdcRepository.GetNextLsnAsync(tableName, lsn, combinedToken))
			.ConfigureAwait(false);

		_logger.LogDebug(
			"Table {CaptureInstance} enqueued: currentLSN={CurrentLsn}, nextLSN={NextLsn}, maxLSN={MaxLsn}",
			tableName,
			ByteArrayToHex(lsn),
			nextLsn != null ? ByteArrayToHex(nextLsn) : "null",
			ByteArrayToHex(maxLsn));

		if (nextLsn != null && nextLsn.CompareLsn(maxLsn) < 0)
		{
			UpdateLsnTracking(tableName, nextLsn, null);
		}
		else
		{
			UpdateLsnTracking(tableName, null, null);
		}
	}

	/// <summary>
	///   Executes the consumer loop that processes CDC events in batches.
	/// </summary>
	/// <param name="eventHandler"> The handler to process data change events. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The total number of events processed. </returns>
	private async Task<int> ConsumerLoop(Func<DataChangeEvent, CancellationToken, Task> eventHandler, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventHandler);

		_logger.LogInformation("CDC Consumer loop started...");

		var totalProcessedCount = 0;
		CdcRow? remainingUpdateBeforeLast = null;

		while (!cancellationToken.IsCancellationRequested)
		{
			if (_disposedFlag == 1)
			{
				_logger.LogWarning("ConsumerLoop: disposal requested, exit now.");
				break;
			}

			if (_producerStopped && _cdcQueue.IsEmpty())
			{
				_logger.LogInformation("No more CDC records. Consumer is exiting gracefully.");
				break;
			}

			if (ShouldWaitForProducer)
			{
				_logger.LogInformation("CDC Queue is empty. Waiting for producer...");

				var waitTime = 10;
				for (var i = 0; i < 10; i++)
				{
					await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
					waitTime = Math.Min(waitTime * 2, 200);
				}

				continue;
			}

			try
			{
				cancellationToken.ThrowIfCancellationRequested();

				_logger.LogDebug("Attempting to dequeue CDC messages...");
				var stopwatch = ValueStopwatch.StartNew();

				var batch = await _cdcQueue.DequeueBatchAsync(_dbConfig.ConsumerBatchSize, cancellationToken).ConfigureAwait(false);
				_logger.LogDebug("Dequeued {BatchSize} messages in {ElapsedMs}ms", batch.Count, stopwatch.Elapsed.TotalMilliseconds);

				_logger.LogDebug("Processing batch of {BatchSize} CDC records", batch.Count);

				await ProcessBatchAsync(batch, eventHandler, cancellationToken).ConfigureAwait(false);

				_logger.LogDebug("Processed {BatchSize} CDC records in {ElapsedMs}ms", batch.Count, stopwatch.Elapsed.TotalMilliseconds);

				totalProcessedCount += batch.Count;
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
	}

	private async Task ProcessBatchAsync(
		IList<DataChangeEvent> batch,
		Func<DataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(batch);
		ArgumentNullException.ThrowIfNull(eventHandler);

		foreach (var changeEvent in batch)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				await _orderedEventProcessor.ProcessAsync(async () =>
						await _policyFactory.GetComprehensivePolicy().ExecuteAsync(async () =>
								await eventHandler(changeEvent, cancellationToken)
									.ConfigureAwait(false))
							.ConfigureAwait(false))
					.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogCritical(ex,
					"Unhandled exception occurred while processing change event for table '{TableName}', LSN {Lsn}, SeqVal {SeqVal}.",
					changeEvent.TableName,
					ByteArrayToHex(changeEvent.Lsn),
					ByteArrayToHex(changeEvent.SeqVal));

				if (_onFatalError != null)
				{
					await _onFatalError(ex, changeEvent).ConfigureAwait(false);
				}
				else
				{
					throw;
				}
			}

			var updateTablePolicy = _policyFactory.GetComprehensivePolicy();
			await updateTablePolicy.ExecuteAsync(
				() => UpdateTableLastProcessed(
					changeEvent.TableName,
					changeEvent.Lsn,
					changeEvent.SeqVal,
					changeEvent.CommitTime,
					cancellationToken)).ConfigureAwait(false);
		}
	}

	private async Task UpdateTableLastProcessed(
		string tableName,
		byte[] lsn,
		byte[]? sequenceValue,
		DateTime? commitTime,
		CancellationToken cancellationToken)
	{
		_ = await _stateStore.UpdateLastProcessedPositionAsync(
			_dbConfig.DatabaseConnectionIdentifier,
			_dbConfig.DatabaseName,
			tableName,
			lsn,
			sequenceValue,
			commitTime,
			cancellationToken).ConfigureAwait(false);

		_logger.LogInformation("Updated state for {TableName}", tableName);
	}

	private async Task InitializeTrackingAsync(CancellationToken cancellationToken)
	{
		var policy = _policyFactory.GetComprehensivePolicy();
		var processingStates = await policy.ExecuteAsync(
							   () => _stateStore.GetLastProcessedPositionAsync(
									   _dbConfig.DatabaseConnectionIdentifier,
									   _dbConfig.DatabaseName,
									   cancellationToken)).ConfigureAwait(false) as ICollection<CdcProcessingState> ?? [];

		var maxLsn = await policy.ExecuteAsync(() => _cdcRepository.GetMaxPositionAsync(cancellationToken)).ConfigureAwait(false);

		foreach (var captureInstance in _dbConfig.CaptureInstances)
		{
			var exists = await policy.ExecuteAsync(
					() => _cdcRepository.CaptureInstanceExistsAsync(captureInstance, cancellationToken)).ConfigureAwait(false);

			if (!exists)
			{
				throw new InvalidOperationException($"Capture instance '{captureInstance}' does not exist in the database.");
			}

			var state = processingStates.FirstOrDefault(
					(CdcProcessingState x) => x.TableName.Equals(captureInstance, StringComparison.OrdinalIgnoreCase));

			byte[] startLsn;
			byte[]? seqVal = null;
			var minLsn = await policy.ExecuteAsync(
					() => _cdcRepository.GetMinPositionAsync(captureInstance, cancellationToken)).ConfigureAwait(false);

			if (state == null || IsEmptyLsn(state.LastProcessedLsn))
			{
				startLsn = minLsn;
			}
			else
			{
				startLsn = state.LastProcessedLsn;
				seqVal = state.LastProcessedSequenceValue;

				if (startLsn.CompareLsn(minLsn) < 0 || startLsn.CompareLsn(maxLsn) > 0)
				{
					var nextValid = await policy.ExecuteAsync(
							() => _cdcRepository.GetNextValidLsn(startLsn, cancellationToken)).ConfigureAwait(false);
					startLsn = nextValid ?? minLsn;
					seqVal = null;
				}
			}

			UpdateLsnTracking(captureInstance, startLsn, seqVal);
		}
	}

	private void UpdateLsnTracking(string tableName, byte[]? lsn, byte[]? seqVal)
	{
		lock (_minHeapLock)
		{
			if (lsn == null)
			{
				if (_tracking.ContainsKey(tableName) && _tracking.Remove(tableName, out _))
				{
					_ = _minHeap.RemoveWhere(((byte[] Lsn, string TableName) item) => item.TableName == tableName);
					_logger.LogDebug("Removed LSN for table {TableName}", tableName);
				}
			}
			else
			{
				if (_tracking.TryGetValue(tableName, out var currentPos))
				{
					if (lsn.CompareLsn(currentPos.Lsn) > 0)
					{
						_tracking[tableName] = new CdcPosition(lsn, seqVal);
						_ = _minHeap.RemoveWhere(((byte[] Lsn, string TableName) item) => item.TableName == tableName);
						_ = _minHeap.Add((lsn, tableName));
						_logger.LogDebug("Updated LSN for table {TableName}: {Lsn}", tableName, ByteArrayToHex(lsn));
					}
				}
				else
				{
					_tracking[tableName] = new CdcPosition(lsn, seqVal);
					_ = _minHeap.Add((lsn, tableName));
					_logger.LogDebug("Inserted new LSN for table {TableName}: {Lsn}", tableName, ByteArrayToHex(lsn));
				}
			}
		}
	}

	private byte[]? GetNextLsn()
	{
		lock (_minHeapLock)
		{
			return _minHeap.Count == 0 ? null : _minHeap.Min.Lsn;
		}
	}

	/// <summary>
	///   Handles cleanup when the application is stopping.
	/// </summary>
	private async Task OnApplicationStoppingAsync()
	{
		_logger.LogInformation("Application is stopping. Cancelling CDCProcessor producer immediately.");

		_producerStopped = true;
		await _producerCancellationTokenSource.CancelAsync().ConfigureAwait(false);

		_logger.LogInformation("CDCProcessor Producer cancellation requested.");
		_logger.LogInformation("Waiting for CDCProcessor consumer to finish remaining work...");

		try
		{
			await DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error while disposing CDCProcessor on application shutdown.");
		}
	}
}
