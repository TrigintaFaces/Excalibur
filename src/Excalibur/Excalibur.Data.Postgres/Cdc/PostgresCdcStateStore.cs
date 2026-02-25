// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Postgres implementation of <see cref="IPostgresCdcStateStore"/> using a state table.
/// </summary>
public sealed partial class PostgresCdcStateStore : IPostgresCdcStateStore
{
	private const string DefaultTableName = "cdc_state";
	private const string DefaultSchemaName = "excalibur";

	private readonly string _connectionString;
	private readonly string _schemaName;
	private readonly string _tableName;
	private readonly string _fullTableName;
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcStateStore"/> class with options.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="options">The CDC state store options.</param>
	public PostgresCdcStateStore(
		string connectionString,
		IOptions<PostgresCdcStateStoreOptions> options)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(options);

		var resolvedOptions = options.Value;
		resolvedOptions.Validate();

		_connectionString = connectionString;
		_schemaName = resolvedOptions.SchemaName;
		_tableName = resolvedOptions.TableName;
		_fullTableName = resolvedOptions.QualifiedTableName;
	}

	/// <inheritdoc/>
	public async Task<PostgresCdcPosition> GetLastPositionAsync(
		string processorId,
		string slotName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		ArgumentException.ThrowIfNullOrWhiteSpace(slotName);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT position
			FROM {_fullTableName}
			WHERE processor_id = @ProcessorId
			  AND slot_name = @SlotName
			  AND table_name IS NULL
			ORDER BY updated_at DESC
			LIMIT 1";

		var position = await connection
			.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
				sql,
				new { ProcessorId = processorId, SlotName = slotName },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return PostgresCdcPosition.TryParse(position, out var result)
			? result
			: PostgresCdcPosition.Start;
	}

	/// <inheritdoc/>
	public async Task SavePositionAsync(
		string processorId,
		string slotName,
		PostgresCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		ArgumentException.ThrowIfNullOrWhiteSpace(slotName);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_fullTableName} (processor_id, slot_name, table_name, position, updated_at)
			VALUES (@ProcessorId, @SlotName, NULL, @Position, @UpdatedAt)
			ON CONFLICT (processor_id, slot_name, COALESCE(table_name, ''))
			DO UPDATE SET position = @Position, updated_at = @UpdatedAt";

		_ = await connection
			.ExecuteAsync(new CommandDefinition(
				sql,
				new
				{
					ProcessorId = processorId,
					SlotName = slotName,
					Position = position.LsnString,
					UpdatedAt = DateTimeOffset.UtcNow,
				},
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<PostgresCdcStateEntry>> GetAllStatesAsync(
		string processorId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT processor_id AS ProcessorId,
			       slot_name AS SlotName,
			       table_name AS TableName,
			       position AS Position,
			       last_event_time AS LastEventTime,
			       updated_at AS UpdatedAt,
			       event_count AS EventCount
			FROM {_fullTableName}
			WHERE processor_id = @ProcessorId
			ORDER BY table_name";

		var results = await connection
			.QueryAsync<PostgresCdcStateEntry>(new CommandDefinition(
				sql,
				new { ProcessorId = processorId },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return results.ToList();
	}

	/// <inheritdoc/>
	public async Task SaveStateAsync(
		PostgresCdcStateEntry entry,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(entry);
		ArgumentException.ThrowIfNullOrWhiteSpace(entry.ProcessorId);
		ArgumentException.ThrowIfNullOrWhiteSpace(entry.SlotName);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_fullTableName}
			       (processor_id, slot_name, table_name, position, last_event_time, updated_at, event_count)
			VALUES (@ProcessorId, @SlotName, @TableName, @Position, @LastEventTime, @UpdatedAt, @EventCount)
			ON CONFLICT (processor_id, slot_name, COALESCE(table_name, ''))
			DO UPDATE SET position = @Position,
			              last_event_time = @LastEventTime,
			              updated_at = @UpdatedAt,
			              event_count = {_fullTableName}.event_count + @EventCount";

		_ = await connection
			.ExecuteAsync(new CommandDefinition(
				sql,
				new
				{
					entry.ProcessorId,
					entry.SlotName,
					entry.TableName,
					entry.Position,
					entry.LastEventTime,
					UpdatedAt = DateTimeOffset.UtcNow,
					entry.EventCount,
				},
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task ClearStateAsync(
		string processorId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $"DELETE FROM {_fullTableName} WHERE processor_id = @ProcessorId";

		_ = await connection
			.ExecuteAsync(new CommandDefinition(
				sql,
				new { ProcessorId = processorId },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT position
			FROM {_fullTableName}
			WHERE processor_id = @ProcessorId
			  AND table_name IS NULL
			ORDER BY updated_at DESC
			LIMIT 1";

		var position = await connection
			.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
				sql,
				new { ProcessorId = consumerId },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		if (PostgresCdcPosition.TryParse(position, out var result) && result.IsValid)
		{
			return result.ToChangePosition();
		}

		return null;
	}

	/// <inheritdoc/>
	async Task ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);
		var pgPosition = PostgresCdcPosition.FromChangePosition(position);

		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_fullTableName} (processor_id, slot_name, table_name, position, updated_at)
			VALUES (@ProcessorId, @SlotName, NULL, @Position, @UpdatedAt)
			ON CONFLICT (processor_id, slot_name, COALESCE(table_name, ''))
			DO UPDATE SET position = @Position, updated_at = @UpdatedAt";

		_ = await connection
			.ExecuteAsync(new CommandDefinition(
				sql,
				new
				{
					ProcessorId = consumerId,
					SlotName = "default",
					Position = pgPosition.LsnString,
					UpdatedAt = DateTimeOffset.UtcNow,
				},
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	async Task<bool> ICdcStateStore.DeletePositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		await ClearStateAsync(consumerId, cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc/>
	async IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> ICdcStateStore.GetAllPositionsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT DISTINCT processor_id AS ProcessorId, position AS Position
			FROM {_fullTableName}
			WHERE table_name IS NULL
			ORDER BY processor_id";

		var rows = await connection
			.QueryAsync<(string ProcessorId, string Position)>(new CommandDefinition(
				sql,
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		foreach (var row in rows)
		{
			if (PostgresCdcPosition.TryParse(row.Position, out var result) && result.IsValid)
			{
				yield return (row.ProcessorId, result.ToChangePosition());
			}
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		return ValueTask.CompletedTask;
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Create schema if not exists
		var createSchemaSql = $"CREATE SCHEMA IF NOT EXISTS \"{_schemaName}\"";
		_ = await connection
			.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		// Create state table
		var createTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_fullTableName} (
				processor_id VARCHAR(255) NOT NULL,
				slot_name VARCHAR(255) NOT NULL,
				table_name VARCHAR(255),
				position VARCHAR(32) NOT NULL,
				last_event_time TIMESTAMPTZ,
				updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
				event_count BIGINT NOT NULL DEFAULT 0,
				CONSTRAINT pk_{_tableName} PRIMARY KEY (processor_id, slot_name, COALESCE(table_name, ''))
			)";

		_ = await connection
			.ExecuteAsync(new CommandDefinition(createTableSql, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		// Create index on updated_at
		var createIndexSql = $@"
			CREATE INDEX IF NOT EXISTS ix_{_tableName}_updated_at
			ON {_fullTableName} (processor_id, updated_at DESC)";

		_ = await connection
			.ExecuteAsync(new CommandDefinition(createIndexSql, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		_initialized = true;
	}

	private static void ValidateSqlIdentifier(string identifier, string parameterName)
	{
		if (!SqlIdentifierRegex().IsMatch(identifier))
		{
			throw new ArgumentException(
				$"SQL identifier '{parameterName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.",
				parameterName);
		}
	}

	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex SqlIdentifierRegex();
}
