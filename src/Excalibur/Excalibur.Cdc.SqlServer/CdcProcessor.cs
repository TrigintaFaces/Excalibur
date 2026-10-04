// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;

using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
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
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<CdcProcessor> _logger;

	// Composed subsystems
	private readonly CdcCheckpointManager _checkpointManager;
	private readonly CdcChangeDetector _changeDetector;
	private readonly CdcChangeApplier _changeApplier;

	// Source and state repositories remain owned for disposal; composed workers share their storage seams.
	private readonly ICdcRepository _cdcRepository;
	private readonly ISqlServerCdcStateStore _stateStore;
	private readonly OrderedEventProcessor _orderedEventProcessor = new();

	// Replaced at the start of every invocation rather than shared across all of them. A channel's writer
	// can be completed only once and never re-opened, and the producer completes it on the way out -- so a
	// single channel built in the constructor serves exactly ONE invocation, and the second finds a closed
	// writer. This field is safe to reassign because the execution lock admits one invocation at a time AND
	// the join below guarantees no half of the previous one is still running when the next begins; without
	// that guarantee, swapping it would hand a live consumer a channel nobody is writing to.
	private Channel<DataChangeEvent> _cdcQueue;
	private readonly int _queueSize;

	private readonly CdcFatalErrorHandler<DataChangeEvent>? _onFatalError;

	// Optional single-active-consumer coordination. Null in single-instance deployments (runs unconditionally).
	private readonly ILeaderElection? _leaderElection;

	// Optional shared failure classifier. Routes the checkpoint-write fatal decision through the same
	// CdcFatalGuard the other providers use; when null, CdcFatalGuard's conservative fallback still classifies
	// CdcLeadershipSupersededException as fatal, so a superseded leader stops rather than retries.
	private readonly IMessageFailureClassifier? _failureClassifier;

#pragma warning disable CA2213 // Managed-only semaphore; queued callers must still acquire/release after disposal. AvailableWaitHandle is never used.
	private readonly SemaphoreSlim _executionLock = new(1, 1);
#pragma warning restore CA2213

	private readonly Lock _disposeGate = new();
	private readonly CancellationTokenRegistration _stoppingRegistration;
	private readonly CancellationToken _stoppingToken;
	private Task? _disposeTask;

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
	/// <param name="stateStoreConnectionFactory"> Supplies a connection per CDC-state operation. </param>
	/// <param name="stateStoreOptions"> The CDC state store options. </param>
	/// <param name="policyFactory"> The factory for creating data access policies. </param>
	/// <param name="timeProvider">
	/// The time provider used for the stale-position recovery backoff delay, so recovery retries are
	/// deterministically testable without wall-clock sleeps.
	/// </param>
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
			Func<IDbConnection> stateStoreConnectionFactory,
			IOptions<SqlServerCdcStateStoreOptions>? stateStoreOptions,
			IDataAccessPolicyFactory policyFactory,
			TimeProvider timeProvider,
			ILogger<CdcProcessor> logger,
			IOptions<CdcFatalErrorOptions<DataChangeEvent>>? fatalErrorOptions = null)
		: this(appLifetime, dbConfig, cdcRepository, stateStoreConnectionFactory,
			   stateStoreOptions, policyFactory, timeProvider, logger, fatalErrorOptions,
			   idempotencyFilter: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcProcessor"/> class with an optional
	/// idempotency filter for deduplicating replayed events.
	/// </summary>
	/// <param name="appLifetime">The application lifetime service for graceful shutdown.</param>
	/// <param name="dbConfig">The database configuration options.</param>
	/// <param name="cdcRepository">The CDC repository for reading change data.</param>
	/// <param name="stateStoreConnectionFactory">Supplies a connection per CDC-state operation.</param>
	/// <param name="stateStoreOptions">The CDC state store options.</param>
	/// <param name="policyFactory">The factory for creating data access policies.</param>
	/// <param name="timeProvider">
	/// The time provider used for the stale-position recovery backoff delay, so recovery retries are
	/// deterministically testable without wall-clock sleeps.
	/// </param>
	/// <param name="logger">The logger.</param>
	/// <param name="fatalErrorOptions">Optional fatal error handler options.</param>
	/// <param name="idempotencyFilter">Optional idempotency filter for deduplicating replayed CDC events.</param>
	/// <param name="leaderElection">
	/// Optional leader election. When supplied, the processor is a single-active consumer: only the current
	/// leader runs a batch, and its leadership tenure's fencing token guards the checkpoint write (a demoted
	/// leader's stale-token write is rejected by the state store's non-decreasing CAS). When <see langword="null"/>
	/// (single-instance deployments) the processor runs unconditionally, as before.
	/// </param>
	/// <param name="failureClassifier">
	/// Optional shared failure classifier. Routes the checkpoint-write fatal decision through the same
	/// <see cref="CdcFatalGuard"/> the other providers use. When <see langword="null"/>, the guard's conservative
	/// fallback still classifies a superseded-leadership fault as fatal, so a demoted leader stops rather than spins.
	/// </param>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Maintainability",
		"CA1506:AvoidExcessiveClassCoupling",
		Justification = "CdcProcessor is the CDC composition root: it wires the detector, applier, checkpoint manager, state store, policy factory, and (optionally) leader election. The coupling is inherent to a composition root and is the cleaner alternative to a parameter object that would merely relocate it.")]
	internal CdcProcessor(
			IHostApplicationLifetime appLifetime,
			IDatabaseOptions dbConfig,
			CdcRepository cdcRepository,
			Func<IDbConnection> stateStoreConnectionFactory,
			IOptions<SqlServerCdcStateStoreOptions>? stateStoreOptions,
			IDataAccessPolicyFactory policyFactory,
			TimeProvider timeProvider,
			ILogger<CdcProcessor> logger,
			IOptions<CdcFatalErrorOptions<DataChangeEvent>>? fatalErrorOptions,
			ICdcIdempotencyFilter? idempotencyFilter,
			ILeaderElection? leaderElection = null,
			IMessageFailureClassifier? failureClassifier = null)
	{
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(cdcRepository);
		ArgumentNullException.ThrowIfNull(stateStoreConnectionFactory);
		ArgumentNullException.ThrowIfNull(policyFactory);
		ArgumentNullException.ThrowIfNull(timeProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_dbConfig = dbConfig;
		_cdcRepository = cdcRepository;
		_timeProvider = timeProvider;
		_stateStore = stateStoreOptions is null
				? new CdcStateStore(stateStoreConnectionFactory)
				: new CdcStateStore(stateStoreConnectionFactory, stateStoreOptions);
		_logger = logger;
		_leaderElection = leaderElection;
		_failureClassifier = failureClassifier;
		_queueSize = _dbConfig.QueueSize;
		_cdcQueue = CreateQueue(_dbConfig.QueueSize);
		_onFatalError = fatalErrorOptions?.Value.OnFatalError;

		// Compose subsystems. The checkpoint manager writes each checkpoint under the fencing token PINNED for
		// the batch (set at the batch-start leadership gate in ProcessBatchAsync), so the state-store CAS rejects
		// a demoted (split-brain) leader's stale-token write instead of a mid-batch-null token silently bypassing it.
		_checkpointManager = new CdcCheckpointManager(
			dbConfig,
			cdcRepository,
			_stateStore,
			logger, policyFactory);
		_changeDetector = new CdcChangeDetector(cdcRepository, cdcRepository, dbConfig, policyFactory, _checkpointManager, logger);
		_changeApplier = new CdcChangeApplier(dbConfig, policyFactory, _checkpointManager, _orderedEventProcessor, logger, _onFatalError, idempotencyFilter, _failureClassifier);

		_stoppingToken = _producerCancellationTokenSource.Token;
		_stoppingRegistration = appLifetime.ApplicationStopping.Register(() =>
		{
			try { _producerCancellationTokenSource.Cancel(); }
			catch (Exception ex) { LogErrorDisposingOnShutdown(ex); }
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
		using var invocationStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stoppingToken);
		cancellationToken = invocationStop.Token;

		using var activity = CdcTelemetryConstants.ActivitySource.StartActivity("cdc.process");
		activity?.SetTag(CdcTelemetryConstants.TagNames.CaptureInstance, string.Join(",", _dbConfig.CaptureInstances));

		await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);
			if (_isRunning)
			{
				throw new InvalidOperationException("CDC processor is already running.");
			}

			_isRunning = true;

            var totalProcessed = 0;
            for (var recoveryAttempt = 0; ; recoveryAttempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
			// Single-active-consumer gate + fencing-token pin. Read CurrentLeadership ONCE: if fencing is
			// configured and this instance is not (or no longer) the leader, stand by. Otherwise PIN the
			// leadership tenure's fencing token for the WHOLE batch — a mid-batch demotion does NOT mutate the
			// pinned value, so the now-stale token loses the state-store CAS (0 rows → CdcLeadershipSupersededException)
			// instead of a mid-batch-null token silently bypassing the guard. Null leader election → pinned null
			// (single-instance, the only legitimate unfenced path); a non-fencing provider yields a null token
			// (unfenced, as before). The gate decision and the pinned token derive from the SAME snapshot, so
			// there is no read-again window between them.
			long? pinnedFencingToken = null;
			if (_leaderElection is not null)
			{
				if (_leaderElection.CurrentLeadership is not { } leadership)
				{
					activity?.SetTag("cdc.standby", "not-leader");
					return totalProcessed;
				}

				pinnedFencingToken = leadership.FencingToken;
			}

			_checkpointManager.SetBatchFencingToken(pinnedFencingToken);

			await _checkpointManager.InitializeTrackingAsync(cancellationToken).ConfigureAwait(false);
			if (!_checkpointManager.HasReadableWindow)
			{
				cancellationToken.ThrowIfCancellationRequested();
				return totalProcessed;
			}

			var lowestStartLsn = _checkpointManager.GetNextLsn() ??
								 throw new InvalidOperationException("Cannot start processing: no valid minimum LSN found.");

			LogStartingNewRun(CdcChangeDetector.ByteArrayToHex(lowestStartLsn));

			// The producer and the consumer are two halves of ONE invocation and must live and die together.
			// This source links the caller's token so either half can stop the other: when one settles as
			// faulted the survivor is cancelled rather than left running, which is what makes the join below
			// bounded. Without it a consumer fault strands the producer forever, because a bounded channel in
			// Wait mode blocks a writer until a reader takes an item and a faulted consumer never will.
			using var batchFaultSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var batchToken = batchFaultSource.Token;

			// A fresh queue and a cleared stop flag for THIS invocation. Both are consumed by the producer's
			// exit -- it completes the writer and raises the flag -- and neither can be un-consumed, so an
			// invocation that inherited them from its predecessor would find a writer it cannot write to and
			// a flag already telling the consumer to stop. That is why polling the same processor a second
			// time delivered nothing: the first poll left both in their terminal state.
			_cdcQueue = CreateQueue(_queueSize);
			_producerStopped = false;
            _changeApplier.ResetBatchAccounting();

			_producerTask = Task.Factory.StartNew(
					() => ProducerLoopAsync(lowestStartLsn, batchToken),
					batchToken,
					TaskCreationOptions.DenyChildAttach,
					TaskScheduler.Default)
				.Unwrap();
			_consumerTask = Task.Factory.StartNew(
					() => _changeApplier.ConsumerLoopAsync(
						_cdcQueue.Reader,
						eventHandler,
						() => _disposedFlag == 1,
						() => ShouldWaitForProducer,
						() => _producerStopped,
						batchToken),
					batchToken,
					TaskCreationOptions.DenyChildAttach,
					TaskScheduler.Default)
				.Unwrap();

                try
                {
                    await JoinBothHalvesAsync(_producerTask, _consumerTask, batchFaultSource).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsProducerStaleFailure(ex))
                {
                    var recovery = _dbConfig.RecoveryOptions;
                    if (recovery is null || recovery.RecoveryStrategy == StalePositionRecoveryStrategy.Throw || recoveryAttempt >= recovery.MaxRecoveryAttempts)
                    {
                        throw;
                    }
                    recovery.Validate();
                    LogStalePositionDetected(ex is SqlException sourceError ? sourceError.Number.ToString(CultureInfo.InvariantCulture) : "bounds-changed", recoveryAttempt + 1);
                    if (recovery.RecoveryAttemptDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(recovery.RecoveryAttemptDelay, _timeProvider, cancellationToken).ConfigureAwait(false);
                    }
                    // The join completed before this catch. The next attempt reloads durable
                    // checkpoints and leadership, and creates a fresh queue and cancellation source.
                    continue;
                }
                finally
                {
                    totalProcessed = checked(totalProcessed + _changeApplier.CompletedBatchEventCount);
                }
                cancellationToken.ThrowIfCancellationRequested();
                activity?.SetTag("cdc.events.total", totalProcessed);
                return totalProcessed;
            }
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
	protected virtual ValueTask DisposeCoreAsync()
	{
		lock (_disposeGate)
		{
			if (_disposeTask is null)
			{
				Interlocked.Exchange(ref _disposedFlag, 1);
				_disposeTask = DisposeOwnedResourcesAsync();
			}
			return new ValueTask(_disposeTask);
		}
	}

	private async Task DisposeOwnedResourcesAsync()
	{
		LogDisposingAsync();
		await _stoppingRegistration.DisposeAsync().ConfigureAwait(false);
		try { await _producerCancellationTokenSource.CancelAsync().ConfigureAwait(false); }
		catch (Exception ex) { LogErrorDisposingAsync(ex); }

		// ProcessBatch releases this only after joining both workers. Never clear state
		// or dispose connections while an admitted invocation still owns them.
		await _executionLock.WaitAsync().ConfigureAwait(false);
		try
		{
			_checkpointManager.Clear();
			_ = _cdcQueue.Writer.TryComplete();
			try { await _cdcRepository.DisposeAsync().ConfigureAwait(false); }
			finally
			{
				try { await _stateStore.DisposeAsync().ConfigureAwait(false); }
				finally { await _orderedEventProcessor.DisposeAsync().ConfigureAwait(false); }
			}
		}
		finally
		{
			_producerCancellationTokenSource.Dispose();
			// Keep the managed semaphore alive for already queued callers, which reject
			// the disposed instance after acquisition and then release it themselves.
			_ = _executionLock.Release();
		}
	}

	/// <summary>Releases resources after all admitted processing has stopped.</summary>
	/// <param name="disposing">Whether managed resources should be released.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
#pragma warning disable RS0030 // Legacy IDisposable must join the same shutdown as IAsyncDisposable before releasing owned resources.
			DisposeCoreAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore RS0030
		}
	}

	/// <summary>
	/// Creates the bounded queue one invocation hands between its producer and its consumer.
	/// </summary>
	/// <param name="queueSize">The bound, from the configured queue size.</param>
	/// <returns>A fresh queue owned by a single invocation.</returns>
	private static Channel<DataChangeEvent> CreateQueue(int queueSize) =>
		Channel.CreateBounded<DataChangeEvent>(new BoundedChannelOptions(queueSize)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true, // Only ConsumerLoopAsync reads from the channel
			SingleWriter = true, // Only ProducerLoopAsync writes to the channel
			AllowSynchronousContinuations = false,
		});

	/// <summary>
	/// Waits for BOTH halves of an invocation to terminate, cancelling the survivor when one of them faults,
	/// and surfaces the causal failure rather than a derived one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Awaiting the two halves in sequence is not a join. It waits for a specific one FIRST, so whichever
	/// half is waited on second is never observed when the first does not return — and each half has a
	/// failure mode that makes the other unable to return on its own:
	/// </para>
	/// <para>
	/// A consumer fault leaves the producer blocked writing into a full bounded channel with no remaining
	/// reader, so awaiting the producer first never completes and the invocation hangs with no deadline.
	/// A producer fault propagates out of the first await, which skips the second entirely and releases the
	/// invocation's lock with the consumer still running — so the next invocation starts while a handler
	/// from the previous one is still applying changes, and the batch identity that consumer completes
	/// under is no longer the one it began with.
	/// </para>
	/// <para>
	/// Cancelling on fault is what bounds this: a pending channel write observes the token and unblocks, so
	/// the surviving half terminates instead of being abandoned. The cancellation the survivor then reports
	/// is a CONSEQUENCE of the fault, never its cause, which is why the first-settled fault is rethrown in
	/// preference to whatever the joined wait surfaces.
	/// </para>
	/// <para>
	/// <b>Two costs, stated rather than discovered.</b> First, waiting for both halves means a handler that
	/// never returns holds this processor's execution lock until shutdown. That is the honest outcome — the
	/// feed genuinely cannot proceed past a change it has neither applied nor abandoned — and it is
	/// preferable to the alternative it replaces, which released the lock and let a second invocation run
	/// alongside the first.
	/// </para>
	/// <para>
	/// Second, termination is <em>eventual</em> rather than prompt. Cancellation unblocks a channel write
	/// immediately, but a retry wait already in flight inside the data-access policy does not observe it:
	/// the producer's fetch is executed through the retry policy's token-less overload, so the join waits
	/// out the remaining backoff before the task completes. The join is therefore bounded by the policy's
	/// own retry budget, not by the cancellation. Threading the token through those call sites is what would
	/// make it prompt.
	/// </para>
	/// </remarks>
	/// <param name="producer">The producer half.</param>
	/// <param name="consumer">The consumer half.</param>
	/// <param name="faultSource">The invocation-scoped source cancelled when either half faults.</param>
	/// <returns>A task that completes when both halves have terminated.</returns>
	private static async Task JoinBothHalvesAsync(Task producer, Task consumer, CancellationTokenSource faultSource)
	{
		var firstSettled = await Task.WhenAny(producer, consumer).ConfigureAwait(false);

        if (firstSettled.IsFaulted || firstSettled.IsCanceled)
        {
            try { await faultSource.CancelAsync().ConfigureAwait(false); }
            catch (Exception) { /* A callback failure must not bypass the join or hide the causal task failure. */ }
        }

		try
		{
			await Task.WhenAll(producer, consumer).ConfigureAwait(false);
		}
		catch when (firstSettled.IsFaulted)
		{
            var other = ReferenceEquals(firstSettled, producer) ? consumer : producer;
            if (other.IsFaulted)
            {
                throw new AggregateException("Both CDC batch workers failed.",
                    firstSettled.Exception!.InnerExceptions.Concat(other.Exception!.InnerExceptions));
            }

			// Rethrow the ORIGINAL failure with its stack intact. Task.WhenAll surfaces whichever exception
			// it happens to pick, which after a cancellation may be the survivor's OperationCanceledException
			// -- an effect of the fault reported in place of the fault.
			ExceptionDispatchInfo.Capture(firstSettled.Exception!.InnerExceptions[0]).Throw();
			throw;
		}
	}

    private bool IsProducerStaleFailure(Exception exception) => _producerTask is { IsFaulted: true } &&
        _consumerTask is { IsFaulted: false } &&
        _producerTask.Exception!.InnerExceptions.Contains(exception) &&
        (exception is SqlServerCdcStalePositionException ||
         (exception is SqlException sql && CdcStalePositionDetector.IsStalePositionException(sql)));

    /// <summary>Produces one batch. Recovery happens only after both batch workers terminate.</summary>
    private async Task ProducerLoopAsync(byte[]? lowestStartLsn, CancellationToken cancellationToken)
    {
        try
        {
            using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ProducerCancellationToken);
            await _changeDetector.ProducerLoopCoreAsync(lowestStartLsn, _cdcQueue.Writer, _queueSize, combined.Token).ConfigureAwait(false);
        }
        finally
        {
            _producerStopped = true;
            _cdcQueue.Writer.TryComplete();
            LogProducerCompleted();
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
