// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Manages CDC checkpoint state and LSN tracking for table processing positions.
/// </summary>
/// <remarks>
/// Extracted from <see cref="CdcProcessor"/> to separate checkpoint management
/// from change detection and change application concerns.
/// </remarks>
internal sealed partial class CdcCheckpointManager
{
	private readonly IDatabaseConfig _dbConfig;
	private readonly ICdcRepository _cdcRepository;
	private readonly ICdcStateStore _stateStore;
	private readonly ILogger _logger;

	private readonly ConcurrentDictionary<string, CdcPosition> _tracking = new(StringComparer.Ordinal);

	private readonly SortedSet<(byte[] Lsn, string TableName)> _minHeap = new(new MinHeapComparer());

#if NET9_0_OR_GREATER
	private readonly Lock _minHeapLock = new();
#else
	private readonly object _minHeapLock = new();
#endif

	internal CdcCheckpointManager(
		IDatabaseConfig dbConfig,
		ICdcRepository cdcRepository,
		ICdcStateStore stateStore,
		ILogger logger)
	{
		_dbConfig = dbConfig;
		_cdcRepository = cdcRepository;
		_stateStore = stateStore;
		_logger = logger;
	}

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

	/// <summary>
	/// Updates LSN tracking after processing a table.
	/// </summary>
	internal void UpdateLsnAfterProcessing(string tableName, byte[]? nextLsn, byte[] maxLsn)
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
	/// Clears all tracking data.
	/// </summary>
	internal void Clear()
	{
		_tracking.Clear();
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
}
