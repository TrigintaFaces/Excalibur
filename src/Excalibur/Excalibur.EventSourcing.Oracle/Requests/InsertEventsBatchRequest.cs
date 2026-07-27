// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Globalization;

using Excalibur.Data;
using Excalibur.Dispatch;

using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Excalibur.EventSourcing.Oracle.Requests;

/// <summary>
/// The position and version of a single event inserted by <see cref="InsertEventsBatchRequest"/>.
/// </summary>
/// <remarks>
/// Each event's identity <c>POSITION</c> is returned by its own single-row
/// <c>INSERT ... RETURNING POSITION INTO</c>, so every position carries the version of the row that
/// produced it — matched to events by version, never by row order.
/// </remarks>
internal readonly record struct EventInsertPosition(long Position, long Version);

/// <summary>
/// A single event row supplied to <see cref="InsertEventsBatchRequest"/>.
/// </summary>
internal readonly record struct EventInsertRow(
	string EventId,
	string AggregateId,
	string AggregateType,
	string EventType,
	byte[] EventData,
	byte[]? Metadata,
	long Version,
	DateTimeOffset Timestamp);

/// <summary>
/// Data request that inserts a batch of events as individual single-row
/// <c>INSERT ... RETURNING POSITION INTO</c> statements executed on the same connection and transaction,
/// each returning its own inserted identity <c>POSITION</c>. All rows in one batch share the same aggregate
/// stream.
/// </summary>
/// <remarks>
/// Under the event-store append's <c>SERIALIZABLE</c> transaction, both a multi-row
/// <c>INSERT ALL ... SELECT FROM DUAL</c> and a post-insert range read-back
/// <c>SELECT ... WHERE VERSION BETWEEN</c> raise <c>ORA-08177</c> ("can't serialize access") for a
/// &gt;1-event batch, because they read blocks the transaction has just written. A per-row single-table
/// <c>INSERT ... VALUES ... RETURNING POSITION INTO</c> is a pure write whose identity value is returned by
/// the statement itself — no self-conflicting read — so it serializes cleanly.
/// </remarks>
internal sealed class InsertEventsBatchRequest : DataRequestBase<IDbConnection, IReadOnlyList<EventInsertPosition>>
{
	/// <summary>
	/// The maximum number of events per batch request. Each event is one single-row <c>INSERT</c> executed
	/// via Dapper multi-exec; 100 is a safe, round chunk. Larger appends are chunked by the caller within
	/// the same transaction.
	/// </summary>
	internal const int MaxEventsPerStatement = 100;

	/// <summary>
	/// Initializes a new instance of the <see cref="InsertEventsBatchRequest"/> class.
	/// </summary>
	/// <param name="rows">The events to insert (non-empty, within <see cref="MaxEventsPerStatement"/>, same stream).</param>
	/// <param name="transaction">The transaction to participate in.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope. The event store is a <strong>keyed</strong> tenant table, so every inserted row is
	/// stamped with a non-null tenant term: the resolved tenant when scoped, or the reserved
	/// <c>__untenanted__</c> sentinel when unscoped — routed through <see cref="KeyedTenantPartition"/>,
	/// which has no empty inhabitant. A write carrying no tenant term is therefore unrepresentable.
	/// </param>
	/// <param name="schema">The schema name for the event store table.</param>
	/// <param name="table">The event store table name.</param>
	public InsertEventsBatchRequest(
		IReadOnlyList<EventInsertRow> rows,
		IDbTransaction? transaction,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "EXCALIBUR",
		string table = "EVENTSTOREEVENTS")
	{
		ArgumentNullException.ThrowIfNull(rows);
		if (rows.Count is 0 or > MaxEventsPerStatement)
		{
			throw new ArgumentOutOfRangeException(
				nameof(rows), rows.Count, $"Batch size must be between 1 and {MaxEventsPerStatement}.");
		}

		var qualifiedTable = OracleTableName.Format(schema, table);

		// Individual single-row INSERTs (Dapper multi-exec), NOT one multi-row `INSERT ALL ... SELECT FROM
		// DUAL`. Under the append's SERIALIZABLE transaction a multi-table INSERT ALL whose row source reads
		// DUAL raises ORA-08177 ("can't serialize access") for a >1-row batch — the statement's read-consistent
		// snapshot self-conflicts with its own writes. A plain `INSERT ... VALUES` is a pure write with no read
		// source, so it serializes cleanly; Dapper runs it once per row-parameter set within the same
		// transaction. Every placeholder is unique within the single-row statement, so binding is correct
		// regardless of the provider's BindByName default.
		// The event store is a KEYED tenant table: every row carries a non-null tenant term, so the tenant
		// column and value are ALWAYS emitted — an unscoped write binds the reserved __untenanted__ sentinel
		// via KeyedTenantPartition rather than omitting the column, so an un-partitioned write is unconstructable.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantColumn = ", TENANTID";
		const string tenantValue = ", :TenantId";

		foreach (var row in rows)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(row.EventId);
			ArgumentException.ThrowIfNullOrWhiteSpace(row.AggregateId);
			ArgumentException.ThrowIfNullOrWhiteSpace(row.AggregateType);
			ArgumentException.ThrowIfNullOrWhiteSpace(row.EventType);
			ArgumentNullException.ThrowIfNull(row.EventData);
		}

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		var insertSql =
			$"INSERT INTO {qualifiedTable} (EVENTID, AGGREGATEID, AGGREGATETYPE, EVENTTYPE, EVENTDATA, METADATA, VERSION, EVENTTIMESTAMP{tenantColumn}) "
			+ $"VALUES (:EventId, :AggregateId, :AggregateType, :EventType, :EventData, :Metadata, :Version, :Timestamp{tenantValue}) "
			+ "RETURNING POSITION INTO :OutPosition";
#pragma warning restore CA2100

		Command = CreateCommand(insertSql, transaction: transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			// Each event is inserted with its OWN single-row `INSERT ... RETURNING POSITION INTO` — the
			// identity POSITION is read back as part of the INSERT statement itself, so there is no separate
			// post-insert range SELECT. Under the append's SERIALIZABLE transaction that follow-up
			// `SELECT ... WHERE VERSION BETWEEN` scanned blocks the transaction had just written and raised
			// ORA-08177 ("can't serialize access") for a >1-event append; RETURNING eliminates that read
			// entirely. Pure single-row writes serialize cleanly, and the VERSION concurrency check remains
			// inside the same SERIALIZABLE transaction (untouched). RETURNING binds each row's POSITION to
			// THAT insert, so positions carry their own version — matched to firstVersion by the caller.
			var oracleConnection = (OracleConnection)connection;
			var oracleTransaction = (OracleTransaction?)transaction;
			var positions = new List<EventInsertPosition>(rows.Count);

			foreach (var row in rows)
			{
				await using var command = oracleConnection.CreateCommand();
#pragma warning disable CA2100 // insertSql is built from schema/table validated by OracleTableName.Format; no user input.
				command.CommandText = insertSql;
#pragma warning restore CA2100
				command.BindByName = true;
				if (oracleTransaction is not null)
				{
					command.Transaction = oracleTransaction;
				}

				_ = command.Parameters.Add(new OracleParameter("EventId", OracleDbType.Varchar2) { Value = row.EventId });
				_ = command.Parameters.Add(new OracleParameter("AggregateId", OracleDbType.Varchar2) { Value = row.AggregateId });
				_ = command.Parameters.Add(new OracleParameter("AggregateType", OracleDbType.Varchar2) { Value = row.AggregateType });
				_ = command.Parameters.Add(new OracleParameter("EventType", OracleDbType.Varchar2) { Value = row.EventType });
				_ = command.Parameters.Add(new OracleParameter("EventData", OracleDbType.Blob) { Value = row.EventData });
				_ = command.Parameters.Add(new OracleParameter("Metadata", OracleDbType.Blob) { Value = (object?)row.Metadata ?? DBNull.Value });
				_ = command.Parameters.Add(new OracleParameter("Version", OracleDbType.Int64) { Value = row.Version });
				_ = command.Parameters.Add(new OracleParameter("Timestamp", OracleDbType.TimeStampTZ) { Value = row.Timestamp });
				_ = command.Parameters.Add(new OracleParameter("TenantId", OracleDbType.Varchar2) { Value = partition.TenantId });

				var outPosition = new OracleParameter("OutPosition", OracleDbType.Int64) { Direction = ParameterDirection.Output };
				_ = command.Parameters.Add(outPosition);

				_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

				positions.Add(new EventInsertPosition(ReadPosition(outPosition.Value), row.Version));
			}

			return positions;
		};
	}

	// ODP.NET surfaces a NUMBER `RETURNING` output as an OracleDecimal; convert it to the CLR long the
	// store's position contract uses.
	private static long ReadPosition(object? value) => value switch
	{
		OracleDecimal d => d.ToInt64(),
		long l => l,
		decimal m => (long)m,
		null => throw new InvalidOperationException("INSERT ... RETURNING POSITION returned no value."),
		_ => Convert.ToInt64(value, CultureInfo.InvariantCulture),
	};
}
