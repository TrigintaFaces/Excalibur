// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch.Extensions;
using Excalibur.EventSourcing.Postgres.DependencyInjection;
using Excalibur.EventSourcing.Postgres.Requests;
using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// PostgreSQL implementation of <see cref="IGlobalStreamQuery"/> that reads events from the event store
/// in global order using the <c>position</c> column.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <c>UsePostgres()</c> when the PostgreSQL event store provider is
/// configured. Uses the same schema/table settings as the event store.
/// </para>
/// <para>
/// Reading by <c>position</c> is only meaningful because the store allocates that value from a counter
/// row inside the appending transaction rather than from a sequence, so the committed positions are
/// always a contiguous prefix. A reader may therefore treat the highest position it has seen as a
/// high-water mark: no event will ever commit below it.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"The global stream is cross-aggregate and cross-tenant by definition: it is the substrate that " +
	"cross-tenant projections, materialized views, projection rebuilds and the lag read-model are built " +
	"from. Its result carries each event's own tenant on the row, so a consumer that needs confinement " +
	"filters there. Adding a tenant predicate here would not harden the statement -- it would silently " +
	"narrow the stream to one partition and make every projection built on it incomplete.")]
internal sealed class PostgresGlobalStreamQuery : IGlobalStreamQuery
{
	// Aliased to the StoredEvent member names, matching LoadEventsRequest. Dapper will not map a
	// snake_case column onto a PascalCase member, and aliasing here keeps the row type free of the
	// underscore-bearing member names that would otherwise be required.
	private const string SelectColumns =
		"position AS Position, event_id AS EventId, aggregate_id AS AggregateId, " +
		"aggregate_type AS AggregateType, event_type AS EventType, event_data AS EventData, " +
		"metadata AS Metadata, version AS Version, timestamp AS \"Timestamp\", " +
		"archived_at AS ArchivedAt";

	private readonly NpgsqlDataSource _dataSource;
	private readonly string _qualifiedTable;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresGlobalStreamQuery"/> class.
	/// </summary>
	internal PostgresGlobalStreamQuery(
		NpgsqlDataSource dataSource,
		IOptions<PostgresEventSourcingOptions> options)
	{
		ArgumentNullException.ThrowIfNull(dataSource);
		ArgumentNullException.ThrowIfNull(options);

		_dataSource = dataSource;
		_qualifiedTable = PgTableName.Format(
			options.Value.EventStoreSchema,
			options.Value.EventStoreTable);
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			SELECT {SelectColumns}
			FROM {_qualifiedTable}
			WHERE position > @Position
			ORDER BY position
			LIMIT @MaxCount
			""";
#pragma warning restore CA2100

		return await ReadAsync(connection, sql, new { Position = position.Position, MaxCount = maxCount }, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
		string eventType,
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventType);
		ArgumentNullException.ThrowIfNull(position);

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			SELECT {SelectColumns}
			FROM {_qualifiedTable}
			WHERE position > @Position AND event_type = @EventType
			ORDER BY position
			LIMIT @MaxCount
			""";
#pragma warning restore CA2100

		return await ReadAsync(
				connection,
				sql,
				new { Position = position.Position, MaxCount = maxCount, EventType = eventType },
				cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken)
	{
		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"SELECT COALESCE(MAX(position), 0) FROM {_qualifiedTable}";
#pragma warning restore CA2100

		return await connection.ExecuteScalarAsync<long>(
			new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	private static async ValueTask<IReadOnlyList<StoredEvent>> ReadAsync(
		NpgsqlConnection connection,
		string sql,
		object parameters,
		CancellationToken cancellationToken)
	{
#pragma warning disable CA2100 // sql is built from schema/table validated by PgTableName.Format; no user input
		var rows = await connection.QueryAsync<StoredEventRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

		var result = new List<StoredEvent>();
		foreach (var row in rows)
		{
			result.Add(new StoredEvent(
				row.EventId,
				row.AggregateId,
				row.AggregateType,
				row.EventType,
				row.EventData,
				row.Metadata,
				row.Version,
				row.Timestamp)
			{
				GlobalPosition = row.Position,
				ArchivedAt = row.ArchivedAt,
			});
		}

		return result.AsReadOnlyList();
	}

	/// <summary>
	/// Row mapping for Dapper query results.
	/// </summary>
	private sealed record StoredEventRow(
		long Position,
		string EventId,
		string AggregateId,
		string AggregateType,
		string EventType,
		byte[] EventData,
		byte[]? Metadata,
		long Version,
		DateTimeOffset Timestamp,
		DateTimeOffset? ArchivedAt);
}
