// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
internal sealed class PostgresGlobalStreamQuery : IGlobalStreamQuery, IProjectionDestinationScope
{
    string IProjectionDestinationScope.Scope
    {
        get
        {
            using var connection = _dataSource.CreateConnection();
            return string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.Database)
                ? string.Empty
                : "postgres/" + Uri.EscapeDataString(connection.DataSource) + "/" + Uri.EscapeDataString(connection.Database);
        }
    }

	// Materialized explicitly by ordinal: PostgreSQL exposes timestamptz as DateTime by default,
	// so reflective constructor matching cannot populate a DateTimeOffset record reliably.
	private const string SelectColumns =
		"position AS Position, event_id AS EventId, aggregate_id AS AggregateId, " +
		"aggregate_type AS AggregateType, event_type AS EventType, event_data AS EventData, " +
		"metadata AS Metadata, version AS Version, timestamp AS \"Timestamp\", " +
		"archived_at AS ArchivedAt, COALESCE(tenant_id, '__untenanted__') AS TenantId";

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

		return await ReadAsync(connection, sql, position.Position, maxCount, null, cancellationToken)
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
				position.Position, maxCount, eventType,
				cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken)
	{
		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"SELECT COALESCE(MAX(position), 0) FROM {_qualifiedTable}";
		await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
		return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
	}

	private static async ValueTask<IReadOnlyList<StoredEvent>> ReadAsync(
		NpgsqlConnection connection,
		string sql,
		long position,
		int maxCount,
		string? eventType,
		CancellationToken cancellationToken)
	{
#pragma warning disable CA2100 // sql is built from schema/table validated by PgTableName.Format; no user input
		await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
		command.Parameters.Add(new NpgsqlParameter<long>("Position", position));
		command.Parameters.Add(new NpgsqlParameter<int>("MaxCount", maxCount));
		if (eventType is not null) { command.Parameters.Add(new NpgsqlParameter<string>("EventType", eventType)); }
		await using var rows = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

		var result = new List<StoredEvent>();
		while (await rows.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			result.Add(new StoredEvent(
				rows.GetString(1), rows.GetString(2), rows.GetString(3), rows.GetString(4),
				await rows.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? null : await rows.GetFieldValueAsync<byte[]>(5, cancellationToken).ConfigureAwait(false),
				await rows.IsDBNullAsync(6, cancellationToken).ConfigureAwait(false) ? null : await rows.GetFieldValueAsync<byte[]>(6, cancellationToken).ConfigureAwait(false),
				rows.GetInt64(7), await rows.GetFieldValueAsync<DateTimeOffset>(8, cancellationToken).ConfigureAwait(false))
			{
				GlobalPosition = rows.GetInt64(0),
				ArchivedAt = await rows.IsDBNullAsync(9, cancellationToken).ConfigureAwait(false) ? null : await rows.GetFieldValueAsync<DateTimeOffset>(9, cancellationToken).ConfigureAwait(false),
				TenantId = rows.GetString(10),
			});
		}

		return result.AsReadOnlyList();
	}

}
