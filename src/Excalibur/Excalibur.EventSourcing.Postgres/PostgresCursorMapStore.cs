// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Dapper;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// PostgreSQL implementation of <see cref="ICursorMapStore"/> using a key-value table.
/// </summary>
/// <remarks>
/// <para>
/// Uses a <c>projection_cursor_maps</c> table with columns
/// <c>(projection_name, stream_id, position)</c>. Saves are atomic via
/// <c>INSERT ... ON CONFLICT ... DO UPDATE</c>.
/// </para>
/// <para>
/// Table DDL:
/// <code>
/// CREATE TABLE projection_cursor_maps (
///     tenant_id VARCHAR(256) NOT NULL,
///     projection_name VARCHAR(256) NOT NULL,
///     stream_id VARCHAR(256) NOT NULL,
///     position BIGINT NOT NULL,
///     CONSTRAINT pk_projection_cursor_maps PRIMARY KEY (tenant_id, projection_name, stream_id)
/// );
/// </code>
/// </para>
/// </remarks>
public sealed class PostgresCursorMapStore : ICursorMapStore
{
	private readonly NpgsqlDataSource _dataSource;
	private readonly ILogger<PostgresCursorMapStore> _logger;
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance with a connection string.
	/// </summary>
	/// <param name="connectionString">The PostgreSQL connection string.</param>
	/// <param name="logger">The logger instance.</param>
	public PostgresCursorMapStore(string connectionString, ILogger<PostgresCursorMapStore> logger)
		: this(NpgsqlDataSource.Create(connectionString), logger, null)
	{
	}

	/// <summary>
	/// Initializes a new instance with a connection string and an ambient tenant context.
	/// </summary>
	/// <param name="connectionString">The PostgreSQL connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered.
	/// </param>
	public PostgresCursorMapStore(
		string connectionString,
		ILogger<PostgresCursorMapStore> logger,
		ITenantContext? tenantContext)
		: this(NpgsqlDataSource.Create(connectionString), logger, tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance with an <see cref="NpgsqlDataSource"/>.
	/// </summary>
	/// <param name="dataSource">The Npgsql data source for connection management.</param>
	/// <param name="logger">The logger instance.</param>
	public PostgresCursorMapStore(NpgsqlDataSource dataSource, ILogger<PostgresCursorMapStore> logger)
		: this(dataSource, logger, null)
	{
	}

	/// <summary>
	/// Initializes a new instance with an <see cref="NpgsqlDataSource"/> and an ambient tenant context.
	/// </summary>
	/// <param name="dataSource">The Npgsql data source for connection management.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered. Cursor
	/// maps are partitioned by the tenant this resolves -- never by a tenant the caller names, which is
	/// why no method takes a tenant argument. The two-argument overloads are kept so the existing public
	/// surface is unchanged: an existing caller compiles untouched and lands in the untenanted partition,
	/// exactly where its rows already were.
	/// </param>
	public PostgresCursorMapStore(
		NpgsqlDataSource dataSource,
		ILogger<PostgresCursorMapStore> logger,
		ITenantContext? tenantContext)
	{
		ArgumentNullException.ThrowIfNull(dataSource);
		ArgumentNullException.ThrowIfNull(logger);

		_dataSource = dataSource;
		_logger = logger;
		_tenantContext = tenantContext;
	}

	/// <summary>
	/// Resolves the partition every cursor map is confined to.
	/// </summary>
	/// <remarks>
	/// Keyed on projection name alone, two tenants running the same projection shared one cursor row: a
	/// position advanced by one made the other's projector skip events it never processed -- data missing
	/// from a read model permanently, with nothing to alert on.
	/// </remarks>
	/// <returns>The reserved partition key for the ambient tenant.</returns>
	private string ResolveTenantKey() =>
		KeyedTenantPartition.FromScope(TenantScope.FromContext(_tenantContext)).TenantId;

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, long>> GetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<CursorMapRow>(
			"SELECT stream_id, position FROM projection_cursor_maps "
			+ "WHERE tenant_id = @TenantId AND projection_name = @ProjectionName",
			new { TenantId = ResolveTenantKey(), ProjectionName = projectionName }).ConfigureAwait(false);

		var result = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (var row in rows)
		{
			result[row.stream_id] = row.position;
		}

		return result;
	}

	/// <inheritdoc />
	public async Task SaveCursorMapAsync(
		string projectionName,
		IReadOnlyDictionary<string, long> cursorMap,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);
		ArgumentNullException.ThrowIfNull(cursorMap);

		if (cursorMap.Count == 0)
		{
			return;
		}

		// Resolved ONCE, outside the loop: every row of one save must land in the same partition, and a
		// per-iteration resolve would let an ambient change mid-loop split the map across two tenants.
		var tenantKey = ResolveTenantKey();

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		foreach (var entry in cursorMap)
		{
			await connection.ExecuteAsync(
				"""
				INSERT INTO projection_cursor_maps (tenant_id, projection_name, stream_id, position)
				VALUES (@TenantId, @ProjectionName, @StreamId, @Position)
				ON CONFLICT (tenant_id, projection_name, stream_id)
				DO UPDATE SET position = @Position
				""",
				new
				{
					TenantId = tenantKey,
					ProjectionName = projectionName,
					StreamId = entry.Key,
					Position = entry.Value
				},
				transaction).ConfigureAwait(false);
		}

		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task ResetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		await connection.ExecuteAsync(
			// The tenant term is NOT optional here and this is the worst place to omit it: a reset without it
			// deletes every tenant's cursors for the projection, and the next run reprojects the entire
			// stream for all of them -- or, if the source has been truncated, loses their positions outright.
			"DELETE FROM projection_cursor_maps "
			+ "WHERE tenant_id = @TenantId AND projection_name = @ProjectionName",
			new { TenantId = ResolveTenantKey(), ProjectionName = projectionName }).ConfigureAwait(false);
	}

	private sealed record CursorMapRow(string stream_id, long position);
}
