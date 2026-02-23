// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;
using System.Threading.Channels;

using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Delivery.BatchProcessing;
using Excalibur.Domain;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Processes Change Data Capture (CDC) changes by reading from a database, managing state, and invoking a specified event handler.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
	Justification = "CDC processor requires coordination across many types; LoggerMessage source generation adds coupling.")]
public partial class CdcProcessor : ICdcProcessor
{
	private readonly IDatabaseConfig _dbConfig;

	// CA1859: These fields are declared as interfaces to allow external consumers to provide custom implementations This is a public API in
	// a NuGet package designed for extensibility R0.8: Use concrete types when possible for improved performance
#pragma warning disable CA1859
	private readonly ICdcRepository _cdcRepository;

	private readonly ICdcRepositoryLsnMapping _cdcLsnMapping;

	private readonly ICdcStateStore _stateStore;
#pragma warning restore CA1859

	private readonly IDataAccessPolicyFactory _policyFactory;

	private readonly ILogger<CdcProcessor> _logger;

	private readonly Channel<DataChangeEvent> _cdcQueue;
	private readonly int _queueSize;

	private readonly OrderedEventProcessor _orderedEventProcessor = new();

	private readonly ConcurrentDictionary<string, CdcPosition> _tracking = new(StringComparer.Ordinal);

	private readonly SortedSet<(byte[] Lsn, string TableName)> _minHeap = new(new MinHeapComparer());

#if NET9_0_OR_GREATER


	private readonly Lock _minHeapLock = new();


#else


	private readonly object _minHeapLock = new();


#endif

	private readonly CancellationTokenSource _producerCancellationTokenSource = new();

	private static readonly Counter<long> EventsProcessedCounter = CdcTelemetryConstants.Meter.CreateCounter<long>(
		CdcTelemetryConstants.MetricNames.EventsProcessed,
		"events",
		"Total CDC events processed successfully");

	private static readonly Counter<long> EventsFailedCounter = CdcTelemetryConstants.Meter.CreateCounter<long>(
		CdcTelemetryConstants.MetricNames.EventsFailed,
		"events",
		"Total CDC event processing failures");

	private static readonly Histogram<double> BatchDurationHistogram = CdcTelemetryConstants.Meter.CreateHistogram<double>(
		CdcTelemetryConstants.MetricNames.BatchDuration,
		"ms",
		"Duration of batch processing in milliseconds");

	private static readonly Histogram<int> BatchSizeHistogram = CdcTelemetryConstants.Meter.CreateHistogram<int>(
		CdcTelemetryConstants.MetricNames.BatchSize,
		"events",
		"Number of events in a processed batch");

	private readonly CdcFatalErrorHandler? _onFatalError;

	private readonly SemaphoreSlim _executionLock = new(1, 1);

	private readonly ConcurrentBag<Task> _backgroundTasks = [];

	private volatile bool _isRunning;

	private int _disposedFlag;

	private Task? _producerTask;

	private Task<int>? _consumerTask;

	private volatile bool _producerStopped;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcProcessor" /> class.
	/// </summary>
	/// <param name="appLifetime"> Provides notifications about application lifetime events. </param>
	/// <param name="dbConfig"> The database configuration for CDC processing. </param>
	/// <param name="cdcConnection"> The SQL connection for interacting with CDC data. </param>
	/// <param name="stateStoreConnection"> The SQL connection for persisting CDC state. </param>
	/// <param name="stateStoreOptions"> The CDC state store options. </param>
	/// <param name="policyFactory"> The factory for creating data access policies. </param>
	/// <param name="logger"> The logger used to log diagnostics and operational information. </param>
	/// <param name="fatalErrorOptions">
	/// Options containing an optional delegate that is invoked when a non-recoverable exception occurs during CDC processing.
	/// If the delegate is not configured, the processor will rethrow the exception and stop processing.
	/// </param>
	/// <exception cref="ArgumentNullException"> Thrown if any required dependency is <c> null </c>. </exception>
	public CdcProcessor(
			IHostApplicationLifetime appLifetime,
			IDatabaseConfig dbConfig,
			SqlConnection cdcConnection,
			SqlConnection stateStoreConnection,
			IOptions<SqlServerCdcStateStoreOptions>? stateStoreOptions,
			IDataAccessPolicyFactory policyFactory,
			ILogger<CdcProcessor> logger,
			IOptions<CdcFatalErrorOptions>? fatalErrorOptions = null)
	{
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(cdcConnection);
		ArgumentNullException.ThrowIfNull(stateStoreConnection);
		ArgumentNullException.ThrowIfNull(policyFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_dbConfig = dbConfig;
		var cdcRepository = new CdcRepository(cdcConnection);
		_cdcRepository = cdcRepository;
		_cdcLsnMapping = cdcRepository;
		_stateStore = stateStoreOptions is null
				? new CdcStateStore(stateStoreConnection)
				: new CdcStateStore(stateStoreConnection, stateStoreOptions);
		_policyFactory = policyFactory;
		_logger = logger;
		_queueSize = _dbConfig.QueueSize;
		_cdcQueue = Channel.CreateBounded<DataChangeEvent>(new BoundedChannelOptions(_dbConfig.QueueSize)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true, // Only ConsumerLoopAsync reads from the channel
			SingleWriter = true, // Only ProducerLoopAsync writes to the channel
			AllowSynchronousContinuations = false,
		});
		_onFatalError = fatalErrorOptions?.Value.OnFatalError;

		_ = appLifetime.ApplicationStopping.Register(() =>
		{
			var task = Task.Factory.StartNew(
					OnApplicationStoppingAsync,
					CancellationToken.None,
					TaskCreationOptions.DenyChildAttach,
					TaskScheduler.Default)
				.Unwrap();
			_backgroundTasks.Add(task);
		});
	}

	private CancellationToken ProducerCancellationToken => _producerCancellationTokenSource.Token;

	private bool ShouldWaitForProducer => !_producerStopped && !(_producerTask?.IsCompleted ?? true) && _cdcQueue.Reader.Count == 0;

	/// <summary>
	/// Processes CDC changes asynchronously by producing changes from the database and consuming them with the provided handler. Ensures
	/// events are processed in strict order to preserve consistency across related changes.
	/// </summary>
	/// <param name="eventHandler">
	/// A delegate that handles each <see cref="DataChangeEvent" />. This handler should be idempotent and thread-safe, and must handle its
	/// own exceptions appropriately.
	/// </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The total number of events processed. </returns>
	/// <exception cref="ObjectDisposedException"> Thrown if the instance is already disposed. </exception>
	/// <remarks>
	/// If an unhandled exception occurs during ordered event processing, the error is logged at <c> Critical </c> level and passed to the
	/// fatal error handler delegate, if supplied. If not supplied, the processor will rethrow and stop execution.
	/// </remarks>
	/// <exception cref="InvalidOperationException"> </exception>
	public async Task<int> ProcessCdcChangesAsync(
		Func<DataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		using var activity = CdcTelemetryConstants.ActivitySource.StartActivity("cdc.process");
		activity?.SetTag(CdcTelemetryConstants.Tags.CaptureInstance, string.Join(",", _dbConfig.CaptureInstances));

		try
		{
			if (_isRunning)
			{
				throw new InvalidOperationException("CDC processor is already running.");
			}

			_isRunning = true;

			await InitializeTrackingAsync(cancellationToken).ConfigureAwait(false);

			var lowestStartLsn = GetNextLsn() ??
								 throw new InvalidOperationException("Cannot start processing: no valid minimum LSN found.");

			LogStartingNewRun(ByteArrayToHex(lowestStartLsn));

			_producerTask = Task.Factory.StartNew(
					() => ProducerLoopAsync(lowestStartLsn, cancellationToken),
					cancellationToken,
					TaskCreationOptions.LongRunning,
					TaskScheduler.Default)
				.Unwrap();
			_consumerTask = Task.Factory.StartNew(
					() => ConsumerLoopAsync(eventHandler, cancellationToken),
					cancellationToken,
					TaskCreationOptions.LongRunning,
					TaskScheduler.Default)
				.Unwrap();

			await _producerTask.ConfigureAwait(false);
			var totalProcessed = await _consumerTask.ConfigureAwait(false);

			activity?.SetTag("cdc.events.total", totalProcessed);
			return totalProcessed;
		}
		catch (Exception ex)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
		finally
		{
			_isRunning = false;
			_ = _executionLock.Release();
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes of resources used by the <see cref="CdcProcessor" />.
	/// </summary>
	// DisposeCoreAsync is the standard .NET IAsyncDisposable pattern name per framework design guidelines
	// See: https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync#implement-the-async-dispose-pattern
	// R0.8: Asynchronous method name should end with 'Async'
#pragma warning disable RCS1046

	protected virtual async ValueTask DisposeAsyncCore()
	{
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		LogDisposingAsync();

		try
		{
			await _producerCancellationTokenSource.CancelAsync().ConfigureAwait(false);

			if (_consumerTask is { IsCompleted: false })
			{
				LogConsumerNotCompletedAsync();

				try
				{
					using var disposalCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
					_ = await _consumerTask.WaitAsync(TimeSpan.FromMinutes(5), disposalCts.Token).ConfigureAwait(false);
				}
				catch (TimeoutException ex)
				{
					LogConsumerTimeoutAsync(ex);
				}
				catch (OperationCanceledException)
				{
					// Disposal timeout expired — continue cleanup
				}
			}

			// Await tracked background tasks
			foreach (var task in _backgroundTasks)
			{
				try
				{
					await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
				}
				catch (TimeoutException)
				{
					// Background task did not complete in time — continue cleanup
				}
				catch (Exception)
				{
					// Swallow exceptions from background tasks during disposal
				}
			}

			if (_producerTask is not null)
			{
				await CdcDisposalHelper.SafeDisposeAsync(_producerTask).ConfigureAwait(false);
			}

			if (_consumerTask is not null)
			{
				await CdcDisposalHelper.SafeDisposeAsync(_consumerTask).ConfigureAwait(false);
			}

			_tracking.Clear();
			_ = _cdcQueue.Writer.TryComplete();
			await _cdcRepository.DisposeAsync().ConfigureAwait(false);
			await _stateStore.DisposeAsync().ConfigureAwait(false);
			await _orderedEventProcessor.DisposeAsync().ConfigureAwait(false);

			_executionLock.Dispose();
		}
		catch (Exception ex)
		{
			LogErrorDisposingAsync(ex);
		}
		finally
		{
			_producerCancellationTokenSource.Dispose();
		}
	}

#pragma warning restore RCS1046 // Asynchronous method name should end with 'Async'

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="CdcProcessor" /> and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing"> True to release both managed and unmanaged resources; false to release only unmanaged resources. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposing || Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		LogDisposingSync();

		_producerCancellationTokenSource.Cancel();

		if (_consumerTask is { IsCompleted: false })
		{
			LogConsumerNotCompletedSync();

			try
			{
				_ = _consumerTask.Wait(TimeSpan.FromMinutes(5));
			}
			catch (AggregateException ex)
			{
				LogConsumerTimeoutSync(ex);
			}
		}

		_tracking.Clear();
		_producerCancellationTokenSource.Dispose();

		_producerTask?.Dispose();
		_consumerTask?.Dispose();
		_cdcRepository.Dispose();
		_stateStore.Dispose();

		// For synchronous disposal, we need to handle the async queue disposal This is a known pattern for dual disposal (sync/async) in
		// .NET We suppress the warning as this is intentional for backward compatibility
		_ = _cdcQueue.Writer.TryComplete();

		_orderedEventProcessor.Dispose();
		_executionLock.Dispose();
	}

	/// <summary>
	/// Determines if a given LSN is empty (contains only zero bytes).
	/// </summary>
	/// <param name="lsn"> The LSN to check. </param>
	/// <returns> <c> true </c> if the LSN is empty; otherwise, <c> false </c>. </returns>
	private static bool IsEmptyLsn(IEnumerable<byte> lsn) => lsn.All(static b => b == 0);

	/// <summary>
	/// Converts a byte array to a hexadecimal string representation.
	/// </summary>
	/// <param name="bytes"> The byte array to convert. </param>
	/// <returns> A hexadecimal string representation of the byte array. </returns>
	private static string ByteArrayToHex(byte[] bytes) => $"0x{Convert.ToHexString(bytes)}";

	private async Task ProducerLoopAsync(byte[]? lowestStartLsn, CancellationToken cancellationToken)
	{
		try
		{
			using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ProducerCancellationToken);
			var combinedToken = combinedTokenSource.Token;
			var currentGlobalLsn = lowestStartLsn;
			var maxLsn = await _cdcRepository.GetMaxPositionAsync(combinedToken).ConfigureAwait(false);

			lock (_minHeapLock)
			{
				LogProducerLoopStarted(_minHeap.Count);
			}

			while (currentGlobalLsn != null && currentGlobalLsn.CompareLsn(maxLsn) <= 0)
			{
				combinedToken.ThrowIfCancellationRequested();

				foreach (var tableName in _tracking.Keys)
				{
					if (_tracking.TryGetValue(tableName, out var tableTracking) &&
						tableTracking.Lsn.CompareLsn(currentGlobalLsn) == 0)
					{
						await EnqueueTableChangesAsync(tableName, tableTracking.Lsn, tableTracking.SequenceValue, maxLsn, combinedToken)
							.ConfigureAwait(false);
					}
				}

				currentGlobalLsn = GetNextLsn();
			}

			LogNoMoreRecordsProducer();
		}
		catch (OperationCanceledException)
		{
			LogProducerCanceled();
		}
		catch (SqlException ex)
		{
			LogSqlErrorInProducer(ex);
			throw;
		}
		catch (Exception ex)
		{
			LogUnexpectedErrorInProducer(ex);
			throw;
		}
		finally
		{
			_producerStopped = true;
			_cdcQueue.Writer.Complete();
			LogProducerCompleted();
		}
	}

	private async Task EnqueueTableChangesAsync(
		string tableName,
		byte[] lastLsn,
		byte[]? lastSequenceValue,
		byte[] maxLsn,
		CancellationToken combinedToken)
	{
		var changeProcessingState = new ChangeProcessingState
		{
			TableName = tableName,
			Lsn = lastLsn,
			SequenceValue = lastSequenceValue,
			LastOperation = CdcOperationCodes.Unknown,
			TotalRowsReadInThisLsn = 0,
			PendingUpdateBefore = new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer()),
			PendingUpdateAfter = new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer()),
		};

		while (!combinedToken.IsCancellationRequested)
		{
			LogFetchingChanges(changeProcessingState);

			var producerBatchSize = Math.Min(_queueSize - _cdcQueue.Reader.Count, _dbConfig.ProducerBatchSize);
			var retryPolicy = _policyFactory.GetComprehensivePolicy();
			var changes = await retryPolicy.ExecuteAsync(() => _cdcRepository.FetchChangesAsync(
				tableName,
				producerBatchSize,
				changeProcessingState.Lsn,
				changeProcessingState.SequenceValue,
				changeProcessingState.LastOperation,
				combinedToken)).ConfigureAwait(false) as IList<CdcRow> ?? [];

			if (changes.Count == 0)
			{
				await HandleNoChangesAsync(changeProcessingState, combinedToken).ConfigureAwait(false);
				break;
			}

			changeProcessingState.TotalRowsReadInThisLsn += changes.Count;
			await ProcessCdcChangesAsync(changes, changeProcessingState, combinedToken).ConfigureAwait(false);
		}

		ValidateUnmatchedUpdates(changeProcessingState);

		var nextLsn = await _cdcLsnMapping.GetNextLsnAsync(tableName, changeProcessingState.Lsn, combinedToken).ConfigureAwait(false);

		LogTableEnqueued(tableName, changeProcessingState.Lsn, nextLsn, maxLsn);
		UpdateLsnAfterProcessing(tableName, nextLsn, maxLsn);
	}

	/// <summary>
	/// Processes a batch of CDC changes and creates data change events.
	/// </summary>
	private async Task ProcessCdcChangesAsync(
		IList<CdcRow> changes,
		ChangeProcessingState state,
		CancellationToken cancellationToken)
	{
		var events = new List<DataChangeEvent>();

		foreach (var record in changes)
		{
			ProcessCdcRecord(record, events, state);

			state.Lsn = record.Lsn;
			state.SequenceValue = record.SeqVal;
			state.LastOperation = record.OperationCode;
		}

		MatchPendingUpdates(events, state);

		if (events.Count > 0)
		{
			// Enqueue all events to the channel
			foreach (var evt in events)
			{
				await _cdcQueue.Writer.WriteAsync(evt, cancellationToken).ConfigureAwait(false);
			}
		}

		events.Clear();
		changes.Clear();
	}

	/// <summary>
	/// Processes a single CDC record and adds the appropriate event to the events list.
	/// </summary>
	private void ProcessCdcRecord(
		CdcRow record,
		List<DataChangeEvent> events,
		ChangeProcessingState state)
	{
		switch (record.OperationCode)
		{
			case CdcOperationCodes.Delete:
				events.Add(DataChangeEvent.CreateDeleteEvent(record));
				break;

			case CdcOperationCodes.Insert:
				events.Add(DataChangeEvent.CreateInsertEvent(record));
				break;

			case CdcOperationCodes.UpdateBefore when state.PendingUpdateAfter.TryGetValue(record.SeqVal, out var afterRecord):
				events.Add(DataChangeEvent.CreateUpdateEvent(record, afterRecord));
				_ = state.PendingUpdateAfter.Remove(record.SeqVal);
				break;

			case CdcOperationCodes.UpdateBefore:
				state.PendingUpdateBefore[record.SeqVal] = record;
				break;

			case CdcOperationCodes.UpdateAfter when state.PendingUpdateBefore.TryGetValue(record.SeqVal, out var beforeRecord):
				events.Add(DataChangeEvent.CreateUpdateEvent(beforeRecord, record));
				_ = state.PendingUpdateBefore.Remove(record.SeqVal);
				break;

			case CdcOperationCodes.UpdateAfter:
				state.PendingUpdateAfter[record.SeqVal] = record;
				break;

			default:
				LogUnknownOperation(record);
				break;
		}
	}

	/// <summary>
	/// Matches any remaining pending update pairs and creates update events.
	/// </summary>
	private static void MatchPendingUpdates(List<DataChangeEvent> events, ChangeProcessingState state)
	{
		// There shouldn't be any matching pairs here just ones with pairs in the next batch but just in case
		foreach (var seqVal in state.PendingUpdateBefore.Keys.ToList())
		{
			if (state.PendingUpdateAfter.TryGetValue(seqVal, out var afterRecord))
			{
				events.Add(DataChangeEvent.CreateUpdateEvent(state.PendingUpdateBefore[seqVal], afterRecord));
				_ = state.PendingUpdateBefore.Remove(seqVal);
				_ = state.PendingUpdateAfter.Remove(seqVal);
			}
		}
	}

	/// <summary>
	/// Handles the case when no changes are found for a table.
	/// </summary>
	private async Task HandleNoChangesAsync(ChangeProcessingState state, CancellationToken cancellationToken)
	{
		if (state.TotalRowsReadInThisLsn > 0)
		{
			LogSuccessfullyEnqueued(state.TotalRowsReadInThisLsn, state.TableName);
		}
		else
		{
			LogNoChangesFound(state.TableName);

			var commitTime = await _cdcLsnMapping.GetLsnToTimeAsync(state.Lsn, cancellationToken).ConfigureAwait(false);
			await UpdateTableLastProcessedAsync(state.TableName, state.Lsn, state.SequenceValue, commitTime, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Validates that there are no unmatched update records and logs/throws if there are.
	/// </summary>
	/// <exception cref="UnmatchedUpdateRecordsException"> </exception>
	private void ValidateUnmatchedUpdates(ChangeProcessingState state)
	{
		if (state.PendingUpdateBefore.Count > 0 || state.PendingUpdateAfter.Count > 0)
		{
			LogUnmatchedUpdates(state);
			throw new UnmatchedUpdateRecordsException(state.Lsn);
		}
	}

	/// <summary>
	/// Logs unmatched update records.
	/// </summary>
	private void LogUnmatchedUpdates(ChangeProcessingState state)
	{
		var unmatchedUpdates = new StringBuilder();
		foreach (var kvp in state.PendingUpdateBefore)
		{
			_ = unmatchedUpdates.Append(
					CultureInfo.CurrentCulture,
					$"Unmatched UpdateBefore at LSN {ByteArrayToHex(state.Lsn)}, SeqVal {ByteArrayToHex(kvp.Key)}")
				.Append(Environment.NewLine);
		}

		foreach (var kvp in state.PendingUpdateAfter)
		{
			_ = unmatchedUpdates.Append(
					CultureInfo.CurrentCulture,
					$"Unmatched UpdateAfter at LSN {ByteArrayToHex(state.Lsn)}, SeqVal {ByteArrayToHex(kvp.Key)}")
				.Append(Environment.NewLine);
		}

		LogUnmatchedUpdatePairs(Environment.NewLine, unmatchedUpdates.ToString());
	}

	/// <summary>
	/// Logs information about fetching CDC changes.
	/// </summary>
	private void LogFetchingChanges(ChangeProcessingState state) =>
		LogFetchingCdcChanges(
			state.TableName,
			ByteArrayToHex(state.Lsn),
			state.SequenceValue != null ? ByteArrayToHex(state.SequenceValue) : "null");

	/// <summary>
	/// Logs information about an unknown operation code.
	/// </summary>
	private void LogUnknownOperation(CdcRow record) =>
		LogUnknownOperationCode(record.OperationCode, record.Lsn, record.SeqVal);

	/// <summary>
	/// Logs information about table enqueuing.
	/// </summary>
	private void LogTableEnqueued(string tableName, byte[] currentLsn, byte[]? nextLsn, byte[] maxLsn) =>
		LogTableEnqueuedDetails(
			tableName,
			ByteArrayToHex(currentLsn),
			nextLsn != null ? ByteArrayToHex(nextLsn) : "null",
			ByteArrayToHex(maxLsn));

	/// <summary>
	/// Updates LSN tracking after processing a table.
	/// </summary>
	private void UpdateLsnAfterProcessing(string tableName, byte[]? nextLsn, byte[] maxLsn)
	{
		if (nextLsn != null && nextLsn.CompareLsn(maxLsn) < 0)
		{
			UpdateLsnTracking(tableName, nextLsn, seqVal: null);
		}
		else
		{
			UpdateLsnTracking(tableName, lsn: null, seqVal: null);
		}
	}

	/// <summary>
	/// Executes the consumer loop that processes CDC events in batches.
	/// </summary>
	/// <param name="eventHandler"> The handler to process data change events. </param>
	/// <param name="cancellationToken"> A cancellation token to stop processing. </param>
	/// <returns> The total number of events processed. </returns>
	private async Task<int> ConsumerLoopAsync(
		Func<DataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventHandler);

		LogConsumerLoopStarted();

		var totalProcessedCount = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			if (_disposedFlag == 1)
			{
				LogDisposalRequested();
				break;
			}

			if (_producerStopped && _cdcQueue.Reader.Count == 0)
			{
				LogNoMoreRecordsConsumer();

				break;
			}

			if (ShouldWaitForProducer)
			{
				LogWaitingForProducer();

				// Event-driven wait: block until data is available or channel closes This is more efficient than polling with exponential backoff
				if (!await _cdcQueue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
				{
					// Channel completed, no more data
					break;
				}

				continue;
			}

			try
			{
				cancellationToken.ThrowIfCancellationRequested();

				LogAttemptingDequeue();

				var stopwatch = ValueStopwatch.StartNew();

				var batch = await ChannelBatchUtilities.DequeueBatchAsync(_cdcQueue.Reader, _dbConfig.ConsumerBatchSize, cancellationToken)
					.ConfigureAwait(false);
				LogDequeuedMessages(batch.Length, stopwatch.Elapsed.TotalMilliseconds);
				LogProcessingBatch(batch.Length);

				await ProcessBatchAsync(batch, eventHandler, cancellationToken).ConfigureAwait(false);

				LogProcessedBatch(batch.Length, stopwatch.Elapsed.TotalMilliseconds);

				totalProcessedCount += batch.Length;
			}
			catch (OperationCanceledException)
			{
				LogConsumerCanceled();
			}
			catch (Exception ex)
			{
				LogErrorInConsumer(ex);
				throw;
			}
		}

		LogCompletedProcessing(totalProcessedCount);

		return totalProcessedCount;
	}

	private async Task ProcessBatchAsync(
		IReadOnlyList<DataChangeEvent> batch,
		Func<DataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(batch);
		ArgumentNullException.ThrowIfNull(eventHandler);

		using var batchActivity = CdcTelemetryConstants.ActivitySource.StartActivity("cdc.consume_batch");
		batchActivity?.SetTag("cdc.batch.size", batch.Count);

		var batchStopwatch = ValueStopwatch.StartNew();

		BatchSizeHistogram.Record(batch.Count);

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

				EventsProcessedCounter.Add(1, new TagList
				{
					{ CdcTelemetryConstants.Tags.CaptureInstance, changeEvent.TableName },
				});
			}
			catch (Exception ex)
			{
				EventsFailedCounter.Add(1, new TagList
				{
					{ CdcTelemetryConstants.Tags.CaptureInstance, changeEvent.TableName },
					{ CdcTelemetryConstants.Tags.ErrorType, ex.GetType().Name },
				});

				LogUnhandledException(changeEvent.TableName, ByteArrayToHex(changeEvent.Lsn), ByteArrayToHex(changeEvent.SeqVal),
					ex);

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
			await updateTablePolicy.ExecuteAsync(() => UpdateTableLastProcessedAsync(
				changeEvent.TableName,
				changeEvent.Lsn,
				changeEvent.SeqVal,
				changeEvent.CommitTime,
				cancellationToken)).ConfigureAwait(false);
		}

		BatchDurationHistogram.Record(batchStopwatch.Elapsed.TotalMilliseconds);
	}

	private async Task UpdateTableLastProcessedAsync(
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

		LogUpdatedState(tableName);
	}

	private async Task InitializeTrackingAsync(CancellationToken cancellationToken)
	{
		var processingStates = await _stateStore.GetLastProcessedPositionAsync(
			_dbConfig.DatabaseConnectionIdentifier,
			_dbConfig.DatabaseName,
			cancellationToken).ConfigureAwait(false) as ICollection<CdcProcessingState> ?? [];

		foreach (var captureInstance in _dbConfig.CaptureInstances)
		{
			var state = processingStates.FirstOrDefault(x => x.TableName.Equals(captureInstance, StringComparison.OrdinalIgnoreCase));

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

			UpdateLsnTracking(captureInstance, startLsn, seqVal);
		}
	}

	private void UpdateLsnTracking(string tableName, byte[]? lsn, byte[]? seqVal)
	{
		lock (_minHeapLock)
		{
			if (lsn == null)
			{
				if (_tracking.TryRemove(tableName, out _))
				{
					_ = _minHeap.RemoveWhere(item => string.Equals(item.TableName, tableName, StringComparison.Ordinal));
					LogRemovedLsn(tableName);
				}
			}
			else if (_tracking.TryGetValue(tableName, out var currentPos))
			{
				if (lsn.CompareLsn(currentPos.Lsn) > 0)
				{
					_tracking[tableName] = new CdcPosition(lsn, seqVal);
					_ = _minHeap.RemoveWhere(item => string.Equals(item.TableName, tableName, StringComparison.Ordinal));
					_ = _minHeap.Add((lsn, tableName));
					LogUpdatedLsn(tableName, ByteArrayToHex(lsn));
				}
			}
			else
			{
				_tracking[tableName] = new CdcPosition(lsn, seqVal);
				_ = _minHeap.Add((lsn, tableName));
				LogInsertedLsn(tableName, ByteArrayToHex(lsn));
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
	/// Handles cleanup when the application is stopping.
	/// </summary>
	private async Task OnApplicationStoppingAsync()
	{
		LogApplicationStopping();

		_producerStopped = true;
		await _producerCancellationTokenSource.CancelAsync().ConfigureAwait(false);

		LogProducerCancellationRequested();
		LogWaitingForConsumer();

		try
		{
			await DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogErrorDisposingOnShutdown(ex);
		}
	}

	/// <summary>
	/// Holds the state for processing CDC changes for a specific table.
	/// </summary>
	private sealed class ChangeProcessingState
	{
		public required string TableName { get; init; }

		public required byte[] Lsn { get; set; }

		public byte[]? SequenceValue { get; set; }

		public CdcOperationCodes LastOperation { get; set; }

		public int TotalRowsReadInThisLsn { get; set; }

		public required Dictionary<byte[], CdcRow> PendingUpdateBefore { get; init; }

		public required Dictionary<byte[], CdcRow> PendingUpdateAfter { get; init; }
	}

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.CdcRunStarting, LogLevel.Debug,
		"Starting new run at LSN {LowestStartLsn}")]
	private partial void LogStartingNewRun(string lowestStartLsn);

	[LoggerMessage(DataSqlServerEventId.CdcRunCompleted, LogLevel.Information,
		"Disposing CdcProcessor resources asynchronously.")]
	private partial void LogDisposingAsync();

	[LoggerMessage(DataSqlServerEventId.CdcRunSkippedNoChanges, LogLevel.Warning,
		"Disposing CdcProcessor but Consumer has not completed.")]
	private partial void LogConsumerNotCompletedAsync();

	[LoggerMessage(DataSqlServerEventId.CdcRunError, LogLevel.Warning,
		"Consumer did not complete in time during async disposal.")]
	private partial void LogConsumerTimeoutAsync(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcChangesRetrieved, LogLevel.Error,
		"Error disposing CdcProcessor asynchronously.")]
	private partial void LogErrorDisposingAsync(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcChangeProcessed, LogLevel.Information,
		"Disposing CdcProcessor resources synchronously.")]
	private partial void LogDisposingSync();

	[LoggerMessage(DataSqlServerEventId.CdcChangeProcessingError, LogLevel.Warning,
		"Disposing CdcProcessor but Consumer has not completed.")]
	private partial void LogConsumerNotCompletedSync();

	[LoggerMessage(DataSqlServerEventId.CdcBatchCompleted, LogLevel.Warning,
		"Consumer did not complete in time during synchronous disposal.")]
	private partial void LogConsumerTimeoutSync(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcBatchError, LogLevel.Warning,
		"MessageQueue disposal timed out in synchronous Dispose")]
	private partial void LogQueueDisposalTimeout();

	[LoggerMessage(DataSqlServerEventId.CdcProcessorStarting, LogLevel.Warning,
		"Error disposing MessageQueue synchronously")]
	private partial void LogErrorDisposingQueue(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcProcessorStopped, LogLevel.Debug,
		"Producer loop started with tracking state for {TableCount} tables")]
	private partial void LogProducerLoopStarted(int tableCount);

	[LoggerMessage(DataSqlServerEventId.CdcProcessorError, LogLevel.Information,
		"No more CDC records. Producer exiting.")]
	private partial void LogNoMoreRecordsProducer();

	[LoggerMessage(DataSqlServerEventId.CdcTableRegistered, LogLevel.Debug,
		"CdcProcessor Producer canceled")]
	private partial void LogProducerCanceled();

	[LoggerMessage(DataSqlServerEventId.CdcTableUnregistered, LogLevel.Error,
		"SQL error in CdcProcessor ProducerLoop")]
	private partial void LogSqlErrorInProducer(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcLsnUpdated, LogLevel.Error,
		"Unexpected Error in CdcProcessor ProducerLoop")]
	private partial void LogUnexpectedErrorInProducer(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcLsnRetrieved, LogLevel.Information,
		"CDC Producer has completed execution. Channel marked as complete.")]
	private partial void LogProducerCompleted();

	[LoggerMessage(DataSqlServerEventId.CdcLsnError, LogLevel.Debug,
		"Successfully enqueued {EnqueuedRowCount} CDC rows for {TableName}, advancing LSN.")]
	private partial void LogSuccessfullyEnqueued(int enqueuedRowCount, string tableName);

	[LoggerMessage(DataSqlServerEventId.CdcEnabledOnTable, LogLevel.Information,
		"No changes found for {TableName}, advancing LSN.")]
	private partial void LogNoChangesFound(string tableName);

	[LoggerMessage(DataSqlServerEventId.CdcDisabledOnTable, LogLevel.Error,
		"Unmatched UpdateBefore/UpdateAfter pairs detected at the end of LSN processing:{NewLine}{UnmatchedUpdates}")]
	private partial void LogUnmatchedUpdatePairs(string newLine, string unmatchedUpdates);

	[LoggerMessage(DataSqlServerEventId.CdcValidationStarted, LogLevel.Debug,
		"Fetching CDC changes: {TableName} - LSN {Lsn}, SeqVal {SeqVal}")]
	private partial void LogFetchingCdcChanges(string tableName, string lsn, string seqVal);

	[LoggerMessage(DataSqlServerEventId.CdcValidationCompleted, LogLevel.Warning,
		"Unknown operation {OperationCode} at Position {Position}, SequenceValue {SequenceValue}")]
	private partial void LogUnknownOperationCode(CdcOperationCodes operationCode, byte[] position, byte[] sequenceValue);

	[LoggerMessage(DataSqlServerEventId.CdcValidationError, LogLevel.Debug,
		"Table {CaptureInstance} enqueued: currentLSN={CurrentLsn}, nextLSN={NextLsn}, maxLSN={MaxLsn}")]
	private partial void LogTableEnqueuedDetails(string captureInstance, string currentLsn, string nextLsn, string maxLsn);

	[LoggerMessage(DataSqlServerEventId.CdcCleanupStarted, LogLevel.Information,
		"CDC Consumer loop started...")]
	private partial void LogConsumerLoopStarted();

	[LoggerMessage(DataSqlServerEventId.CdcCleanupCompleted, LogLevel.Warning,
		"ConsumerLoop: disposal requested, exit Excalibur.Data.")]
	private partial void LogDisposalRequested();

	[LoggerMessage(DataSqlServerEventId.CdcCleanupError, LogLevel.Information,
		"No more CDC records. Consumer is exiting gracefully.")]
	private partial void LogNoMoreRecordsConsumer();

	[LoggerMessage(DataSqlServerEventId.CdcHandlerInvoked, LogLevel.Information,
		"CDC Queue is empty. Waiting for examples.AdvancedSample.Producer...")]
	private partial void LogWaitingForProducer();

	[LoggerMessage(DataSqlServerEventId.CdcHandlerError, LogLevel.Debug,
		"Attempting to dequeue CDC messages...")]
	private partial void LogAttemptingDequeue();

	[LoggerMessage(DataSqlServerEventId.CdcPartitionProcessed, LogLevel.Debug,
		"Dequeued {BatchSize} messages in {ElapsedMs}ms")]
	private partial void LogDequeuedMessages(int batchSize, double elapsedMs);

	[LoggerMessage(DataSqlServerEventId.CdcPartitionError, LogLevel.Debug,
		"Processing batch of {BatchSize} CDC records")]
	private partial void LogProcessingBatch(int batchSize);

	[LoggerMessage(DataSqlServerEventId.CdcCheckpointCreated, LogLevel.Debug,
		"Processed {BatchSize} CDC records in {ElapsedMs}ms")]
	private partial void LogProcessedBatch(int batchSize, double elapsedMs);

	[LoggerMessage(DataSqlServerEventId.CdcCheckpointRestored, LogLevel.Debug,
		"Consumer canceled")]
	private partial void LogConsumerCanceled();

	[LoggerMessage(DataSqlServerEventId.CdcCheckpointError, LogLevel.Error,
		"Error in ConsumerLoop")]
	private partial void LogErrorInConsumer(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcRetryAttempted, LogLevel.Information,
		"Completed CDC processing, total events processed: {TotalEvents}")]
	private partial void LogCompletedProcessing(int totalEvents);

	[LoggerMessage(DataSqlServerEventId.CdcMaxRetriesExceeded, LogLevel.Critical,
		"Unhandled exception occurred while processing change event for table '{TableName}', LSN {Lsn}, SeqVal {SeqVal}.")]
	private partial void LogUnhandledException(string tableName, string lsn, string seqVal, Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcConfigurationLoaded, LogLevel.Information,
		"Updated state for {TableName}")]
	private partial void LogUpdatedState(string tableName);

	[LoggerMessage(DataSqlServerEventId.CdcConfigurationError, LogLevel.Debug,
		"Removed LSN for table {TableName}")]
	private partial void LogRemovedLsn(string tableName);

	[LoggerMessage(DataSqlServerEventId.CdcConnectionEstablished, LogLevel.Debug,
		"Updated LSN for table {TableName}: {Lsn}")]
	private partial void LogUpdatedLsn(string tableName, string lsn);

	[LoggerMessage(DataSqlServerEventId.CdcConnectionError, LogLevel.Debug,
		"Inserted new LSN for table {TableName}: {Lsn}")]
	private partial void LogInsertedLsn(string tableName, string lsn);

	[LoggerMessage(DataSqlServerEventId.CdcPollingStarted, LogLevel.Information,
		"Application is stopping. Cancelling CDCProcessor producer immediately.")]
	private partial void LogApplicationStopping();

	[LoggerMessage(DataSqlServerEventId.CdcPollingStopped, LogLevel.Information,
		"CDCProcessor Producer cancellation requested.")]
	private partial void LogProducerCancellationRequested();

	[LoggerMessage(DataSqlServerEventId.CdcPollingError, LogLevel.Information,
		"Waiting for CDCProcessor consumer to finish remaining work...")]
	private partial void LogWaitingForConsumer();

	[LoggerMessage(DataSqlServerEventId.CdcSchemaChangeDetected, LogLevel.Error,
		"Error while disposing CDCProcessor on application shutdown.")]
	private partial void LogErrorDisposingOnShutdown(Exception ex);
}
