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
/// Data request that inserts a batch of events as ONE <c>INSERT ... RETURNING POSITION INTO</c> statement
/// executed via ODP.NET array binding (<see cref="OracleCommand.ArrayBindCount"/>) — a single database
/// round-trip regardless of batch size — on the same connection and transaction, returning every row's own
/// inserted identity <c>POSITION</c> in one output array. All rows in one batch share the same aggregate
/// stream.
/// </summary>
/// <remarks>
/// Under the event-store append's <c>SERIALIZABLE</c> transaction, both a multi-row
/// <c>INSERT ALL ... SELECT FROM DUAL</c> and a post-insert range read-back
/// <c>SELECT ... WHERE VERSION BETWEEN</c> raise <c>ORA-08177</c> ("can't serialize access") for a
/// &gt;1-event batch, because they read blocks the transaction has just written. Array binding keeps the
/// same single-table <c>INSERT ... VALUES ... RETURNING POSITION INTO</c> shape — still a pure write whose
/// identity value is returned by the statement itself, no self-conflicting read — but binds every row's
/// parameters as one array per column and executes the statement once, collapsing what was N round-trips
/// (one per row, via Dapper multi-exec) into one. It is NOT <c>INSERT ALL</c>: no row source is read, so the
/// same serialization argument that ruled out <c>INSERT ALL</c> does not apply here.
/// </remarks>
internal sealed class InsertEventsBatchRequest : DataRequestBase<IDbConnection, IReadOnlyList<EventInsertPosition>>
{
	/// <summary>
	/// The maximum number of events per batch request — the array-bind size ceiling for this statement. 100
	/// is a safe, round chunk. Larger appends are chunked by the caller within the same transaction.
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

		var rowCount = rows.Count;
		ResolveAsync = async connection =>
		{
			// ONE statement, array-bound across all N rows -- ArrayBindCount tells ODP.NET how many
			// elements each parameter array carries, and the whole batch executes in a single round-trip.
			// The statement shape is UNCHANGED from the per-row form: still a pure single-table
			// `INSERT ... VALUES ... RETURNING POSITION INTO`, still no row source read -- the argument
			// that ruled out `INSERT ALL` (a read of the transaction's own just-written blocks raising
			// ORA-08177 under SERIALIZABLE) never applied to array binding, because array binding is N
			// repetitions of the same pure-write statement, sent together, not a read-driven multi-row form.
			var oracleConnection = (OracleConnection)connection;
			var oracleTransaction = (OracleTransaction?)transaction;

			await using var command = oracleConnection.CreateCommand();
#pragma warning disable CA2100 // insertSql is built from schema/table validated by OracleTableName.Format; no user input.
			command.CommandText = insertSql;
#pragma warning restore CA2100
			command.BindByName = true;
			command.ArrayBindCount = rowCount;
			if (oracleTransaction is not null)
			{
				command.Transaction = oracleTransaction;
			}

			var eventIds = new string[rowCount];
			var aggregateIds = new string[rowCount];
			var aggregateTypes = new string[rowCount];
			var eventTypes = new string[rowCount];
			var eventData = new byte[rowCount][];
			var eventDataSizes = new int[rowCount];
			var metadata = new object[rowCount];
			var metadataSizes = new int[rowCount];
			var versions = new long[rowCount];
			var timestamps = new DateTimeOffset[rowCount];
			var tenantIds = new string[rowCount];

			for (var i = 0; i < rowCount; i++)
			{
				var row = rows[i];
				eventIds[i] = row.EventId;
				aggregateIds[i] = row.AggregateId;
				aggregateTypes[i] = row.AggregateType;
				eventTypes[i] = row.EventType;
				eventData[i] = row.EventData;
				eventDataSizes[i] = row.EventData.Length;
				// A NULL element still needs an ArrayBindSize entry to keep the per-row size array aligned
				// with ArrayBindCount -- 0 for the DBNull rows, since ODP.NET requires the size array to
				// have one entry per bound row regardless of whether that row's value is null.
				metadata[i] = (object?)row.Metadata ?? DBNull.Value;
				metadataSizes[i] = row.Metadata?.Length ?? 0;
				versions[i] = row.Version;
				timestamps[i] = row.Timestamp;
				tenantIds[i] = partition.TenantId;
			}

			// Fixed-length types (Int64, TimeStampTZ) need no ArrayBindSize. Variable-length types
			// (Varchar2, Blob) require it on every array -- input included -- per ODP.NET array-bind rules.
			_ = command.Parameters.Add(new OracleParameter("EventId", OracleDbType.Varchar2) { Value = eventIds });
			_ = command.Parameters.Add(new OracleParameter("AggregateId", OracleDbType.Varchar2) { Value = aggregateIds });
			_ = command.Parameters.Add(new OracleParameter("AggregateType", OracleDbType.Varchar2) { Value = aggregateTypes });
			_ = command.Parameters.Add(new OracleParameter("EventType", OracleDbType.Varchar2) { Value = eventTypes });
			_ = command.Parameters.Add(new OracleParameter("EventData", OracleDbType.Blob) { Value = eventData, ArrayBindSize = eventDataSizes });
			_ = command.Parameters.Add(new OracleParameter("Metadata", OracleDbType.Blob) { Value = metadata, ArrayBindSize = metadataSizes });
			_ = command.Parameters.Add(new OracleParameter("Version", OracleDbType.Int64) { Value = versions });
			_ = command.Parameters.Add(new OracleParameter("Timestamp", OracleDbType.TimeStampTZ) { Value = timestamps });
			_ = command.Parameters.Add(new OracleParameter("TenantId", OracleDbType.Varchar2) { Value = tenantIds });

			// Output array: one POSITION per row, populated in the same row order the input arrays were
			// bound in. NUMBER is fixed-length, so no ArrayBindSize is required for the output either.
			var outPosition = new OracleParameter("OutPosition", OracleDbType.Int64)
			{
				Direction = ParameterDirection.Output,
			};
			_ = command.Parameters.Add(outPosition);

			_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

			return ReadPositions(outPosition.Value, rows);
		};
	}

	// ODP.NET returns an array-bound RETURNING output as an Array of per-row values (typically
	// OracleDecimal[] for a NUMBER column, occasionally already long[]/decimal[]); each element is read
	// through the same per-value conversion the single-row form used, and zipped back to the row that
	// produced it BY INDEX -- array binding preserves input row order in the output array, but this is
	// verified empirically by the real-Oracle correctness lock, not merely assumed from the
	// driver's documented behavior.
	private static IReadOnlyList<EventInsertPosition> ReadPositions(object? value, IReadOnlyList<EventInsertRow> rows)
	{
		if (value is not Array positions || positions.Length != rows.Count)
		{
			throw new InvalidOperationException(
				$"INSERT ... RETURNING POSITION array bind returned {(value as Array)?.Length.ToString(CultureInfo.InvariantCulture) ?? "no"} "
				+ $"positions for a {rows.Count}-row batch.");
		}

		var result = new List<EventInsertPosition>(rows.Count);
		for (var i = 0; i < rows.Count; i++)
		{
			result.Add(new EventInsertPosition(ReadPosition(positions.GetValue(i)), rows[i].Version));
		}

		return result;
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
