// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;

using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Domain;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Processes Change Data Capture (CDC) changes by reading from a database, managing state, and invoking a specified event handler.
/// </summary>
/// <remarks>
/// <para>
/// Orchestrates a producer-consumer pipeline: the producer reads CDC rows from the database,
/// the consumer processes them via a caller-supplied event handler. Stale LSN positions are
/// recovered automatically when <see cref="CdcRecoveryOptions"/> is configured.
/// </para>
/// <para>
/// Implementation delegates to <see cref="CdcCheckpointManager"/> (LSN tracking and state persistence),
/// <see cref="CdcChangeDetector"/> (producer / CDC row fetching), and <see cref="CdcChangeApplier"/>
/// (consumer / event processing). This class owns the channel, the recovery retry loop, and
/// the disposal lifecycle.
/// </para>
/// </remarks>
public partial class CdcProcessor : ISqlServerCdcProcessor
{
	private protected readonly IDatabaseOptions _dbConfig;
	private readonly IDataAccessPolicyFactory _policyFactory;
	private readonly ILogger<CdcProcessor> _logger;

	// Composed subsystems
	private readonly CdcCheckpointManager _checkpointManager;
	private readonly CdcChangeDetector _changeDetector;
	private readonly CdcChangeApplier _changeApplier;

	// Disposal targets — kept for lifecycle management
	private readonly CdcRepository _cdcRepository;
	private readonly ISqlServerCdcStateStore _stateStore;
	private readonly OrderedEventProcessor _orderedEventProcessor = new();

	private readonly Channel<DataChangeEvent> _cdcQueue;
	private readonly int _queueSize;

	private readonly CdcFatalErrorHandler? _onFatalError;

	private readonly SemaphoreSlim _executionLock = new(1, 1);

	private readonly ConcurrentBag<Task> _backgroundTasks = [];

	private volatile bool _isRunning;

	private int _disposedFlag;

	private Task? _producerTask;

	private Task<int>? _consumerTask;

	private volatile bool _producerStopped;

	private readonly CancellationTokenSource _producerCancellationTokenSource = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcProcessor" /> class.
	/// </summary>
	/// <param name="appLifetime"> Provides notifications about application lifetime events. </param>
	/// <param name="dbConfig"> The database configuration for CDC processing. </param>
	/// <param name="cdcRepository"> The CDC repository for querying change data. </param>
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
			IDatabaseOptions dbConfig,
			CdcRepository cdcRepository,
			SqlConnection stateStoreConnection,
			IOptions<SqlServerCdcStateStoreOptions>? stateStoreOptions,
			IDataAccessPolicyFactory policyFactory,
			ILogger<CdcProcessor> logger,
			IOptions<CdcFatalErrorOptions>? fatalErrorOptions = null)
	{
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(cdcRepository);
		ArgumentNullException.ThrowIfNull(stateStoreConnection);
		ArgumentNullException.ThrowIfNull(policyFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_dbConfig = dbConfig;
		_cdcRepository = cdcRepository;
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

		// Compose subsystems
		_checkpointManager = new CdcCheckpointManager(dbConfig, cdcRepository, _stateStore, logger);
		_changeDetector = new CdcChangeDetector(cdcRepository, cdcRepository, dbConfig, policyFactory, _checkpointManager, logger);
		_changeApplier = new CdcChangeApplier(dbConfig, policyFactory, _checkpointManager, _orderedEventProcessor, logger, _onFatalError);

		_ = appLifetime.ApplicationStopping.Register(() =>
		{
			var task = Task.Factory.StartNew(
					OnApplicationStoppingAsync,
					CancellationToken.None,
					TaskCreationOptions.DenyChildAttach,
					TaskScheduler.Default)
				.Unwrap();
			_backgroundTasks.Add(task);

			// Drain completed tasks to prevent unbounded growth
			var snapshot = _backgroundTasks.ToArray();
			_backgroundTasks.Clear();
			foreach (var t in snapshot)
			{
				if (!t.IsCompleted)
				{
					_backgroundTasks.Add(t);
				}
			}
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
	public async Task<int> ProcessBatchAsync(
		Func<DataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		using var activity = CdcTelemetryConstants.ActivitySource.StartActivity("cdc.process");
		activity?.SetTag(CdcTelemetryConstants.TagNames.CaptureInstance, string.Join(",", _dbConfig.CaptureInstances));

		try
		{
			if (_isRunning)
			{
				throw new InvalidOperationException("CDC processor is already running.");
			}

			_isRunning = true;

			await _checkpointManager.InitializeTrackingAsync(cancellationToken).ConfigureAwait(false);

			var lowestStartLsn = _checkpointManager.GetNextLsn() ??
								 throw new InvalidOperationException("Cannot start processing: no valid minimum LSN found.");

			LogStartingNewRun(CdcChangeDetector.ByteArrayToHex(lowestStartLsn));

			_producerTask = Task.Factory.StartNew(
					() => ProducerLoopAsync(lowestStartLsn, cancellationToken),
					cancellationToken,
					TaskCreationOptions.LongRunning,
					TaskScheduler.Default)
				.Unwrap();
			_consumerTask = Task.Factory.StartNew(
					() => _changeApplier.ConsumerLoopAsync(
						_cdcQueue.Reader,
						eventHandler,
						() => _disposedFlag == 1,
						() => ShouldWaitForProducer,
						() => _producerStopped,
						cancellationToken),
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
		await DisposeCoreAsync().ConfigureAwait(false);
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
	protected virtual async ValueTask DisposeCoreAsync()
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
					using var disposalCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
					_ = await _consumerTask.WaitAsync(TimeSpan.FromMinutes(5), disposalCts.Token).ConfigureAwait(false);
				}
				catch (TimeoutException ex)
				{
					LogConsumerTimeoutAsync(ex);
				}
				catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
				{
					// Disposal timeout expired — continue cleanup
				}
			}

			// Await tracked background tasks
			foreach (var task in _backgroundTasks)
			{
				try
				{
					await task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
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

			_checkpointManager.Clear();
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
		}

		_checkpointManager.Clear();
		_producerCancellationTokenSource.Dispose();

		if (_producerTask?.IsCompleted == true)
		{
			_producerTask.Dispose();
		}

		if (_consumerTask?.IsCompleted == true)
		{
			_consumerTask.Dispose();
		}
		_cdcRepository.Dispose();
		_stateStore.Dispose();

		// For synchronous disposal, we need to handle the async queue disposal This is a known pattern for dual disposal (sync/async) in
		// .NET We suppress the warning as this is intentional for backward compatibility
		_ = _cdcQueue.Writer.TryComplete();

		_orderedEventProcessor.Dispose();
		_executionLock.Dispose();
	}

	/// <summary>
	/// Runs the producer loop with stale-position recovery. Delegates the core CDC iteration
	/// to <see cref="CdcChangeDetector.ProducerLoopCoreAsync"/> and wraps it with a retry loop
	/// that handles <see cref="SqlException"/>s indicating stale LSN positions.
	/// </summary>
	private async Task ProducerLoopAsync(byte[]? lowestStartLsn, CancellationToken cancellationToken)
	{
		var recoveryAttempts = 0;
		var currentStartLsn = lowestStartLsn;

		try
		{
			while (true) // Recovery retry loop
			{
				try
				{
					using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ProducerCancellationToken);
					await _changeDetector.ProducerLoopCoreAsync(currentStartLsn, _cdcQueue.Writer, _queueSize, combinedTokenSource.Token)
						.ConfigureAwait(false);
					break; // Success — exit recovery retry loop
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || ProducerCancellationToken.IsCancellationRequested)
				{
					LogProducerCanceled();
					break;
				}
				catch (SqlException ex) when (CdcStalePositionDetector.IsStalePositionException(ex))
				{
					recoveryAttempts++;
					var recoveryOptions = _dbConfig.RecoveryOptions;

					LogStalePositionDetected(
						CdcStalePositionDetector.GetStalePositionErrorNumber(ex)?.ToString(CultureInfo.InvariantCulture) ?? "unknown",
						recoveryAttempts);

					if (recoveryOptions is null || recoveryOptions.RecoveryStrategy == StalePositionRecoveryStrategy.Throw)
					{
						LogSqlErrorInProducer(ex);
						throw;
					}

					if (recoveryAttempts > recoveryOptions.MaxRecoveryAttempts)
					{
						LogRecoveryExhausted(recoveryAttempts, recoveryOptions.MaxRecoveryAttempts);
						throw new SqlServerCdcStalePositionException(
							CdcStalePositionDetector.CreateEventArgs(
								ex, _dbConfig.DatabaseConnectionIdentifier,
								databaseName: _dbConfig.DatabaseName),
							$"Stale position recovery exhausted after {recoveryAttempts} attempts.",
							ex);
					}

					currentStartLsn = await RecoverFromStalePositionAsync(ex, recoveryOptions, cancellationToken)
						.ConfigureAwait(false);

					if (recoveryOptions.RecoveryAttemptDelay > TimeSpan.Zero)
					{
						await Task.Delay(recoveryOptions.RecoveryAttemptDelay, cancellationToken).ConfigureAwait(false);
					}

					// Loop will retry with recovered LSN
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
			}
		}
		finally
		{
			_producerStopped = true;
			_cdcQueue.Writer.Complete();
			LogProducerCompleted();
		}
	}

	/// <summary>
	/// Attempts to recover from a stale CDC position by resetting tracking to a valid LSN range.
	/// </summary>
	private async Task<byte[]?> RecoverFromStalePositionAsync(
		SqlException ex,
		CdcRecoveryOptions recoveryOptions,
		CancellationToken cancellationToken)
	{
		byte[]? newPosition = null;

		switch (recoveryOptions.RecoveryStrategy)
		{
			case StalePositionRecoveryStrategy.FallbackToEarliest:
				foreach (var captureInstance in _dbConfig.CaptureInstances)
				{
					var minLsn = await _cdcRepository.GetMinPositionAsync(captureInstance, cancellationToken)
						.ConfigureAwait(false);
					_checkpointManager.UpdateLsnTracking(captureInstance, minLsn, seqVal: null);
				}
				newPosition = _checkpointManager.GetNextLsn();
				LogRecoveryAttempt("FallbackToEarliest",
					newPosition != null ? CdcChangeDetector.ByteArrayToHex(newPosition) : "null");
				break;

			case StalePositionRecoveryStrategy.FallbackToLatest:
				newPosition = await _cdcRepository.GetMaxPositionAsync(cancellationToken)
					.ConfigureAwait(false);
				foreach (var captureInstance in _dbConfig.CaptureInstances)
				{
					_checkpointManager.UpdateLsnTracking(captureInstance, newPosition, seqVal: null);
				}
				LogRecoveryAttempt("FallbackToLatest", CdcChangeDetector.ByteArrayToHex(newPosition));
				break;

			case StalePositionRecoveryStrategy.InvokeCallback:
				if (recoveryOptions.OnPositionReset is null)
				{
					throw new InvalidOperationException(
						$"{nameof(CdcRecoveryOptions.OnPositionReset)} callback is required when using " +
						$"{nameof(StalePositionRecoveryStrategy)}.{nameof(StalePositionRecoveryStrategy.InvokeCallback)} strategy.");
				}
				// Fall through to callback invocation below — callback decides the action
				foreach (var captureInstance in _dbConfig.CaptureInstances)
				{
					var minLsn = await _cdcRepository.GetMinPositionAsync(captureInstance, cancellationToken)
						.ConfigureAwait(false);
					_checkpointManager.UpdateLsnTracking(captureInstance, minLsn, seqVal: null);
				}
				newPosition = _checkpointManager.GetNextLsn();
				LogRecoveryAttempt("InvokeCallback",
					newPosition != null ? CdcChangeDetector.ByteArrayToHex(newPosition) : "null");
				break;
		}

		// Invoke callback if configured (for all strategies — enables logging/alerting)
		if (recoveryOptions.OnPositionReset is not null)
		{
			var args = CdcStalePositionDetector.CreateEventArgs(
				ex,
				_dbConfig.DatabaseConnectionIdentifier,
				newPosition: newPosition,
				databaseName: _dbConfig.DatabaseName);

			await recoveryOptions.OnPositionReset(args, cancellationToken).ConfigureAwait(false);
		}

		return newPosition;
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

	// ── Source-generated logging ──────────────────────────────────────────────

	// Orchestration
	[LoggerMessage(DataSqlServerEventId.CdcOrchestratorRunStarting, LogLevel.Debug,
		"Starting new run at LSN {LowestStartLsn}")]
	private partial void LogStartingNewRun(string lowestStartLsn);

	// Disposal (async)
	[LoggerMessage(DataSqlServerEventId.CdcOrchestratorDisposingAsync, LogLevel.Information,
		"Disposing CdcProcessor resources asynchronously.")]
	private partial void LogDisposingAsync();

	[LoggerMessage(DataSqlServerEventId.CdcOrchestratorConsumerNotCompleted, LogLevel.Warning,
		"Disposing CdcProcessor but Consumer has not completed.")]
	private partial void LogConsumerNotCompletedAsync();

	[LoggerMessage(DataSqlServerEventId.CdcOrchestratorConsumerTimeout, LogLevel.Warning,
		"Consumer did not complete in time during async disposal.")]
	private partial void LogConsumerTimeoutAsync(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcOrchestratorDisposeError, LogLevel.Error,
		"Error disposing CdcProcessor asynchronously.")]
	private partial void LogErrorDisposingAsync(Exception ex);

	// Disposal (sync)
	[LoggerMessage(DataSqlServerEventId.CdcOrchestratorDisposingSync, LogLevel.Information,
		"Disposing CdcProcessor resources synchronously.")]
	private partial void LogDisposingSync();

	[LoggerMessage(DataSqlServerEventId.CdcOrchestratorConsumerNotCompletedSync, LogLevel.Warning,
		"Disposing CdcProcessor but Consumer has not completed.")]
	private partial void LogConsumerNotCompletedSync();

	// Producer lifecycle (recovery wrapper)
	[LoggerMessage(DataSqlServerEventId.CdcProducerCanceled, LogLevel.Debug,
		"CdcProcessor Producer canceled")]
	private partial void LogProducerCanceled();

	[LoggerMessage(DataSqlServerEventId.CdcProducerSqlError, LogLevel.Error,
		"SQL error in CdcProcessor ProducerLoop")]
	private partial void LogSqlErrorInProducer(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcProducerUnexpectedError, LogLevel.Error,
		"Unexpected Error in CdcProcessor ProducerLoop")]
	private partial void LogUnexpectedErrorInProducer(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcProducerCompleted, LogLevel.Information,
		"CDC Producer has completed execution. Channel marked as complete.")]
	private partial void LogProducerCompleted();

	// Application stopping
	[LoggerMessage(DataSqlServerEventId.CdcApplicationStopping, LogLevel.Information,
		"Application is stopping. Cancelling CDCProcessor producer immediately.")]
	private partial void LogApplicationStopping();

	[LoggerMessage(DataSqlServerEventId.CdcProducerCancellationRequested, LogLevel.Information,
		"CDCProcessor Producer cancellation requested.")]
	private partial void LogProducerCancellationRequested();

	[LoggerMessage(DataSqlServerEventId.CdcWaitingForConsumer, LogLevel.Information,
		"Waiting for CDCProcessor consumer to finish remaining work...")]
	private partial void LogWaitingForConsumer();

	[LoggerMessage(DataSqlServerEventId.CdcDisposeShutdownError, LogLevel.Error,
		"Error while disposing CDCProcessor on application shutdown.")]
	private partial void LogErrorDisposingOnShutdown(Exception ex);

	// Stale position recovery (these were already semantically correct)
	[LoggerMessage(DataSqlServerEventId.CdcStalePositionDetected, LogLevel.Warning,
		"Stale CDC position detected (SQL error {SqlErrorNumber}). Recovery attempt {AttemptNumber}.")]
	private partial void LogStalePositionDetected(string sqlErrorNumber, int attemptNumber);

	[LoggerMessage(DataSqlServerEventId.CdcRecoveryAttempt, LogLevel.Information,
		"CDC recovery using strategy '{Strategy}', new position: {NewPosition}.")]
	private partial void LogRecoveryAttempt(string strategy, string newPosition);

	[LoggerMessage(DataSqlServerEventId.CdcRecoveryExhausted, LogLevel.Critical,
		"CDC stale position recovery exhausted after {Attempts} attempts (max: {MaxAttempts}). Processing will stop.")]
	private partial void LogRecoveryExhausted(int attempts, int maxAttempts);
}
