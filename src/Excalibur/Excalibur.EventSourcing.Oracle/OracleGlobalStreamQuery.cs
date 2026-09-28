// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch.Extensions;
using Excalibur.EventSourcing.Oracle.Requests;
using Excalibur.EventSourcing.Queries;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.EventSourcing.Oracle;

/// <summary>
/// Oracle implementation of <see cref="IGlobalStreamQuery"/> that reads events from the event store in
/// global order using the <c>POSITION</c> column.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <c>UseOracle()</c> when the Oracle event store provider is configured.
/// Uses the same schema/table settings as the event store.
/// </para>
/// <para>
/// Reading by <c>POSITION</c> is only meaningful because the store allocates that value from a counter
/// row inside the appending transaction rather than from a sequence, so the committed positions are
/// always a contiguous prefix. That matters more on Oracle than anywhere else: a sequence defaults to
/// <c>CACHE 20</c>, so a pooled session draws a block of values and can issue a LOW position long after
/// another session committed a HIGHER one. A reader may treat the highest position it has seen as a
/// high-water mark only because no sequence is involved.
/// </para>
/// <para>
/// ODP.NET binds parameters by POSITION rather than by name unless told otherwise, so the parameter
/// members below are declared in the order their placeholders appear in the statement text. Reordering
/// either without the other silently binds the wrong value to the wrong placeholder.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"The global stream is cross-aggregate and cross-tenant by definition: it is the substrate that " +
	"cross-tenant projections, materialized views, projection rebuilds and the lag read-model are built " +
	"from. Its result carries each event's own tenant on the row, so a consumer that needs confinement " +
	"filters there. Adding a tenant predicate here would not harden the statement -- it would silently " +
	"narrow the stream to one partition and make every projection built on it incomplete.")]
internal sealed class OracleGlobalStreamQuery : IGlobalStreamQuery
{
	// EVENTTIMESTAMP is aliased to a QUOTED "Timestamp" because TIMESTAMP is an Oracle keyword; the same
	// quoting the store's own read path uses.
	private const string SelectColumns =
		"POSITION AS Position, EVENTID AS EventId, AGGREGATEID AS AggregateId, " +
		"AGGREGATETYPE AS AggregateType, EVENTTYPE AS EventType, EVENTDATA AS EventData, " +
		"METADATA AS Metadata, VERSION AS Version, EVENTTIMESTAMP AS \"Timestamp\"";

	private readonly Func<OracleConnection> _connectionFactory;
	private readonly string _qualifiedTable;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleGlobalStreamQuery"/> class.
	/// </summary>
	internal OracleGlobalStreamQuery(Func<OracleConnection> connectionFactory, string schema, string table)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		_connectionFactory = connectionFactory;
		_qualifiedTable = OracleTableName.Format(schema, table);
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		var sql = $"""
			SELECT {SelectColumns}
			FROM {_qualifiedTable}
			WHERE POSITION > :Position
			ORDER BY POSITION
			FETCH FIRST :MaxCount ROWS ONLY
			""";
#pragma warning restore CA2100

		// Declared in the statement's textual placeholder order -- see the positional-binding note above.
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

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		var sql = $"""
			SELECT {SelectColumns}
			FROM {_qualifiedTable}
			WHERE POSITION > :Position AND EVENTTYPE = :EventType
			ORDER BY POSITION
			FETCH FIRST :MaxCount ROWS ONLY
			""";
#pragma warning restore CA2100

		// Declared in the statement's textual placeholder order -- see the positional-binding note above.
		return await ReadAsync(
				connection,
				sql,
				new { Position = position.Position, EventType = eventType, MaxCount = maxCount },
				cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		var sql = $"SELECT NVL(MAX(POSITION), 0) FROM {_qualifiedTable}";
#pragma warning restore CA2100

		// NUMBER(19) materializes as decimal, so the scalar is read as decimal and narrowed here rather
		// than bound straight to long.
		var head = await connection.ExecuteScalarAsync<decimal?>(
			new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);

		return head is null ? 0L : (long)head.Value;
	}

	private static async ValueTask<IReadOnlyList<StoredEvent>> ReadAsync(
		OracleConnection connection,
		string sql,
		object parameters,
		CancellationToken cancellationToken)
	{
#pragma warning disable CA2100 // sql is built from schema/table validated by OracleTableName.Format; no user input
		var rows = await connection.QueryAsync<OracleEventRow>(
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
				(long)row.Version,
				row.Timestamp.ToUniversalTime())
			{
				GlobalPosition = (long)row.Position,
			});
		}

		return result.AsReadOnlyList();
	}

	/// <summary>
	/// Row mapping for Dapper query results.
	/// </summary>
	/// <remarks>
	/// <c>NUMBER(19)</c> materializes as <see cref="decimal"/> and will not bind to a <see cref="long"/>
	/// member, and <c>TIMESTAMP(7) WITH TIME ZONE</c> materializes as <see cref="DateTimeOffset"/>. Both
	/// are narrowed in <c>ReadAsync</c> -- the same shape the store's own read path uses.
	/// </remarks>
	private sealed record OracleEventRow(
		decimal Position,
		string EventId,
		string AggregateId,
		string AggregateType,
		string EventType,
		byte[] EventData,
		byte[]? Metadata,
		decimal Version,
		DateTimeOffset Timestamp);
}
