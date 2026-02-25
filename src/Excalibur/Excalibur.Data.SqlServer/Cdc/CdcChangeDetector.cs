// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;
using System.Threading.Channels;

using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Responsible for detecting CDC changes by reading from the database
/// and enqueuing them into the processing channel (producer role).
/// </summary>
/// <remarks>
/// Extracted from <see cref="CdcProcessor"/> to separate the change detection
/// concern from change application and checkpoint management.
/// </remarks>
internal sealed partial class CdcChangeDetector
{
	private readonly ICdcRepository _cdcRepository;
	private readonly ICdcRepositoryLsnMapping _cdcLsnMapping;
	private readonly IDatabaseConfig _dbConfig;
	private readonly IDataAccessPolicyFactory _policyFactory;
	private readonly CdcCheckpointManager _checkpointManager;
	private readonly ILogger _logger;

	internal CdcChangeDetector(
		ICdcRepository cdcRepository,
		ICdcRepositoryLsnMapping cdcLsnMapping,
		IDatabaseConfig dbConfig,
		IDataAccessPolicyFactory policyFactory,
		CdcCheckpointManager checkpointManager,
		ILogger logger)
	{
		_cdcRepository = cdcRepository;
		_cdcLsnMapping = cdcLsnMapping;
		_dbConfig = dbConfig;
		_policyFactory = policyFactory;
		_checkpointManager = checkpointManager;
		_logger = logger;
	}

	/// <summary>
	/// Runs the producer loop, fetching CDC changes from the database and writing them to the channel.
	/// </summary>
	internal async Task ProducerLoopAsync(
		byte[]? lowestStartLsn,
		ChannelWriter<DataChangeEvent> writer,
		int queueSize,
		CancellationToken cancellationToken)
	{
		try
		{
			var currentGlobalLsn = lowestStartLsn;
			var maxLsn = await _cdcRepository.GetMaxPositionAsync(cancellationToken).ConfigureAwait(false);

			LogProducerLoopStarted(_checkpointManager.TrackingCount);

			while (currentGlobalLsn != null && currentGlobalLsn.CompareLsn(maxLsn) <= 0)
			{
				cancellationToken.ThrowIfCancellationRequested();

				foreach (var tableName in _checkpointManager.TrackedTables)
				{
					var tableTracking = _checkpointManager.GetTracking(tableName);
					if (tableTracking is not null &&
						tableTracking.Lsn.CompareLsn(currentGlobalLsn) == 0)
					{
						await EnqueueTableChangesAsync(tableName, tableTracking.Lsn, tableTracking.SequenceValue, maxLsn, writer, queueSize, cancellationToken)
							.ConfigureAwait(false);
					}
				}

				currentGlobalLsn = _checkpointManager.GetNextLsn();
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
			writer.Complete();
			LogProducerCompleted();
		}
	}

	private async Task EnqueueTableChangesAsync(
		string tableName,
		byte[] lastLsn,
		byte[]? lastSequenceValue,
		byte[] maxLsn,
		ChannelWriter<DataChangeEvent> writer,
		int queueSize,
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

			var producerBatchSize = Math.Min(queueSize, _dbConfig.ProducerBatchSize);
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
			await ProcessAndEnqueueChangesAsync(changes, changeProcessingState, writer, combinedToken).ConfigureAwait(false);
		}

		ValidateUnmatchedUpdates(changeProcessingState);

		var nextLsn = await _cdcLsnMapping.GetNextLsnAsync(tableName, changeProcessingState.Lsn, combinedToken).ConfigureAwait(false);

		LogTableEnqueued(tableName, changeProcessingState.Lsn, nextLsn, maxLsn);
		_checkpointManager.UpdateLsnAfterProcessing(tableName, nextLsn, maxLsn);
	}

	private async Task ProcessAndEnqueueChangesAsync(
		IList<CdcRow> changes,
		ChangeProcessingState state,
		ChannelWriter<DataChangeEvent> writer,
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
			foreach (var evt in events)
			{
				await writer.WriteAsync(evt, cancellationToken).ConfigureAwait(false);
			}
		}

		events.Clear();
		changes.Clear();
	}

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

	private static void MatchPendingUpdates(List<DataChangeEvent> events, ChangeProcessingState state)
	{
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
			await _checkpointManager.UpdateTableLastProcessedAsync(state.TableName, state.Lsn, state.SequenceValue, commitTime, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	private void ValidateUnmatchedUpdates(ChangeProcessingState state)
	{
		if (state.PendingUpdateBefore.Count > 0 || state.PendingUpdateAfter.Count > 0)
		{
			LogUnmatchedUpdates(state);
			throw new UnmatchedUpdateRecordsException(state.Lsn);
		}
	}

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

	private void LogFetchingChanges(ChangeProcessingState state) =>
		LogFetchingCdcChanges(
			state.TableName,
			ByteArrayToHex(state.Lsn),
			state.SequenceValue != null ? ByteArrayToHex(state.SequenceValue) : "null");

	private void LogUnknownOperation(CdcRow record) =>
		LogUnknownOperationCode(record.OperationCode, record.Lsn, record.SeqVal);

	private void LogTableEnqueued(string tableName, byte[] currentLsn, byte[]? nextLsn, byte[] maxLsn) =>
		LogTableEnqueuedDetails(
			tableName,
			ByteArrayToHex(currentLsn),
			nextLsn != null ? ByteArrayToHex(nextLsn) : "null",
			ByteArrayToHex(maxLsn));

	internal static string ByteArrayToHex(byte[] bytes) => $"0x{Convert.ToHexString(bytes)}";

	/// <summary>
	/// Holds the state for processing CDC changes for a specific table.
	/// </summary>
	internal sealed class ChangeProcessingState
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
}
