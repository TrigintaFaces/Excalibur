// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;

namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Postgres CDC processor using logical replication with pgoutput protocol.
/// </summary>
public sealed partial class PostgresCdcProcessor : IPostgresCdcProcessor
{
	private readonly PostgresCdcOptions _options;
	private readonly IPostgresCdcStateStore _stateStore;
	private readonly ILogger<PostgresCdcProcessor> _logger;

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
	public PostgresCdcProcessor(
		IOptions<PostgresCdcOptions> options,
		IPostgresCdcStateStore stateStore,
		ILogger<PostgresCdcProcessor> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(stateStore);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();

		_stateStore = stateStore;
		_logger = logger;
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

		LogStarting(_options.ReplicationSlotName, _options.PublicationName);

		// Load last confirmed position
		_confirmedPosition = await _stateStore
			.GetLastPositionAsync(_options.ProcessorId, _options.ReplicationSlotName, cancellationToken)
			.ConfigureAwait(false);

		_currentPosition = _confirmedPosition;

		LogResuming(_confirmedPosition.LsnString);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
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
				LogError(ex);

				// Dispose connection to force reconnect
				if (_replicationConnection is not null)
				{
					await _replicationConnection.DisposeAsync().ConfigureAwait(false);
					_replicationConnection = null;
				}

				// Wait before reconnecting
				await Task.Delay(_options.PollingInterval, cancellationToken).ConfigureAwait(false);
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

		// Load last confirmed position
		_confirmedPosition = await _stateStore
			.GetLastPositionAsync(_options.ProcessorId, _options.ReplicationSlotName, cancellationToken)
			.ConfigureAwait(false);

		_currentPosition = _confirmedPosition;

		await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

		var count = 0;
		var slot = new PgOutputReplicationSlot(_options.ReplicationSlotName);

		var replicationOptions = new PgOutputReplicationOptions(
			_options.PublicationName,
			protocolVersion: 1,
			binary: _options.UseBinaryProtocol);

		await foreach (var message in _replicationConnection
						   .StartReplication(slot, replicationOptions, cancellationToken)
						   .ConfigureAwait(false))
		{
			var changeEvent = await ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);

			if (changeEvent is not null)
			{
				await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);
				count++;
			}

			// Update position
			_replicationConnection.SetReplicationStatus(message.WalEnd);
			_currentPosition = new PostgresCdcPosition(message.WalEnd);

			if (count >= _options.BatchSize)
			{
				break;
			}
		}

		// Save position if we processed anything
		if (count > 0)
		{
			await _stateStore
				.SavePositionAsync(_options.ProcessorId, _options.ReplicationSlotName, _currentPosition, cancellationToken)
				.ConfigureAwait(false);

			_confirmedPosition = _currentPosition;
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
			_replicationConnection.SetReplicationStatus(position.Lsn);
		}

		LogConfirmed(position.LsnString);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		// LogicalReplicationConnection doesn't have sync Dispose, just mark as disposed
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
				IsPrimaryKey = column.Flags.HasFlag(RelationMessage.Column.ColumnFlags.PartOfKey),
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
		await _replicationConnection.Open(cancellationToken).ConfigureAwait(false);

		// Check if slot exists, create if needed
		if (_options.AutoCreateSlot)
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
			_ = await _replicationConnection
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
		var slot = new PgOutputReplicationSlot(_options.ReplicationSlotName);

		var replicationOptions = new PgOutputReplicationOptions(
			_options.PublicationName,
			protocolVersion: 1,
			binary: _options.UseBinaryProtocol);

		// Start from confirmed position or beginning
		var startLsn = _confirmedPosition.IsValid ? _confirmedPosition.Lsn : default;

		await foreach (var message in _replicationConnection
						   .StartReplication(slot, replicationOptions, cancellationToken, startLsn)
						   .ConfigureAwait(false))
		{
			var changeEvent = await ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);

			if (changeEvent is not null)
			{
				// Apply table filter if configured
				if (ShouldProcessTable(changeEvent.FullTableName))
				{
					await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);

					LogProcessed(changeEvent.ChangeType, changeEvent.FullTableName, changeEvent.Position.LsnString);
				}
			}

			// Update position and acknowledge
			_replicationConnection.SetReplicationStatus(message.WalEnd);
			_currentPosition = new PostgresCdcPosition(message.WalEnd);
		}
	}

	private async Task<PostgresDataChangeEvent?> ProcessMessageAsync(
		PgOutputReplicationMessage message,
		CancellationToken cancellationToken)
	{
		return message switch
		{
			BeginMessage begin => HandleBegin(begin),
			CommitMessage commit => HandleCommit(commit),
			InsertMessage insert => await HandleInsertAsync(insert, cancellationToken).ConfigureAwait(false),
			FullUpdateMessage fullUpdate => await HandleFullUpdateAsync(fullUpdate, cancellationToken).ConfigureAwait(false),
			IndexUpdateMessage indexUpdate => await HandleIndexUpdateAsync(indexUpdate, cancellationToken).ConfigureAwait(false),
			UpdateMessage update => await HandleDefaultUpdateAsync(update, cancellationToken).ConfigureAwait(false),
			FullDeleteMessage fullDelete => await HandleFullDeleteAsync(fullDelete, cancellationToken).ConfigureAwait(false),
			KeyDeleteMessage keyDelete => await HandleKeyDeleteAsync(keyDelete, cancellationToken).ConfigureAwait(false),
			DeleteMessage delete => await HandleDefaultDeleteAsync(delete, cancellationToken).ConfigureAwait(false),
			TruncateMessage truncate => HandleTruncate(truncate),
			_ => null, // Ignore relation, type, origin messages
		};
	}

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

	private PostgresDataChangeEvent HandleTruncate(TruncateMessage truncate)
	{
		// Truncate affects all tables in the list
		var firstRelation = truncate.Relations[0];

		return PostgresDataChangeEvent.CreateTruncate(
			new PostgresCdcPosition(truncate.WalEnd),
			firstRelation.Namespace,
			firstRelation.RelationName,
			_currentTransactionId,
			_currentCommitTime);
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

	[LoggerMessage(DataPostgresEventId.CdcProcessorStarting, LogLevel.Information,
		"Starting Postgres CDC processor for slot '{SlotName}' with publication '{Publication}'")]
	private partial void LogStarting(string slotName, string publication);

	[LoggerMessage(DataPostgresEventId.CdcResumingFromPosition, LogLevel.Information, "Resuming from LSN position {Position}")]
	private partial void LogResuming(string position);

	[LoggerMessage(DataPostgresEventId.CdcConnectedToReplicationStream, LogLevel.Information, "Connected to Postgres replication stream")]
	private partial void LogConnected();

	[LoggerMessage(DataPostgresEventId.CdcCreatedReplicationSlot, LogLevel.Information, "Created replication slot '{SlotName}'")]
	private partial void LogSlotCreated(string slotName);

	[LoggerMessage(DataPostgresEventId.CdcReplicationSlotExists, LogLevel.Debug, "Replication slot '{SlotName}' already exists")]
	private partial void LogSlotExists(string slotName);

	[LoggerMessage(DataPostgresEventId.CdcProcessedChange, LogLevel.Debug, "Processed {ChangeType} on {TableName} at LSN {Position}")]
	private partial void LogProcessed(PostgresDataChangeType changeType, string tableName, string position);

	[LoggerMessage(DataPostgresEventId.CdcConfirmedPosition, LogLevel.Debug, "Confirmed position {Position}")]
	private partial void LogConfirmed(string position);

	[LoggerMessage(DataPostgresEventId.CdcProcessorStopping, LogLevel.Information, "Stopping Postgres CDC processor")]
	private partial void LogStopping();

	[LoggerMessage(DataPostgresEventId.CdcProcessingError, LogLevel.Error, "Error in Postgres CDC processor")]
	private partial void LogError(Exception ex);
}
