// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Dapper;

using Excalibur.Data;
using Excalibur.Data.Validation;
using Excalibur.Dispatch.Extensions;
using Excalibur.EventSourcing.Queries;

using Microsoft.Data.Sqlite;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>
/// SQLite implementation of <see cref="IGlobalStreamQuery"/> that reads events from the event store in
/// global order using the <c>GlobalPosition</c> column.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <c>UseSqlite()</c> when the SQLite event store provider is configured.
/// Uses the same table setting as the event store.
/// </para>
/// <para>
/// Reading by <c>GlobalPosition</c> is only meaningful because the store allocates that value from a
/// counter row inside the appending transaction rather than letting SQLite assign a rowid, so the
/// committed positions are always a contiguous prefix. A reader may therefore treat the highest position
/// it has seen as a high-water mark: no event will ever commit below it.
/// </para>
/// <para>
/// The events table is created on demand by the store rather than by a migration, so every read here
/// ensures it first. Without that, a query issued before the first append fails with "no such table"
/// on a fresh database file rather than returning an empty stream.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"The global stream is cross-aggregate and cross-tenant by definition: it is the substrate that " +
	"cross-tenant projections, materialized views, projection rebuilds and the lag read-model are built " +
	"from. Its result carries each event's own tenant on the row, so a consumer that needs confinement " +
	"filters there. Adding a tenant predicate here would not harden the statement -- it would silently " +
	"narrow the stream to one partition and make every projection built on it incomplete.")]
internal sealed class SqliteGlobalStreamQuery : IGlobalStreamQuery
{
	private const string SelectColumns =
		"GlobalPosition, EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp";

	private readonly string _connectionString;
	private readonly string _table;
	private readonly bool _requireTenant;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteGlobalStreamQuery"/> class.
	/// </summary>
	internal SqliteGlobalStreamQuery(string connectionString, string table, bool requireTenant)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(table);
		SqlIdentifierValidator.ThrowIfInvalid(table, nameof(table));

		_connectionString = connectionString;
		_table = table;
		_requireTenant = requireTenant;
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);

		await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $"""
			SELECT {SelectColumns}
			FROM [{_table}]
			WHERE GlobalPosition > @Position
			ORDER BY GlobalPosition
			LIMIT @MaxCount
			""";

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

		await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $"""
			SELECT {SelectColumns}
			FROM [{_table}]
			WHERE GlobalPosition > @Position AND EventType = @EventType
			ORDER BY GlobalPosition
			LIMIT @MaxCount
			""";

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
		await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $"SELECT COALESCE(MAX(GlobalPosition), 0) FROM [{_table}]";

#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in the constructor
		return await connection.ExecuteScalarAsync<long>(
			new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100
	}

	private async ValueTask<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
	{
		var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureEventsTableAsync(connection, _table, _requireTenant, cancellationToken)
			.ConfigureAwait(false);

		return connection;
	}

	private static async ValueTask<IReadOnlyList<StoredEvent>> ReadAsync(
		SqliteConnection connection,
		string sql,
		object parameters,
		CancellationToken cancellationToken)
	{
#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in the constructor
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
				// SQLite has no timestamp type: the store writes the round-trip ("O") form as TEXT, so the
				// value is parsed here rather than cast in SQL. Ordering is by GlobalPosition for the same
				// reason -- ORDER BY on this column would be lexicographic over text.
				DateTimeOffset.Parse(row.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
			{
				GlobalPosition = row.GlobalPosition,
			});
		}

		return result.AsReadOnlyList();
	}

	/// <summary>
	/// Row mapping for Dapper query results.
	/// </summary>
	private sealed record StoredEventRow(
		long GlobalPosition,
		string EventId,
		string AggregateId,
		string AggregateType,
		string EventType,
		byte[] EventData,
		byte[]? Metadata,
		long Version,
		string Timestamp);
}
