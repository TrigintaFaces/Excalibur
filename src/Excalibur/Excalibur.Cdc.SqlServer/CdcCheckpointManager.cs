// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;

using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Manages CDC checkpoint state and LSN tracking for table processing positions.
/// </summary>
/// <remarks>
/// Extracted from <see cref="CdcProcessor"/> to separate checkpoint management
/// from change detection and change application concerns.
/// </remarks>
internal sealed partial class CdcCheckpointManager : ICdcFetchProgress
{
	private readonly IDatabaseOptions _dbConfig;
	private readonly ICdcRepository _cdcRepository;
	private readonly ISqlServerCdcStateStore _stateStore;
	private readonly ILogger _logger;

	// The leadership tenure's fencing token PINNED for the current batch (set once at the batch-start gate via
	// SetBatchFencingToken), or null when leader-election fencing is not configured / the provider issues no
	// token (single-active is then unenforced and every write is unfenced). Pinned — NOT re-read per write — so a
	// demotion mid-batch does NOT flip it to null: the now-stale pinned token loses the CdcStateStore CAS (0 rows
	// → CdcLeadershipSupersededException) instead of a null token silently bypassing the guard and landing.
	private long? _pinnedFencingToken;

	private readonly ConcurrentDictionary<string, CdcPosition> _tracking = new(StringComparer.Ordinal);

	private readonly SortedSet<(byte[] Lsn, string TableName)> _minHeap = new(new MinHeapComparer());

	private readonly Lock _minHeapLock = new();

	internal CdcCheckpointManager(
		IDatabaseOptions dbConfig,
		ICdcRepository cdcRepository,
		ISqlServerCdcStateStore stateStore,
		ILogger logger)
	{
		_dbConfig = dbConfig;
		_cdcRepository = cdcRepository;
		_stateStore = stateStore;
		_logger = logger;
	}

	/// <summary>
	/// Pins the leadership tenure's fencing token for the batch about to be processed. Called once at the
	/// batch-start leadership gate (where <c>CurrentLeadership</c> is proven non-null when fencing is
	/// configured), so every checkpoint write in the batch presents the SAME value. A mid-batch demotion does
	/// not mutate the pinned token, so a superseded leader's write correctly loses the state-store CAS.
	/// </summary>
	/// <param name="fencingToken">
	/// The pinned token, or <see langword="null"/> when fencing is not configured / the provider issues no token
	/// (the only legitimate unfenced path).
	/// </param>
	internal void SetBatchFencingToken(long? fencingToken) => _pinnedFencingToken = fencingToken;

	/// <summary>
	/// Gets the number of tracked tables.
	/// </summary>
	internal int TrackingCount
	{
		get
		{
			lock (_minHeapLock)
			{
				return _minHeap.Count;
			}
		}
	}

	/// <summary>
	/// Gets the tracked table names.
	/// </summary>
	internal IEnumerable<string> TrackedTables => _tracking.Keys;

	/// <summary>
	/// Gets the tracking position for a specific table.
	/// </summary>
	internal CdcPosition? GetTracking(string tableName)
	{
		return _tracking.TryGetValue(tableName, out var position) ? position : null;
	}

	/// <summary>
	/// Gets the next (lowest) LSN from the tracking heap.
	/// </summary>
	internal byte[]? GetNextLsn()
	{
		lock (_minHeapLock)
		{
			return _minHeap.Count == 0 ? null : _minHeap.Min.Lsn;
		}
	}

	/// <summary>
	/// Initializes tracking positions from the state store.
	/// </summary>
	internal async Task InitializeTrackingAsync(CancellationToken cancellationToken)
	{
		// Discard the retained in-memory position BEFORE installing the durable one, and do it here rather
		// than at the caller's invocation boundary.
		//
		// The two structures are process-scoped while this method — whose entire job is to install the
		// durable truth — is batch-scoped. UpdateLsnTracking advances only on a STRICTLY GREATER position,
		// so a retained position at-or-ahead of the durable checkpoint silently discards the durable value
		// and the batch resumes from memory. The exposure is the gap between ENQUEUE and DELIVER: the
		// producer hands a table's position to UpdateLsnAfterProcessing as soon as it has enqueued that
		// table's changes, while the durable checkpoint advances only once the consumer has delivered
		// them. That call is made for every table on every poll, but what it does is NOT uniform -- for
		// a table whose next transaction is still inside the captured window it ADVANCES the retained
		// position, and for one that has moved past the window it DROPS the table from tracking
		// instead. A dropped table retains nothing and is not at risk; the risk is carried by the
		// tables that remain tracked. For those, it does not depend on the bounded queue filling or on
		// the producer ever blocking -- a full queue widens the enqueue-to-deliver gap, it does not
		// create it. The changes still sitting in the discarded queue are then never delivered to
		// anyone, and the next poll burns the loss in permanently.
		//
		// Resetting HERE rather than at the boundary is what makes the carryover inexpressible: the durable
		// read and the discard of what would defeat it become one operation that cannot be half-performed.
		// A reset placed at the caller adds a new obligation to remember, and this defect exists precisely
		// because that obligation was implicit.
		//
		// BOTH structures, under the lock. Clear() alone is not sufficient and is not used: it empties
		// _tracking only and takes no lock, which would leave a surviving _minHeap entry whose Min happens
		// to give a correct answer — correct by coincidence is not a fix for silent data loss.
		lock (_minHeapLock)
		{
			_tracking.Clear();
			_minHeap.Clear();
		}

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

	/// <summary>
	/// Updates the last processed position in the state store.
	/// </summary>
	internal async Task UpdateTableLastProcessedAsync(
		string tableName,
		byte[] lsn,
		byte[]? sequenceValue,
		DateTime? commitTime,
		CancellationToken cancellationToken)
	{
		var fencingToken = _pinnedFencingToken;

		var rowsWritten = await _stateStore.UpdateLastProcessedPositionAsync(
			_dbConfig.DatabaseConnectionIdentifier,
			_dbConfig.DatabaseName,
			tableName,
			lsn,
			sequenceValue,
			commitTime,
			fencingToken,
			cancellationToken).ConfigureAwait(false);

		// With fencing active, 0 rows written means the non-decreasing CAS rejected this write: a newer leader
		// holds a higher token. The stored LSN was left unchanged (no regression/skip); this instance is a
		// demoted split-brain leader and must stop advancing the change feed.
		if (fencingToken.HasValue && rowsWritten == 0)
		{
			throw new CdcLeadershipSupersededException();
		}

		LogUpdatedState(tableName);
	}

	/// <summary>
	/// Updates LSN tracking after processing a table, retaining the table for this run when its next
	/// transaction still falls inside the captured window.
	/// </summary>
	/// <remarks>
	/// The window is INCLUSIVE of <paramref name="maxLsn"/>: the producer loop admits every position
	/// satisfying <c>current &lt;= max</c>, so a table whose next transaction sits exactly AT the captured
	/// maximum still has work to do and must stay tracked. Comparing with <c>&lt; 0</c> here dropped that
	/// table instead, and because the durable checkpoint only advances for positions actually processed,
	/// the run ended with the final transaction neither delivered nor checkpointed — and every later run
	/// resumed from the same durable position, re-derived the same next LSN, and dropped it again. The
	/// last transaction on a quiet table was therefore starved indefinitely, until unrelated database
	/// activity raised the captured maximum above it.
	/// <para>
	/// Retaining on equality terminates: <paramref name="nextLsn"/> is the next ACTUAL transaction for the
	/// table and is strictly increasing, so the following iteration yields a position above the captured
	/// maximum and drops the table then. The table is picked up again on the next poll, which captures a
	/// fresh maximum.
	/// </para>
	/// </remarks>
	internal void UpdateLsnAfterProcessing(string tableName, byte[]? nextLsn, byte[] maxLsn)
	{
		if (nextLsn != null && nextLsn.CompareLsn(maxLsn) <= 0)
		{
			UpdateLsnTracking(tableName, nextLsn, seqVal: null);
		}
		else
		{
			UpdateLsnTracking(tableName, lsn: null, seqVal: null);
		}
	}

	int ICdcFetchProgress.TrackingCount => TrackingCount;

	IEnumerable<string> ICdcFetchProgress.TrackedTables => TrackedTables;

	CdcPosition? ICdcFetchProgress.GetTracking(string tableName) => GetTracking(tableName);

	byte[]? ICdcFetchProgress.GetNextLsn() => GetNextLsn();

	void ICdcFetchProgress.UpdateLsnTracking(string tableName, byte[]? lsn, byte[]? seqVal) =>
		UpdateLsnTracking(tableName, lsn, seqVal);

	void ICdcFetchProgress.UpdateLsnAfterProcessing(string tableName, byte[]? nextLsn, byte[] maxLsn) =>
		UpdateLsnAfterProcessing(tableName, nextLsn, maxLsn);

	/// <summary>
	/// Clears all tracking data.
	/// </summary>
	internal void Clear()
	{
		_tracking.Clear();
	}

	internal void UpdateLsnTracking(string tableName, byte[]? lsn, byte[]? seqVal)
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
					LogUpdatedLsn(tableName, CdcChangeDetector.ByteArrayToHex(lsn));
				}
			}
			else
			{
				_tracking[tableName] = new CdcPosition(lsn, seqVal);
				_ = _minHeap.Add((lsn, tableName));
				LogInsertedLsn(tableName, CdcChangeDetector.ByteArrayToHex(lsn));
			}
		}
	}

	private static bool IsEmptyLsn(IEnumerable<byte> lsn) => lsn.All(static b => b == 0);

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.CdcCheckpointStateUpdated, LogLevel.Information,
		"Updated state for {TableName}")]
	private partial void LogUpdatedState(string tableName);

	[LoggerMessage(DataSqlServerEventId.CdcCheckpointLsnRemoved, LogLevel.Debug,
		"Removed LSN for table {TableName}")]
	private partial void LogRemovedLsn(string tableName);

	[LoggerMessage(DataSqlServerEventId.CdcCheckpointLsnUpdated, LogLevel.Debug,
		"Updated LSN for table {TableName}: {Lsn}")]
	private partial void LogUpdatedLsn(string tableName, string lsn);

	[LoggerMessage(DataSqlServerEventId.CdcCheckpointLsnInserted, LogLevel.Debug,
		"Inserted new LSN for table {TableName}: {Lsn}")]
	private partial void LogInsertedLsn(string tableName, string lsn);
}
