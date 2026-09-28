// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Globalization;
using System.Text;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Reserves a contiguous block of global positions from the counter row <strong>and</strong> inserts the
/// first chunk of events, in ONE statement, returning the first position of the block.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is one statement and not two.</b> The counter row's lock is taken by the <c>UPDATE</c> and
/// released only at COMMIT, so every other appender is blocked for the remainder of this transaction.
/// Anything issued between the allocation and the COMMIT is therefore paid by every waiting writer, not
/// just this one — and a network round trip issued there costs far more than the same round trip issued
/// outside the window. Merging the allocation into the insert removes one round trip from inside the lock.
/// </para>
/// <para>
/// Measured on SQL Server, whose append has the same shape: <b>5.24x → 3.94x</b> slower than an identity
/// column at 8 concurrent writers, and <b>6.75x → 4.90x</b> at 32. The guarantee is unchanged; only the
/// time the lock is held is.
/// </para>
/// <para>
/// <b>The ordering guarantee this carries.</b> Positions are allocated here, inside the appending
/// transaction, and never from a sequence. A PostgreSQL sequence is explicitly non-transactional — its
/// increment does NOT roll back — so an aborted append would burn a value and leave a permanent hole. The
/// counter row's increment rolls back with the transaction, so the set of committed positions is always a
/// contiguous prefix. Merging the allocation into the insert makes that stronger rather than weaker: a
/// block can no longer be reserved without also writing the rows that consume it.
/// </para>
/// <para>
/// <b>The CTE form is load-bearing.</b> PostgreSQL executes a data-modifying statement in a <c>WITH</c>
/// clause exactly once and to completion, whether or not the primary query reads its output — so the
/// insert runs even though the final <c>SELECT</c> reads only the allocation. The insert's reference to
/// <c>alloc</c> is what orders the two: without it PostgreSQL would be free to evaluate them in either
/// order.
/// </para>
/// <para>
/// <b>Do not split this back into two statements, and do not replace the counter with a sequence.</b>
/// The first is silent — the store keeps working and sustained append throughput falls. The second is
/// worse than silent: it reintroduces the permanent hole this design exists to remove.
/// </para>
/// </remarks>
internal sealed class AllocateAndInsertEventsRequest : DataRequestBase<IDbConnection, long>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AllocateAndInsertEventsRequest"/> class.
	/// </summary>
	/// <param name="rows">
	/// The first chunk of events to insert. Their <c>Position</c> is ignored and assigned server-side from
	/// the reserved block, so the caller does not need to know the block before issuing this.
	/// </param>
	/// <param name="totalEventCount">
	/// The number of positions to reserve — the whole append's event count, not this chunk's. A chunked
	/// append reserves its entire block here and the remaining chunks are written with positions derived
	/// from the returned value, so an append always takes the counter lock exactly once.
	/// </param>
	/// <param name="transaction">The transaction to participate in. Required: the allocation must roll back with the append.</param>
	/// <param name="scope">The tenant scope; every row is stamped with a non-null tenant term.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name. Default: "public".</param>
	/// <param name="table">The event store table name. Default: "events".</param>
	/// <param name="positionTable">The position counter table name. Default: "events_position".</param>
	public AllocateAndInsertEventsRequest(
		IReadOnlyList<EventInsertRow> rows,
		int totalEventCount,
		IDbTransaction transaction,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "events",
		string positionTable = "events_position")
	{
		ArgumentNullException.ThrowIfNull(rows);
		ArgumentNullException.ThrowIfNull(transaction);
		ArgumentOutOfRangeException.ThrowIfLessThan(totalEventCount, rows.Count);

		if (rows.Count is 0 or > InsertEventsBatchRequest.MaxEventsPerStatement)
		{
			throw new ArgumentOutOfRangeException(
				nameof(rows),
				rows.Count,
				$"Batch size must be between 1 and {InsertEventsBatchRequest.MaxEventsPerStatement}.");
		}

		var qualifiedTable = PgTableName.Format(schema, table);
		var qualifiedCounter = PgTableName.Format(schema, positionTable);

		var valuesBuilder = new StringBuilder();
		var parameters = new DynamicParameters();
		parameters.Add("@AllocCount", totalEventCount);

		// The event store is a KEYED tenant table: the tenant column and parameter are ALWAYS emitted, so
		// an un-partitioned write is unconstructable. One value for the batch, appended to every tuple.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantColumn = ", tenant_id";
		const string tenantValue = ",@TenantId";
		parameters.Add("@TenantId", partition.TenantId);

		for (var i = 0; i < rows.Count; i++)
		{
			var row = rows[i];
			ArgumentException.ThrowIfNullOrWhiteSpace(row.EventId);
			ArgumentException.ThrowIfNullOrWhiteSpace(row.AggregateId);
			ArgumentException.ThrowIfNullOrWhiteSpace(row.AggregateType);
			ArgumentException.ThrowIfNullOrWhiteSpace(row.EventType);
			ArgumentNullException.ThrowIfNull(row.EventData);

			var p = i.ToString(CultureInfo.InvariantCulture);
			if (i > 0)
			{
				_ = valuesBuilder.Append(',');
			}

			// The row supplies its ORDINAL, not its position: the position is computed server-side as
			// first_pos + ord, because the caller cannot know first_pos yet — it is produced by the UPDATE
			// in this same statement. The ordinal is the loop index, a compile-time-safe integer, never
			// consumer input.
			//
			// The FIRST tuple carries explicit casts and the rest do not need them. A VALUES list used as
			// a derived table takes each column's type from the first row, and a bound parameter that is
			// NULL (metadata, and event_data on an archived row) would otherwise leave the type
			// undeterminable and the statement would fail to plan.
			var cast = i == 0;
			_ = valuesBuilder
				.Append('(').Append(p).Append(cast ? "::int" : null)
				.Append(",@EventId").Append(p).Append(cast ? "::varchar" : null)
				.Append(",@AggregateId").Append(p).Append(cast ? "::varchar" : null)
				.Append(",@AggregateType").Append(p).Append(cast ? "::varchar" : null)
				.Append(",@EventType").Append(p).Append(cast ? "::varchar" : null)
				.Append(",@EventData").Append(p).Append(cast ? "::bytea" : null)
				.Append(",@Metadata").Append(p).Append(cast ? "::bytea" : null)
				.Append(",@Version").Append(p).Append(cast ? "::bigint" : null)
				.Append(",@Timestamp").Append(p).Append(cast ? "::timestamptz" : null)
				.Append(tenantValue).Append(cast ? "::varchar" : null)
				.Append(')');

			parameters.Add("@EventId" + p, row.EventId);
			parameters.Add("@AggregateId" + p, row.AggregateId);
			parameters.Add("@AggregateType" + p, row.AggregateType);
			parameters.Add("@EventType" + p, row.EventType);
			parameters.Add("@EventData" + p, row.EventData, DbType.Binary);
			parameters.Add("@Metadata" + p, row.Metadata, DbType.Binary);
			parameters.Add("@Version" + p, row.Version);
			parameters.Add("@Timestamp" + p, row.Timestamp);
		}

		// RETURNING yields the post-increment value minus the block size, so the block this append owns is
		// [value - count + 1, value]. The row lock is held until the caller commits.
		//
		// The insert reads the rows from a CROSS JOIN against `alloc` rather than referencing it with a
		// scalar subquery per tuple, and that choice is about the FAILURE path. If the counter row is
		// missing the UPDATE matches nothing, `alloc` is empty, the join yields no rows, and the whole
		// statement inserts nothing and returns nothing — so ResolveAsync sees null and can name the
		// actual fault. With a scalar subquery the insert would instead run with a NULL position and the
		// consumer would get "null value in column position violates not-null constraint", which points
		// at the wrong thing entirely. Verified against a real PostgreSQL both ways.
#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			WITH alloc AS (
				UPDATE {qualifiedCounter}
				SET value = value + @AllocCount
				WHERE id = 1
				RETURNING value - @AllocCount + 1 AS first_pos
			), ins AS (
				INSERT INTO {qualifiedTable} (position, event_id, aggregate_id, aggregate_type, event_type, event_data, metadata, version, timestamp{tenantColumn})
				SELECT a.first_pos + v.ord, v.event_id, v.aggregate_id, v.aggregate_type, v.event_type, v.event_data, v.metadata, v.version, v.timestamp, v.tenant_id
				FROM alloc a
				CROSS JOIN (VALUES {valuesBuilder}) AS v(ord, event_id, aggregate_id, aggregate_type, event_type, event_data, metadata, version, timestamp, tenant_id)
				RETURNING 1
			)
			SELECT first_pos FROM alloc
			""";
#pragma warning restore CA2100

		Command = CreateCommand(sql, parameters, transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var first = await connection.ExecuteScalarAsync<long?>(Command).ConfigureAwait(false);

			return first ?? throw new InvalidOperationException(
				$"The global position counter row is missing from {qualifiedCounter}. The event store " +
				"cannot allocate stream positions without it. Run the event store schema script against " +
				"this database; it creates the counter table and seeds the single row it requires.");
		};
	}
}
