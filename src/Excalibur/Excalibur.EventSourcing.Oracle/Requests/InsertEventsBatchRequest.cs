// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Globalization;

using Excalibur.Data;
using Excalibur.Dispatch;

using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Excalibur.EventSourcing.Oracle.Requests;

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
	DateTimeOffset Timestamp)
{
	/// <summary>
	/// Gets the global stream position assigned to this event.
	/// </summary>
	/// <remarks>
	/// Set by the caller from the block reserved by <see cref="AllocateGlobalPositionsRequest"/>, never by
	/// the database. Declared outside the positional constructor so that building a row and assigning its
	/// position stay separate steps: rows are built while the append does its serialization work, and
	/// positions are stamped immediately before the insert, which keeps the counter's lock window short.
	/// </remarks>
	public long Position { get; init; }
}

/// <summary>
/// Data request that inserts a batch of events as ONE array-bound <c>INSERT ... VALUES</c> statement
/// executed via ODP.NET array binding (<see cref="OracleCommand.ArrayBindCount"/>) — a single database
/// round-trip regardless of batch size — on the same connection and transaction, returning every row's own
/// inserted identity <c>POSITION</c> in one output array. All rows in one batch share the same aggregate
/// stream.
/// </summary>
/// <remarks>
/// A single-table <c>INSERT ... VALUES</c> whose rows are bound as one array per column and executed as
/// ONE statement, rather than a multi-row <c>INSERT ALL ... SELECT FROM DUAL</c> or a per-row loop. Array
/// binding collapses what was N round-trips into one, which is the reason to keep this shape.
/// <para>
/// The shape was ORIGINALLY chosen for a different reason that no longer holds, recorded so a reader who
/// meets it elsewhere recognises it as superseded: under a <c>SERIALIZABLE</c> transaction, both
/// <c>INSERT ALL ... SELECT FROM DUAL</c> and a post-insert range read-back
/// <c>SELECT ... WHERE VERSION BETWEEN</c> raise <c>ORA-08177</c> ("can't serialize access") for a
/// &gt;1-event batch, because they read blocks the transaction has just written. The append now runs at
/// <see cref="System.Data.IsolationLevel.ReadCommitted"/>, and <c>ORA-08177</c> is raised only under
/// <c>SERIALIZABLE</c>, so that argument is no longer load-bearing here.
/// </para>
/// </remarks>
internal sealed class InsertEventsBatchRequest : DataRequestBase<IDbConnection, int>
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

		// A single-table `INSERT ... VALUES`, array-bound across every row (see the class remarks), NOT a
		// multi-row `INSERT ALL ... SELECT FROM DUAL`. Every placeholder is unique within the statement, so
		// binding is correct regardless of the provider's BindByName default.
		//
		// The original reason for ruling out INSERT ALL was that its row source reads DUAL, so under a
		// SERIALIZABLE transaction it self-conflicts with its own writes and raises ORA-08177 ("can't
		// serialize access") for a >1-row batch. THAT REASON NO LONGER APPLIES: the append now runs at
		// READ COMMITTED, and ORA-08177 is raised only under SERIALIZABLE. It is recorded here because the
		// shape it produced is still the one we want for an unrelated and still-valid reason — array
		// binding executes one statement for N rows, which INSERT ALL would not improve on — and because a
		// reader who meets the old rationale elsewhere should know it is superseded rather than act on it.
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
			$"INSERT INTO {qualifiedTable} (POSITION, EVENTID, AGGREGATEID, AGGREGATETYPE, EVENTTYPE, EVENTDATA, METADATA, VERSION, EVENTTIMESTAMP{tenantColumn}) "
			+ $"VALUES (:Position, :EventId, :AggregateId, :AggregateType, :EventType, :EventData, :Metadata, :Version, :Timestamp{tenantValue})";
#pragma warning restore CA2100

		Command = CreateCommand(insertSql, transaction: transaction, cancellationToken: cancellationToken);

		var rowCount = rows.Count;
		ResolveAsync = async connection =>
		{
			// ONE statement, array-bound across all N rows -- ArrayBindCount tells ODP.NET how many
			// elements each parameter array carries, and the whole batch executes in a single round-trip.
			// The statement shape is UNCHANGED from the per-row form: still a pure single-table
			// `INSERT ... VALUES`, still no row source read -- the argument
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

			var positions = new long[rowCount];
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
				positions[i] = row.Position;
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
			_ = command.Parameters.Add(new OracleParameter("Position", OracleDbType.Int64) { Value = positions });
			_ = command.Parameters.Add(new OracleParameter("EventId", OracleDbType.Varchar2) { Value = eventIds });
			_ = command.Parameters.Add(new OracleParameter("AggregateId", OracleDbType.Varchar2) { Value = aggregateIds });
			_ = command.Parameters.Add(new OracleParameter("AggregateType", OracleDbType.Varchar2) { Value = aggregateTypes });
			_ = command.Parameters.Add(new OracleParameter("EventType", OracleDbType.Varchar2) { Value = eventTypes });
			_ = command.Parameters.Add(new OracleParameter("EventData", OracleDbType.Blob) { Value = eventData, ArrayBindSize = eventDataSizes });
			_ = command.Parameters.Add(new OracleParameter("Metadata", OracleDbType.Blob) { Value = metadata, ArrayBindSize = metadataSizes });
			_ = command.Parameters.Add(new OracleParameter("Version", OracleDbType.Int64) { Value = versions });
			_ = command.Parameters.Add(new OracleParameter("Timestamp", OracleDbType.TimeStampTZ) { Value = timestamps });
			_ = command.Parameters.Add(new OracleParameter("TenantId", OracleDbType.Varchar2) { Value = tenantIds });

			// Positions are assigned by the caller from the store's position counter before this request is
			// built, so there is nothing to read back: no RETURNING clause, no per-row output array, and no
			// zipping of returned values to input rows by index. That whole correspondence problem existed
			// only because an identity column chose the numbers.
			return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		};
	}

}
