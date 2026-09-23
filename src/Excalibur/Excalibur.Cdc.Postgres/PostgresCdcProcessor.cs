// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc.Diagnostics;
using Excalibur.Cdc.Postgres.Diagnostics;
using System.Runtime.ExceptionServices;

using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Postgres CDC processor using logical replication with pgoutput protocol.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where processing resumes.</b> Both <see cref="StartAsync"/> and <see cref="ProcessBatchAsync"/> resume from
/// the replication slot's confirmed position, which PostgreSQL records durably alongside the write-ahead log. The
/// slot is acknowledged only after the changes of a transaction have been handed to your handler, so a restart
/// never resumes past a change that was not delivered. A change may be delivered again after a restart, so
/// handlers must be idempotent.
/// </para>
/// <para>
/// The position saved to the CDC state store is for observation, reported by
/// <see cref="GetCurrentPositionAsync"/> and useful for monitoring. It does not decide where processing resumes,
/// so editing or rewinding it does not cause a replay. To replay, reposition or recreate the replication slot.
/// </para>
/// </remarks>
public sealed partial class PostgresCdcProcessor : IPostgresCdcProcessor
{
	private readonly PostgresCdcOptions _options;
	private readonly IPostgresCdcStateStore _stateStore;
	private readonly ILogger<PostgresCdcProcessor> _logger;

	// optional fatal-handoff. When a fatal (non-retryable) error occurs the processor stops and
	// surfaces it loudly instead of an infinite silent reconnect loop. _onFatalError is invoked
	// with the in-flight event for a per-event fatal, or null for a connection/poll-level fatal.
	private readonly CdcFatalErrorHandler<PostgresDataChangeEvent>? _onFatalError;
	private readonly IMessageFailureClassifier? _failureClassifier;
	private readonly CdcFatalErrorOptions<PostgresDataChangeEvent> _fatalErrorOptions;
	private readonly TimeProvider _timeProvider;
	private readonly CdcHealthState? _healthState;
	private PostgresDataChangeEvent? _inFlightEvent;

	// The reconnect bound for the current StartAsync call, and the position confirmed when the current
	// attempt began. Both are touched only by the single consume loop the single-flight gate admits.
	private CdcTransientFailureBackoff? _backoff;
	private PostgresCdcPosition _attemptStartPosition;

	// SINGLE-ENTRY GATE. The fields below are per-STREAM state that the replication loop mutates as
	// messages arrive -- _currentTransactionId and _currentCommitTime are stamped by Begin and read by
	// every Handle* that follows it -- while this processor is registered TryAddSingleton and its batch
	// API is documented for a serverless timer trigger, where OVERLAPPING invocations are ordinary. Two
	// callers sharing one instance interleave that state: A's Begin(xid=100) is overwritten by B's
	// Begin(xid=200) and A's later changes are stamped with B's transaction identity. The same
	// interleaving reaches the durable checkpoint, which can then advance past changes whose handler
	// never ran.
	//
	// DETECTED AND REFUSED rather than serialized. A SemaphoreSlim would make the second caller WAIT,
	// but the replication loop is unbounded in time -- it blocks awaiting the next message while the
	// publication is quiet -- so waiting converts a safety bug into a liveness bug, and a timer trigger
	// firing on a schedule piles invocations up behind one that may never return. Refusing loudly is
	// also what the framework does in the same situation: EF Core's ConcurrencyDetector throws
	// InvalidOperationException on a second concurrent operation rather than silently serializing it.
	//
	// 0 = idle, 1 = a replication loop is running.
	private int _activeOperation;

	private LogicalReplicationConnection? _replicationConnection;
	private PostgresCdcPosition _currentPosition;
	private PostgresCdcPosition _confirmedPosition;
	private volatile bool _disposed;

	// Transaction state
	private uint _currentTransactionId;

	private DateTimeOffset _currentCommitTime;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcProcessor"/> class.
	/// </summary>
	/// <param name="options">The CDC options.</param>
	/// <param name="stateStore">The state store for position tracking.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="fatalErrorOptions">
	/// Optional fatal-error handling. When omitted (or its handler is <see langword="null"/>), a fatal
	/// error rethrows and stops the processor (fail-loud — never an infinite silent reconnect loop).
	/// </param>
	/// <param name="failureClassifier">
	/// Optional shared classifier deciding whether a processing error is fatal (non-retryable) or
	/// transient. When omitted, a conservative built-in fallback is used (only definitively non-retryable
	/// faults are fatal; everything else is retried with backoff).
	/// </param>
	/// <param name="timeProvider">
	/// The clock the reconnect backoff waits on and measures stable connections with; defaults to the
	/// system clock.
	/// </param>
	/// <param name="healthState">
	/// Where consecutive reconnect failures are reported for the CDC health check, when one is registered.
	/// </param>
	public PostgresCdcProcessor(
		IOptions<PostgresCdcOptions> options,
		IPostgresCdcStateStore stateStore,
		ILogger<PostgresCdcProcessor> logger,
		IOptions<CdcFatalErrorOptions<PostgresDataChangeEvent>>? fatalErrorOptions = null,
		IMessageFailureClassifier? failureClassifier = null,
		TimeProvider? timeProvider = null,
		CdcHealthState? healthState = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(stateStore);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();

		_stateStore = stateStore;
		_logger = logger;
		_fatalErrorOptions = fatalErrorOptions?.Value ?? new CdcFatalErrorOptions<PostgresDataChangeEvent>();
		_onFatalError = _fatalErrorOptions.OnFatalError;
		_failureClassifier = failureClassifier;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_healthState = healthState;
		_currentPosition = PostgresCdcPosition.Start;
		_confirmedPosition = PostgresCdcPosition.Start;
	}

	/// <inheritdoc/>
	public async Task StartAsync(
		Func<PostgresDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(eventHandler);

		if (!TryEnterSingleFlight())
		{
			LogConcurrentInvocationSkipped(nameof(StartAsync));
			return;
		}

		try
		{
			await StartCoreAsync(eventHandler, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			ExitSingleFlight();
		}
	}

	private async Task StartCoreAsync(
		Func<PostgresDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		LogStarting(_options.ReplicationSlotName, _options.PublicationName);

		// Load last confirmed position
		_confirmedPosition = await _stateStore
			.GetLastPositionAsync(_options.ProcessorId, _options.ReplicationSlotName, cancellationToken)
			.ConfigureAwait(false);

		_currentPosition = _confirmedPosition;

		LogResuming(_confirmedPosition.LsnString);

		_backoff = new CdcTransientFailureBackoff(
			_options.PollingInterval,
			_fatalErrorOptions.MaxReconnectDelay,
			_fatalErrorOptions.MaxConsecutiveTransientFailures,
			_timeProvider,
			_healthState);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				_backoff.BeginAttempt();
				_attemptStartPosition = _confirmedPosition;
				await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
				await ProcessChangesAsync(eventHandler, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				LogStopping();
				throw;
			}
			catch (Exception ex)
			{
				// delegate the fatal-vs-transient decision to the single shared guard
				// (CdcFatalGuard.Decide) — the same unit the regression lock binds — instead of an inline
				// `catch when IsFatal` filter. The durable checkpoint is advanced ONLY on the success path
				// (ConfirmCommitAsync inside ProcessChangesAsync); this catch never advances, and the
				// replication stream unwinds BEFORE that confirm on a fault, so a fault (fatal or transient)
				// never advances the checkpoint past the failing change (decision.AdvanceCheckpoint is false
				// on every fault). behavior is byte-preserved.
				var decision = CdcFatalGuard.Decide(ex, _failureClassifier);

				if (decision.Stop)
				{
					// Fatal (non-retryable) — stop loud, never an infinite silent reconnect.
					await StopTerminallyAsync(ex).ConfigureAwait(false);
					return; // the fatal handler took over → terminal; do not reconnect.
				}

				// Transient (non-fatal: decision.Stop == false). Counted BEFORE the in-flight event is cleared,
				// so a limit reached on a poisoned change still hands that change to the fatal handler.
				var outcome = _backoff.RecordTransientFailure();
				if (outcome.Exhausted)
				{
					await StopTerminallyAsync(new CdcRetryExhaustedException(outcome.ConsecutiveFailures, ex))
						.ConfigureAwait(false);
					return;
				}

				// Reconnect and retry from the un-advanced checkpoint.
				LogError(ex);
				_inFlightEvent = null;

				// Dispose connection to force reconnect
				if (_replicationConnection is not null)
				{
					await _replicationConnection.DisposeAsync().ConfigureAwait(false);
					_replicationConnection = null;
				}

				await Task.Delay(outcome.Delay, _timeProvider, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	public async Task<int> ProcessBatchAsync(
		Func<PostgresDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(eventHandler);

		if (!TryEnterSingleFlight())
		{
			// SPECIFIED BEHAVIOUR, NOT AN ERROR. See the remarks on TryEnterSingleFlight.
			LogConcurrentInvocationSkipped(nameof(ProcessBatchAsync));
			return 0;
		}

		try
		{
			return await ProcessBatchCoreAsync(eventHandler, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			ExitSingleFlight();
		}
	}

	private async Task<int> ProcessBatchCoreAsync(
		Func<PostgresDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		// Load last confirmed position
		_confirmedPosition = await _stateStore
			.GetLastPositionAsync(_options.ProcessorId, _options.ReplicationSlotName, cancellationToken)
			.ConfigureAwait(false);

		_currentPosition = _confirmedPosition;

		await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

		using var pollActivity = CdcActivitySource.StartPollActivity("Postgres");

		var count = 0;
		var slot = new PgOutputReplicationSlot(_options.ReplicationSlotName);

#pragma warning disable CS0618 // PgOutputReplicationOptions constructor is obsolete in newer Npgsql but no replacement available yet
		var replicationOptions = new PgOutputReplicationOptions(
			_options.PublicationName,
			protocolVersion: 1,
			binary: _options.Replication.UseBinaryProtocol);
#pragma warning restore CS0618

		await foreach (var message in _replicationConnection!
						   .StartReplication(slot, replicationOptions, cancellationToken)
						   .ConfigureAwait(false))
		{
			// One replication message can carry MORE THAN ONE change (multi-table TRUNCATE) — see
			// ProcessMessageAsync. Handing every change off inside THIS iteration, before the commit
			// boundary below, keeps the durable checkpoint from advancing past an unhandled relation.
			foreach (var changeEvent in await ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false))
			{
				// Apply the table filter symmetrically with ProcessChangesAsync — a change on a
				// non-matching table must not be handed to the handler here either, and the filter is
				// applied per change so a later-named truncate relation is not discarded with the first.
				if (!ShouldProcessTable(changeEvent.FullTableName))
				{
					continue;
				}

				await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);
				count++;
			}

			// Observed position advances as messages are read; it is NOT durably acknowledged here.
			_currentPosition = new PostgresCdcPosition(message.WalEnd);

			// Durably confirm/ack ONLY at a committed transaction boundary, after every change in the
			// transaction was successfully handed off — never mid-transaction, never for Begin/Relation/
			// unhandled messages, never past a change whose handler threw (a throw above skips this).
			if (message is CommitMessage commitMessage)
			{
				await ConfirmCommitAsync(commitMessage, cancellationToken).ConfigureAwait(false);

				// Enforce the batch-size limit ONLY at a committed transaction boundary. Breaking
				// mid-transaction (the previous behavior) left the transaction's handled-but-uncommitted
				// prefix un-confirmed, so it re-delivered on the next call — and if BatchSize is smaller
				// than the transaction's change count the prefix would re-deliver forever without
				// progressing. A large transaction is now fully processed then committed before the break.
				if (count >= _options.BatchSize)
				{
					break;
				}
			}
		}

		if (count > 0)
		{
			using var batchActivity = CdcActivitySource.StartProcessBatchActivity("Postgres", count);
		}

		return count;
	}

	/// <inheritdoc/>
	public Task<PostgresCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return Task.FromResult(_currentPosition);
	}

	/// <inheritdoc/>
	public async Task ConfirmPositionAsync(
		PostgresCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _stateStore
			.SavePositionAsync(_options.ProcessorId, _options.ReplicationSlotName, position, cancellationToken)
			.ConfigureAwait(false);

		_confirmedPosition = position;

		// Acknowledge to Postgres
		if (_replicationConnection is not null)
		{
			_replicationConnection!.SetReplicationStatus(position.Lsn);
		}

		LogConfirmed(position.LsnString);
	}

	/// <summary>
	/// Marks this processor as disposed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This method cannot release the underlying Postgres logical replication connection.</b>
	/// Npgsql's replication connection exposes only asynchronous disposal (it does not implement
	/// <see cref="IDisposable"/>), so a synchronous <c>using</c> block leaves the connection — and
	/// the replication slot it holds open on the server — alive. An unreleased slot accumulates
	/// WAL until the connection is eventually closed by the server or the process exits.
	/// </para>
	/// <para>
	/// Always dispose this processor with <c>await using</c> (see <see cref="DisposeAsync"/>) so
	/// the connection and its replication slot are released deterministically. This method exists
	/// only to satisfy <see cref="IDisposable"/>; when it detects a live replication connection it
	/// logs an error identifying the leaked slot instead of leaking it silently.
	/// </para>
	/// </remarks>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		if (_replicationConnection is not null)
		{
			// Npgsql's LogicalReplicationConnection implements only IAsyncDisposable — there is no
			// synchronous release path. Sync-over-async here is both banned repo-wide (RS0030) and
			// unsafe on a network connection, so the honest fix is to make the leak loud: only
			// DisposeAsync actually releases the connection and the replication slot it holds open.
			LogSyncDisposeLeaksReplicationConnection(_options.ReplicationSlotName);
		}

		_disposed = true;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		if (_replicationConnection is not null)
		{
			await _replicationConnection.DisposeAsync().ConfigureAwait(false);
		}

		_disposed = true;
	}

	private static async Task<List<PostgresDataChange>> ReadColumnsAsync(
		ReplicationTuple tuple,
		RelationMessage relation,
		CancellationToken cancellationToken)
	{
		var columns = new List<PostgresDataChange>();
		var columnIndex = 0;

		await foreach (var value in tuple.ConfigureAwait(false))
		{
			var column = relation.Columns[columnIndex];

			var dataChange = new PostgresDataChange
			{
				ColumnName = column.ColumnName,
				DataType = column.DataTypeId.ToString(),
				NewValue = await GetValueAsync(value, cancellationToken).ConfigureAwait(false),
				IsPrimaryKey = (column.Flags & RelationMessage.Column.ColumnFlags.PartOfKey) != 0,
			};

			columns.Add(dataChange);
			columnIndex++;
		}

		return columns;
	}

	private static async Task<object?> GetValueAsync(
		ReplicationValue value,
		CancellationToken cancellationToken)
	{
		if (value.IsDBNull)
		{
			return null;
		}

		if (value.IsUnchangedToastedValue)
		{
			return "<TOASTED>";
		}

		// Read as text - specific type handling can be added as needed
		return await value.Get<string>(cancellationToken).ConfigureAwait(false);
	}

	private static List<PostgresDataChange> BuildUpdateChanges(
		IReadOnlyList<PostgresDataChange> oldColumns,
		IReadOnlyList<PostgresDataChange> newColumns)
	{
		// If we have old values, merge them with new values
		if (oldColumns.Count == 0)
		{
			return [.. newColumns];
		}

		var oldByName = oldColumns.ToDictionary(c => c.ColumnName);

		return [.. newColumns.Select(newCol =>
		{
			var oldValue = oldByName.TryGetValue(newCol.ColumnName, out var oldCol)
				? oldCol.NewValue
				: null;

			return new PostgresDataChange
			{
				ColumnName = newCol.ColumnName,
				DataType = newCol.DataType,
				OldValue = oldValue,
				NewValue = newCol.NewValue,
				IsPrimaryKey = newCol.IsPrimaryKey,
			};
		})];
	}

	/// <summary>
	/// Claims the processor for one replication loop. Returns <see langword="false"/> when another loop is
	/// already running; the caller then returns without touching the stream.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The loser returns rather than throwing, and that is a deliberate consumer-facing choice.</b> The
	/// realistic collision here is not two hosts — it is ONE timer tick overlapping the previous one
	/// because the previous is still draining under load, which is exactly the shape of the published
	/// timer-trigger example. Throwing would make that example start failing under precisely the load it
	/// exists to handle, and an exception raised out of a timer callback is, in most hosts, an unobserved
	/// task exception or a crashed background service. A skip is benign: the next tick picks the work up.
	/// </para>
	/// <para>
	/// <b>A silent skip would be indistinguishable from a hang</b>, which is why this is not merely a
	/// no-op: every refusal is logged under
	/// <see cref="Diagnostics.CdcPostgresEventId.CdcConcurrentInvocationSkipped"/>, and
	/// <c>ProcessBatchAsync</c> documents that a return of zero may mean "another call held the
	/// processor" as well as "no changes were available". Specified single-flight behaviour is a
	/// contract; an undocumented one is a defect.
	/// </para>
	/// <para>
	/// The claim is what closes the interleaving: the per-stream fields below are instance state, so
	/// making them unreachable by two callers is the invariant. Note that this PREVENTS the corruption
	/// rather than making it INEXPRESSIBLE — anyone adding a third entry point must take this claim too.
	/// </para>
	/// </remarks>
	private bool TryEnterSingleFlight() => Interlocked.CompareExchange(ref _activeOperation, 1, 0) == 0;

	/// <summary>Releases the claim taken by <see cref="TryEnterSingleFlight"/>.</summary>
	private void ExitSingleFlight() => _ = Interlocked.Exchange(ref _activeOperation, 0);

	private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
	{
		if (_replicationConnection is not null)
		{
			return;
		}

		var connectionString = new NpgsqlConnectionStringBuilder(_options.ConnectionString)
		{
			// Required for replication connections
			ApplicationName = $"excalibur_cdc_{_options.ProcessorId}",
		}.ToString();

		_replicationConnection = new LogicalReplicationConnection(connectionString);
		await _replicationConnection!.Open(cancellationToken).ConfigureAwait(false);

		// Check if slot exists, create if needed
		if (_options.Replication.AutoCreateSlot)
		{
			await EnsureReplicationSlotAsync(cancellationToken).ConfigureAwait(false);
		}

		LogConnected();
	}

	private async Task EnsureReplicationSlotAsync(CancellationToken cancellationToken)
	{
		try
		{
			// Check if slot already exists by trying to create it
			// CreatePgOutputReplicationSlot will fail if slot exists
			_ = await _replicationConnection!
				.CreatePgOutputReplicationSlot(
					_options.ReplicationSlotName,
					slotSnapshotInitMode: LogicalSlotSnapshotInitMode.NoExport,
					cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			LogSlotCreated(_options.ReplicationSlotName);
		}
		catch (PostgresException ex) when (ex.SqlState == "42710") // duplicate_object
		{
			// Slot already exists, which is fine
			LogSlotExists(_options.ReplicationSlotName);
		}
	}

	private async Task ProcessChangesAsync(
		Func<PostgresDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		using var pollActivity = CdcActivitySource.StartPollActivity("Postgres");

		var slot = new PgOutputReplicationSlot(_options.ReplicationSlotName);

#pragma warning disable CS0618 // PgOutputReplicationOptions constructor is obsolete in newer Npgsql but no replacement available yet
		var replicationOptions = new PgOutputReplicationOptions(
			_options.PublicationName,
			protocolVersion: 1,
			binary: _options.Replication.UseBinaryProtocol);
#pragma warning restore CS0618

		// Resume from the REPLICATION SLOT, exactly as the batch loop does, never from the state-store row.
		// PostgreSQL starts a logical stream at the requested LSN or the slot's confirmed_flush_lsn, whichever
		// is greater, so passing the row could only ever move the start FORWARD of the slot, and a row ahead of
		// an undelivered change would skip that change for good. The slot's confirmed_flush_lsn advances only
		// from the flush position reported by SetReplicationStatus in ConfirmCommitAsync, which runs after the
		// transaction's changes were handed off, so it is the one position that cannot run ahead of delivery.
		// The state-store row is recorded for observation (GetCurrentPositionAsync, monitoring) and does not
		// decide where either loop resumes.
		var count = 0;

		await foreach (var message in _replicationConnection!
						   .StartReplication(slot, replicationOptions, cancellationToken)
						   .ConfigureAwait(false))
		{
			// One replication message can carry MORE THAN ONE change — a multi-table TRUNCATE arrives as a
			// single message naming every truncated relation (see ProcessMessageAsync). Every change is
			// handed off inside THIS iteration, before the commit boundary below, so a handler that throws
			// on any of them unwinds the replication stream before ConfirmCommitAsync: the durable
			// checkpoint cannot advance past a relation that was not handled.
			foreach (var changeEvent in await ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false))
			{
				// Apply table filter if configured — per change, so a truncate whose only configured table
				// is named after the first relation is still delivered.
				if (!ShouldProcessTable(changeEvent.FullTableName))
				{
					continue;
				}

				// Track the in-flight event so a fatal raised by the handler is attributed to it and the
				// fatal path unwinds before ConfirmCommitAsync (durable checkpoint not advanced past it).
				_inFlightEvent = changeEvent;
				await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);
				_inFlightEvent = null;
				count++;

				LogProcessed(changeEvent.ChangeType, changeEvent.FullTableName, changeEvent.Position.LsnString);
			}

			// Observed position advances as messages are read; it is NOT durably acknowledged here.
			_currentPosition = new PostgresCdcPosition(message.WalEnd);

			// Durably confirm/ack ONLY at a committed transaction boundary, after every change in the
			// transaction was successfully handed off (see ProcessBatchAsync) — never mid-transaction,
			// never past a change whose handler threw.
			if (message is CommitMessage commitMessage)
			{
				await ConfirmCommitAsync(commitMessage, cancellationToken).ConfigureAwait(false);

				// Progress is a position STRICTLY past the one confirmed when this attempt began. The slot can
				// re-send a transaction already confirmed to the state store, and re-confirming it proves nothing:
				// counting it as progress would let a fault that recurs right after it retry forever.
				if (_confirmedPosition.CompareTo(_attemptStartPosition) > 0)
				{
					_backoff?.RecordProgress();
				}
			}
		}

		if (count > 0)
		{
			using var batchActivity = CdcActivitySource.StartProcessBatchActivity("Postgres", count);
		}
	}

	/// <summary>
	/// Maps one replication message to every change it carries, in message order.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Returns a LIST, not a single change, because one pgoutput message can represent more than one
	/// change: PostgreSQL encodes a multi-table <c>TRUNCATE</c> (and the extra tables a <c>CASCADE</c>
	/// reaches) as a SINGLE <see cref="TruncateMessage"/> naming every truncated relation. Reporting only
	/// one change per message silently dropped every relation after the first, and dropped the truncate
	/// entirely when the only configured table was not the first relation named — while the transaction
	/// commit still advanced the durable checkpoint past it.
	/// </para>
	/// <para>
	/// Both processing loops hand off every element of this list inside the same loop iteration, BEFORE
	/// the commit boundary that calls <see cref="ConfirmCommitAsync"/>. A handler that throws on any
	/// element therefore unwinds the replication stream before the confirm — the same mechanism that
	/// already keeps the checkpoint behind the individual row changes of a multi-row transaction.
	/// </para>
	/// </remarks>
	private async Task<IReadOnlyList<PostgresDataChangeEvent>> ProcessMessageAsync(
		PgOutputReplicationMessage message,
		CancellationToken cancellationToken)
	{
		return message switch
		{
			BeginMessage begin => OneOrNone(HandleBegin(begin)),
			CommitMessage commit => OneOrNone(HandleCommit(commit)),
			InsertMessage insert => OneOrNone(await HandleInsertAsync(insert, cancellationToken).ConfigureAwait(false)),
			FullUpdateMessage fullUpdate => OneOrNone(await HandleFullUpdateAsync(fullUpdate, cancellationToken).ConfigureAwait(false)),
			IndexUpdateMessage indexUpdate => OneOrNone(await HandleIndexUpdateAsync(indexUpdate, cancellationToken).ConfigureAwait(false)),
			UpdateMessage update => OneOrNone(await HandleDefaultUpdateAsync(update, cancellationToken).ConfigureAwait(false)),
			FullDeleteMessage fullDelete => OneOrNone(await HandleFullDeleteAsync(fullDelete, cancellationToken).ConfigureAwait(false)),
			KeyDeleteMessage keyDelete => OneOrNone(await HandleKeyDeleteAsync(keyDelete, cancellationToken).ConfigureAwait(false)),
			DeleteMessage delete => OneOrNone(await HandleDefaultDeleteAsync(delete, cancellationToken).ConfigureAwait(false)),
			TruncateMessage truncate => HandleTruncate(truncate),
			_ => [], // Ignore relation, type, origin messages
		};
	}

	/// <summary>
	/// Lifts a message handler that yields at most one change into the list shape
	/// <see cref="ProcessMessageAsync"/> returns.
	/// </summary>
	private static IReadOnlyList<PostgresDataChangeEvent> OneOrNone(PostgresDataChangeEvent? changeEvent) =>
		changeEvent is null ? [] : [changeEvent];

	private PostgresDataChangeEvent? HandleBegin(BeginMessage begin)
	{
		_currentTransactionId = begin.TransactionXid;
		_currentCommitTime = begin.TransactionCommitTimestamp;
		return null;
	}

	private PostgresDataChangeEvent? HandleCommit(CommitMessage commit)
	{
		// Save position after each transaction
		_currentPosition = new PostgresCdcPosition(commit.TransactionEndLsn);
		return null;
	}

	/// <summary>
	/// Stops the consume loop for good: releases the replication connection, then hands the failure to the
	/// fatal-error handler when one is configured, or throws it.
	/// </summary>
	/// <remarks>
	/// Shared by a fatal error and an exhausted retry so the two cannot drift apart. Releasing the connection
	/// matters on both: a connection left open keeps the replication slot held, and the next instance would
	/// fail to start replication until it closed. Nothing here writes a position.
	/// </remarks>
	/// <param name="reason">The fatal error, or the exception describing an exhausted retry.</param>
	/// <returns>A task that completes only when the fatal-error handler took over.</returns>
	private async Task StopTerminallyAsync(Exception reason)
	{
		LogFatalError(reason);

		if (_replicationConnection is not null)
		{
			await _replicationConnection.DisposeAsync().ConfigureAwait(false);
			_replicationConnection = null;
		}

		if (_onFatalError is not null)
		{
			// In-flight event for a per-event failure; null for a connection-level one.
			await _onFatalError(reason, _inFlightEvent).ConfigureAwait(false);
			return;
		}

		// Rethrow preserving the original stack when the reason was thrown; an exhaustion wrapper was not.
		ExceptionDispatchInfo.Throw(reason);
	}

	/// <summary>
	/// Durably confirms a committed transaction boundary: persists and acknowledges the WAL up to the
	/// transaction's end LSN.
	/// </summary>
	/// <remarks>
	/// Called only after every change in the transaction was successfully handed off, so a failing handler
	/// (which aborts the loop before its transaction's commit) never advances the confirmed position past
	/// unhandled work — Postgres re-sends from the last confirmed commit boundary (at-least-once).
	/// </remarks>
	private async Task ConfirmCommitAsync(CommitMessage commit, CancellationToken cancellationToken)
	{
		var confirmed = new PostgresCdcPosition(commit.TransactionEndLsn);

		await _stateStore
			.SavePositionAsync(_options.ProcessorId, _options.ReplicationSlotName, confirmed, cancellationToken)
			.ConfigureAwait(false);

		_confirmedPosition = confirmed;
		_replicationConnection!.SetReplicationStatus(commit.TransactionEndLsn);
	}

	private async Task<PostgresDataChangeEvent> HandleInsertAsync(
		InsertMessage insert,
		CancellationToken cancellationToken)
	{
		var columns = await ReadColumnsAsync(insert.NewRow, insert.Relation, cancellationToken).ConfigureAwait(false);

		return PostgresDataChangeEvent.CreateInsert(
			new PostgresCdcPosition(insert.WalEnd),
			insert.Relation.Namespace,
			insert.Relation.RelationName,
			_currentTransactionId,
			_currentCommitTime,
			columns);
	}

	private async Task<PostgresDataChangeEvent> HandleFullUpdateAsync(
		FullUpdateMessage update,
		CancellationToken cancellationToken)
	{
		var newColumns = await ReadColumnsAsync(update.NewRow, update.Relation, cancellationToken).ConfigureAwait(false);
		var oldColumns = await ReadColumnsAsync(update.OldRow, update.Relation, cancellationToken).ConfigureAwait(false);

		// Build changes with old and new values
		var changes = BuildUpdateChanges(oldColumns, newColumns);
		var keyColumns = newColumns.Where(c => c.IsPrimaryKey).ToList();

		return PostgresDataChangeEvent.CreateUpdate(
			new PostgresCdcPosition(update.WalEnd),
			update.Relation.Namespace,
			update.Relation.RelationName,
			_currentTransactionId,
			_currentCommitTime,
			changes,
			keyColumns);
	}

	private async Task<PostgresDataChangeEvent> HandleIndexUpdateAsync(
		IndexUpdateMessage update,
		CancellationToken cancellationToken)
	{
		var newColumns = await ReadColumnsAsync(update.NewRow, update.Relation, cancellationToken).ConfigureAwait(false);
		var keyColumns = await ReadColumnsAsync(update.Key, update.Relation, cancellationToken).ConfigureAwait(false);

		return PostgresDataChangeEvent.CreateUpdate(
			new PostgresCdcPosition(update.WalEnd),
			update.Relation.Namespace,
			update.Relation.RelationName,
			_currentTransactionId,
			_currentCommitTime,
			newColumns,
			keyColumns);
	}

	private async Task<PostgresDataChangeEvent> HandleDefaultUpdateAsync(
		UpdateMessage update,
		CancellationToken cancellationToken)
	{
		var newColumns = await ReadColumnsAsync(update.NewRow, update.Relation, cancellationToken).ConfigureAwait(false);
		var keyColumns = newColumns.Where(c => c.IsPrimaryKey).ToList();

		return PostgresDataChangeEvent.CreateUpdate(
			new PostgresCdcPosition(update.WalEnd),
			update.Relation.Namespace,
			update.Relation.RelationName,
			_currentTransactionId,
			_currentCommitTime,
			newColumns,
			keyColumns);
	}

	private async Task<PostgresDataChangeEvent> HandleFullDeleteAsync(
		FullDeleteMessage delete,
		CancellationToken cancellationToken)
	{
		var keyColumns = await ReadColumnsAsync(delete.OldRow, delete.Relation, cancellationToken).ConfigureAwait(false);

		return PostgresDataChangeEvent.CreateDelete(
			new PostgresCdcPosition(delete.WalEnd),
			delete.Relation.Namespace,
			delete.Relation.RelationName,
			_currentTransactionId,
			_currentCommitTime,
			keyColumns);
	}

	private async Task<PostgresDataChangeEvent> HandleKeyDeleteAsync(
		KeyDeleteMessage delete,
		CancellationToken cancellationToken)
	{
		var keyColumns = await ReadColumnsAsync(delete.Key, delete.Relation, cancellationToken).ConfigureAwait(false);

		return PostgresDataChangeEvent.CreateDelete(
			new PostgresCdcPosition(delete.WalEnd),
			delete.Relation.Namespace,
			delete.Relation.RelationName,
			_currentTransactionId,
			_currentCommitTime,
			keyColumns);
	}

	private Task<PostgresDataChangeEvent> HandleDefaultDeleteAsync(
		DeleteMessage delete,
		CancellationToken cancellationToken)
	{
		// Suppress unused parameter warning - kept for API consistency
		_ = cancellationToken;

		// For default delete without REPLICA IDENTITY, we have no key information
		return Task.FromResult(PostgresDataChangeEvent.CreateDelete(
			new PostgresCdcPosition(delete.WalEnd),
			delete.Relation.Namespace,
			delete.Relation.RelationName,
			_currentTransactionId,
			_currentCommitTime,
			[]));
	}

	/// <summary>
	/// Maps a truncate message to one truncate change per truncated relation.
	/// </summary>
	/// <remarks>
	/// <c>TRUNCATE a, b</c> — and the further tables a <c>CASCADE</c> reaches — arrive as ONE message
	/// naming every affected relation, so this fans out over <see cref="TruncateMessage.Relations"/>
	/// rather than reporting only the first. Every change carries the same WAL position, transaction id
	/// and commit time, because they are one transactional act; the caller applies the table filter per
	/// change, so a configured table named after the first relation is still delivered.
	/// </remarks>
	private IReadOnlyList<PostgresDataChangeEvent> HandleTruncate(TruncateMessage truncate)
	{
		var relations = truncate.Relations;
		var position = new PostgresCdcPosition(truncate.WalEnd);
		var changes = new List<PostgresDataChangeEvent>(relations.Count);

		foreach (var relation in relations)
		{
			changes.Add(PostgresDataChangeEvent.CreateTruncate(
				position,
				relation.Namespace,
				relation.RelationName,
				_currentTransactionId,
				_currentCommitTime));
		}

		return changes;
	}

	private bool ShouldProcessTable(string fullTableName)
	{
		// If no tables configured, process all
		if (_options.TableNames.Length == 0)
		{
			return true;
		}

		return _options.TableNames.Any(t =>
			t.Equals(fullTableName, StringComparison.OrdinalIgnoreCase) ||
			fullTableName.EndsWith($".{t}", StringComparison.OrdinalIgnoreCase));
	}

	[LoggerMessage(CdcPostgresEventId.CdcProcessorStarting, LogLevel.Information,
		"Starting Postgres CDC processor for slot '{SlotName}' with publication '{Publication}'")]
	private partial void LogStarting(string slotName, string publication);

	[LoggerMessage(CdcPostgresEventId.CdcResumingFromPosition, LogLevel.Information, "Resuming from LSN position {Position}")]
	private partial void LogResuming(string position);

	[LoggerMessage(CdcPostgresEventId.CdcConcurrentInvocationSkipped, LogLevel.Information,
		"{Operation} found a replication loop already running on this processor and returned without "
		+ "processing. This is specified single-flight behaviour; the next invocation picks the work up. "
		+ "Persistent occurrences mean invocations are overlapping faster than a batch drains.")]
	private partial void LogConcurrentInvocationSkipped(string operation);

	[LoggerMessage(CdcPostgresEventId.CdcConnectedToReplicationStream, LogLevel.Information, "Connected to Postgres replication stream")]
	private partial void LogConnected();

	[LoggerMessage(CdcPostgresEventId.CdcCreatedReplicationSlot, LogLevel.Information, "Created replication slot '{SlotName}'")]
	private partial void LogSlotCreated(string slotName);

	[LoggerMessage(CdcPostgresEventId.CdcReplicationSlotExists, LogLevel.Debug, "Replication slot '{SlotName}' already exists")]
	private partial void LogSlotExists(string slotName);

	[LoggerMessage(CdcPostgresEventId.CdcProcessedChange, LogLevel.Debug, "Processed {ChangeType} on {TableName} at LSN {Position}")]
	private partial void LogProcessed(PostgresDataChangeType changeType, string tableName, string position);

	[LoggerMessage(CdcPostgresEventId.CdcConfirmedPosition, LogLevel.Debug, "Confirmed position {Position}")]
	private partial void LogConfirmed(string position);

	[LoggerMessage(CdcPostgresEventId.CdcProcessorStopping, LogLevel.Information, "Stopping Postgres CDC processor")]
	private partial void LogStopping();

	[LoggerMessage(CdcPostgresEventId.CdcProcessingError, LogLevel.Error, "Error in Postgres CDC processor")]
	private partial void LogError(Exception ex);

	[LoggerMessage(CdcPostgresEventId.CdcFatalError, LogLevel.Critical,
		"Fatal (non-retryable) error in Postgres CDC processor — stopping; the failure is surfaced to the configured handler or rethrown (no silent reconnect)")]
	private partial void LogFatalError(Exception ex);

	[LoggerMessage(CdcPostgresEventId.CdcSyncDisposeLeaksReplicationConnection, LogLevel.Error,
		"Dispose() was called synchronously on a Postgres CDC processor with an open replication connection. " +
		"The replication slot '{SlotName}' was NOT released and remains open on the server. Dispose this " +
		"processor with 'await using' (DisposeAsync) instead to release the connection and the slot.")]
	private partial void LogSyncDisposeLeaksReplicationConnection(string slotName);
}
