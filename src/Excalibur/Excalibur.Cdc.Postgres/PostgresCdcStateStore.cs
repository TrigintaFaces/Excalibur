// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Dapper;

using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Postgres implementation of <see cref="IPostgresCdcStateStore"/> using a state table.
/// </summary>
/// <remarks>
/// <para>
/// <b>This store is global infrastructure, not tenant-partitioned, and that is deliberate.</b> A row here
/// records how far a replication slot has been consumed. A replication slot and its LSN are properties of
/// the <em>database</em>, not of any tenant, so partitioning a checkpoint per tenant would not isolate
/// anything - it would break the mechanism, because a slot advanced by one partition is advanced for every
/// other reader of that slot. The consequences of that election are named here rather than left implicit:
/// </para>
/// <list type="number">
/// <item><description>
/// <see cref="ICdcStateStore.GetAllPositionsAsync"/> enumerates <em>every</em> consumer's checkpoint - that
/// is the method's documented purpose (operational visibility into consumer progress), so it is scoped to
/// the store, not to the caller. Any component holding this store therefore observes the existence,
/// identity and replication progress of every other consumer sharing the table. In a shared deployment,
/// hand this store only to components entitled to that view, or give each tenant its own state table via
/// <see cref="PostgresCdcStateStoreOptions.TableName"/>.
/// </description></item>
/// <item><description>
/// A consumer's identity on the <see cref="ICdcStateStore"/> path is its <c>consumerId</c> alone. Two
/// consumers configured with the same id share one checkpoint and each will advance past changes the other
/// has not read. Distinct consumers require distinct ids; the store cannot detect a collision because both
/// callers are, by construction, indistinguishable to it.
/// </description></item>
/// <item><description>
/// <see cref="ClearStateAsync"/> removes every row for a processor - all slots and all per-table state -
/// which is its documented contract ("clears all state for a processor"), not an oversight. It is not a
/// per-slot reset; to reset one generic checkpoint use <see cref="ICdcStateStore.DeletePositionAsync"/>.
/// </description></item>
/// </list>
/// <para>
/// The generic <see cref="ICdcStateStore"/> checkpoint and the typed per-slot positions share one table and
/// are separated by a reserved discriminator, described on the discriminator constant itself.
/// </para>
/// </remarks>
public sealed partial class PostgresCdcStateStore : IPostgresCdcStateStore
{
	/// <summary>
	/// The <c>slot_name</c> value reserved for the provider-neutral <see cref="ICdcStateStore"/> checkpoint,
	/// mirroring the sentinel-row pattern the SQL Server store uses for its own discriminator columns.
	/// </summary>
	/// <remarks>
	/// The empty string is unforgeable through the typed API: every typed entry point rejects a null, empty
	/// or whitespace <c>slotName</c>, so a caller cannot create a row that aliases the generic checkpoint.
	/// That is why the separation is a property of the key rather than of a naming convention. A named slot
	/// could not serve here - any value legal as a slot name is also a value a typed caller may legitimately
	/// pass, which is precisely how the generic checkpoint previously collided with real slots.
	/// </remarks>
	private const string GenericSlotDiscriminator = "";

	/// <summary>
	/// The <c>slot_name</c> the generic checkpoint used before it moved to the reserved discriminator. Read
	/// and deleted alongside the reserved row so an existing deployment keeps its checkpoint across the
	/// upgrade instead of silently rewinding to the beginning of the stream and reprocessing.
	/// </summary>
	private const string LegacyGenericSlotName = "default";

	private readonly string _connectionString;
	private readonly string _schemaName;
	private readonly string _tableName;
	private readonly string _fullTableName;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcStateStore"/> class with options.
	/// </summary>
	public PostgresCdcStateStore(string connectionString, IOptions<PostgresCdcStateStoreOptions> options)
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
	public async Task<PostgresCdcPosition> GetLastPositionAsync(string processorId, string slotName, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		ArgumentException.ThrowIfNullOrWhiteSpace(slotName);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT position FROM {_fullTableName}
			WHERE processor_id = @ProcessorId AND slot_name = @SlotName AND table_name = ''
			ORDER BY updated_at DESC LIMIT 1";

		var position = await connection
			.QuerySingleOrDefaultAsync<string>(new CommandDefinition(sql,
				new { ProcessorId = processorId, SlotName = slotName },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return PostgresCdcPosition.TryParse(position, out var result) ? result : PostgresCdcPosition.Start;
	}

	/// <inheritdoc/>
	public async Task SavePositionAsync(string processorId, string slotName, PostgresCdcPosition position, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		ArgumentException.ThrowIfNullOrWhiteSpace(slotName);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_fullTableName} (processor_id, slot_name, table_name, position, updated_at)
			VALUES (@ProcessorId, @SlotName, '', @Position, @UpdatedAt)
			ON CONFLICT (processor_id, slot_name, table_name)
			DO UPDATE SET position = @Position, updated_at = @UpdatedAt";

		_ = await connection
			.ExecuteAsync(new CommandDefinition(sql,
				new { ProcessorId = processorId, SlotName = slotName, Position = position.LsnString, UpdatedAt = DateTimeOffset.UtcNow },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<PostgresCdcStateEntry>> GetAllStatesAsync(string processorId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT processor_id AS ProcessorId, slot_name AS SlotName, table_name AS TableName,
			       position AS Position, last_event_time AS LastEventTime, updated_at AS UpdatedAt, event_count AS EventCount
			FROM {_fullTableName} WHERE processor_id = @ProcessorId ORDER BY table_name";

		var results = await connection
			.QueryAsync<PostgresCdcStateEntry>(new CommandDefinition(sql,
				new { ProcessorId = processorId }, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return results.ToList();
	}

	/// <inheritdoc/>
	public async Task SaveStateAsync(PostgresCdcStateEntry entry, CancellationToken cancellationToken)
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
			VALUES (@ProcessorId, @SlotName, COALESCE(@TableName, ''), @Position, @LastEventTime, @UpdatedAt, @EventCount)
			ON CONFLICT (processor_id, slot_name, table_name)
			DO UPDATE SET position = @Position, last_event_time = @LastEventTime,
			              updated_at = @UpdatedAt, event_count = {_fullTableName}.event_count + @EventCount";

		_ = await connection
			.ExecuteAsync(new CommandDefinition(sql,
				new
				{
					entry.ProcessorId,
					entry.SlotName,
					entry.TableName,
					entry.Position,
					entry.LastEventTime,
					UpdatedAt = DateTimeOffset.UtcNow,
					entry.EventCount
				},
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task ClearStateAsync(string processorId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $"DELETE FROM {_fullTableName} WHERE processor_id = @ProcessorId";
		_ = await connection
			.ExecuteAsync(new CommandDefinition(sql, new { ProcessorId = processorId }, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// slot_name is part of the predicate, not decoration. Without it this read matches every row the
		// processor owns with an empty table_name -- including the TYPED per-slot checkpoints -- and
		// ORDER BY updated_at DESC then returns whichever slot advanced most recently. The generic consumer
		// would resume from a position it never reached and skip every change in between: silent loss, in
		// the same losing direction as a suppressed duplicate. Ordering by (slot_name <> reserved) prefers
		// the reserved row and falls back to the legacy one only when no reserved row exists yet.
		var sql = $@"
			SELECT position FROM {_fullTableName}
			WHERE processor_id = @ProcessorId AND table_name = ''
			      AND slot_name IN (@GenericSlot, @LegacySlot)
			ORDER BY (slot_name <> @GenericSlot), updated_at DESC LIMIT 1";

		var position = await connection
			.QuerySingleOrDefaultAsync<string>(new CommandDefinition(sql,
				new { ProcessorId = consumerId, GenericSlot = GenericSlotDiscriminator, LegacySlot = LegacyGenericSlotName },
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
			VALUES (@ProcessorId, @SlotName, '', @Position, @UpdatedAt)
			ON CONFLICT (processor_id, slot_name, table_name)
			DO UPDATE SET position = @Position, updated_at = @UpdatedAt";

		_ = await connection
			.ExecuteAsync(new CommandDefinition(sql,
				new { ProcessorId = consumerId, SlotName = GenericSlotDiscriminator, Position = pgPosition.LsnString, UpdatedAt = DateTimeOffset.UtcNow },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	async Task<bool> ICdcStateStore.DeletePositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Scope to the generic checkpoint rows and report whether one actually existed, per the
		// ICdcStateStore contract: deleting a non-existent checkpoint returns false. The slot_name term is
		// what keeps this from being a wildcard -- without it the delete also destroys every TYPED per-slot
		// checkpoint the processor owns, resetting replication progress the caller never asked to touch.
		var sql = $@"
			DELETE FROM {_fullTableName}
			WHERE processor_id = @ProcessorId AND table_name = ''
			      AND slot_name IN (@GenericSlot, @LegacySlot)";
		var affected = await connection
			.ExecuteAsync(new CommandDefinition(sql,
				new { ProcessorId = consumerId, GenericSlot = GenericSlotDiscriminator, LegacySlot = LegacyGenericSlotName },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);
		return affected > 0;
	}

	/// <inheritdoc/>
	async IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> ICdcStateStore.GetAllPositionsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// The contract yields one (ConsumerId, Position) pair per consumer. Selecting every empty-table_name
		// row breaks that: a processor with several typed slots emits several tuples under one consumer id,
		// each claiming to be that consumer's position. DISTINCT ON collapses to a single generic checkpoint
		// per processor, preferring the reserved row over the legacy one.
		var sql = $@"
			SELECT DISTINCT ON (processor_id) processor_id AS ProcessorId, position AS Position
			FROM {_fullTableName}
			WHERE table_name = '' AND slot_name IN (@GenericSlot, @LegacySlot)
			ORDER BY processor_id, (slot_name <> @GenericSlot)";

		var rows = await connection
			.QueryAsync<(string ProcessorId, string Position)>(new CommandDefinition(sql,
				new { GenericSlot = GenericSlotDiscriminator, LegacySlot = LegacyGenericSlotName },
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
		_initLock?.Dispose();
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		_initLock?.Dispose();
		return ValueTask.CompletedTask;
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		// Serialize first-time provisioning: PostgreSQL DDL (CREATE SCHEMA/TABLE ... IF NOT EXISTS) is NOT
		// concurrency-safe — racing statements collide on internal catalog inserts (23505 on
		// pg_type_typname_nsp_index). Concurrent first callers would each run the DDL without this gate.
		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_initialized)
			{
				return;
			}

			await using var connection = new NpgsqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var createSchemaSql = $"CREATE SCHEMA IF NOT EXISTS \"{_schemaName}\"";
			_ = await connection.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: cancellationToken)).ConfigureAwait(false);

			var createTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_fullTableName} (
				processor_id VARCHAR(255) NOT NULL, slot_name VARCHAR(255) NOT NULL,
				table_name VARCHAR(255) NOT NULL DEFAULT '', position VARCHAR(32) NOT NULL,
				last_event_time TIMESTAMPTZ, updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
				event_count BIGINT NOT NULL DEFAULT 0,
				CONSTRAINT pk_{_tableName} PRIMARY KEY (processor_id, slot_name, table_name)
			)";
			_ = await connection.ExecuteAsync(new CommandDefinition(createTableSql, cancellationToken: cancellationToken)).ConfigureAwait(false);

			var createIndexSql = $@"
			CREATE INDEX IF NOT EXISTS ix_{_tableName}_updated_at
			ON {_fullTableName} (processor_id, updated_at DESC)";
			_ = await connection.ExecuteAsync(new CommandDefinition(createIndexSql, cancellationToken: cancellationToken)).ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}
}
