// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Globalization;
using System.Text;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Reserves a contiguous block of global positions from the counter row <strong>and</strong> inserts the
/// first chunk of events, in ONE command, returning the first position of the block.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is one command and not two.</b> The counter row's exclusive lock is taken by the
/// <c>UPDATE</c> and released only at COMMIT, so every other appender in the system is blocked for the
/// remainder of this transaction. Anything issued between the allocation and the COMMIT is therefore paid
/// by every waiting writer, not just this one — and a network round trip issued there costs far more than
/// the same round trip issued outside the window. Merging the allocation into the insert removes one
/// round trip from inside the lock.
/// </para>
/// <para>
/// <b>What this path costs, stated as the SHAPE rather than as numbers.</b> Throughput here is bounded by
/// the rate at which one transaction after another can flush the log, because the allocation is serialised
/// to COMMIT:
/// </para>
/// <code>
/// this design:        throughput  ~=  1 / commit_latency    -- FLAT in writer count
/// identity column:    throughput  ~=  N / commit_latency    -- N writers share one flush
/// </code>
/// <para>
/// An identity column keeps climbing under load because the server group-commits concurrent flushes and a
/// globally serialised writer cannot join that. The only remedy that reaches the difference is batching the
/// appends themselves — never tuning this statement.
/// </para>
/// <para>
/// <b>No appends/sec figure and no ratio is quoted here, and that is a correction rather than a style
/// choice.</b> This block previously carried a per-append cost, a "practical ceiling", per-writer-count
/// throughputs for both strategies, and a ratio derived from them, all presented as properties of the
/// design. They are properties of a MACHINE. Re-measuring both strategies on a different SQL Server
/// instance moved every one of those numbers by factors between three and fourteen, and moved the derived
/// ratio by about four — while the scaling shape above reproduced exactly, identity climbing with writer
/// count and this path staying flat. The shape is the stable part, so the shape is what is written down.
/// </para>
/// <para>
/// <b>For a current figure, run the instrument; do not read one from here.</b> The fixed-window harness in
/// the benchmarks project interleaves every compared dimension, gates the confidence interval of what it
/// publishes, and REFUSES rather than reporting when the machine stalls mid-measurement. A number cached in
/// a source comment cannot do any of that and has no half-life — which is how the superseded figures above
/// outlived the conditions that produced them.
/// </para>
/// <para>
/// <b>Do not quote <c>AppendAllocationStrategyBenchmarks</c> for any of this.</b> It constructs and opens a
/// connection inside its measured region, once per append, so at 32 writers each measurement pays 32
/// connection opens and the churn — not this statement — is what degrades. It reports ~21 ms for a single
/// append against a true cost of ~2.36 ms, with a standard deviation near 40% of the mean, and it reports
/// throughput falling under concurrency when it rises. Repairing it is tracked; until then the figures
/// above are the ones to use, and they are a floor from one machine rather than a specification.
/// </para>
/// <para>
/// <b>The ordering guarantee this carries.</b> Positions are allocated here, inside the appending
/// transaction, and never from an identity column or a sequence. The counter's increment rolls back with
/// the transaction, so an aborted append burns no position and the set of committed positions is always a
/// contiguous prefix.
/// </para>
/// <para>
/// <b><c>SET XACT_ABORT ON</c> is load-bearing, not hygiene.</b> A BATCH IS NOT A STATEMENT. With
/// <c>XACT_ABORT</c> off — the server default — a run-time error in the <c>INSERT</c> aborts only that
/// statement and leaves the counter <c>UPDATE</c> applied in the still-open transaction. The error that
/// does this is not exotic: it is the unique-key violation on the stream, which is the EXPECTED outcome
/// of a lost optimistic-concurrency race. Anything that then commits instead of rolling back loses the
/// reserved block out of the committed sequence permanently. Turning it on makes the whole batch atomic,
/// so allocating a block without writing the rows that consume it becomes unrepresentable rather than
/// merely unreached.
/// </para>
/// <para>
/// Before this, the invariant survived only because every reachable catch happens to roll back — a
/// property of the callers, not of this statement. That is the weaker kind of guarantee this codebase
/// exists to avoid. <c>XACT_ABORT</c> zombies the client-side transaction on abort, which the append's
/// rollback path already tolerates: it swallows a rollback failure so it cannot mask the original fault.
/// Error numbers are unaffected, so a 2627/2601 lost race still classifies as a concurrency conflict.
/// </para>
/// <para>
/// <b>Do not split this back into two commands, and do not move the allocation earlier.</b> Both changes
/// are silent: the store keeps working and sustained append throughput falls.
/// </para>
/// <para>
/// The <c>SET</c> assigns <c>@First</c> from the pre-update value, which is what SQL Server's
/// all-at-once semantics guarantee for every expression on the right-hand side of a <c>SET</c> clause —
/// so the reserved block is <c>[old + 1, old + count]</c> regardless of the order the assignments are
/// written in.
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
	/// <param name="schema">The schema name. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	/// <param name="positionTable">The position counter table name. Default: "EventStoreEventsPosition".</param>
	public AllocateAndInsertEventsRequest(
		IReadOnlyList<EventInsertRow> rows,
		int totalEventCount,
		IDbTransaction transaction,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreEvents",
		string positionTable = "EventStoreEventsPosition")
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

		var qualifiedTable = SqlTableName.Format(schema, table);
		var qualifiedCounter = SqlTableName.Format(schema, positionTable);

		var valuesBuilder = new StringBuilder();
		var parameters = new DynamicParameters();
		parameters.Add("@AllocCount", totalEventCount);

		// The event store is a KEYED tenant table: the tenant column and parameter are ALWAYS emitted, so
		// an un-partitioned write is unconstructable. One value for the batch, appended to every tuple.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantColumn = ", TenantId";
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

			// The position is an EXPRESSION over the reserved block, not a bound parameter: the caller
			// cannot know it yet, because it is produced by the UPDATE in this same command. The offset is
			// the loop index, so it is a compile-time-safe integer and never consumer input.
			_ = valuesBuilder
				.Append("(@First+").Append(p)
				.Append(",@EventId").Append(p)
				.Append(",@AggregateId").Append(p)
				.Append(",@AggregateType").Append(p)
				.Append(",@EventType").Append(p)
				.Append(",@EventData").Append(p)
				.Append(",@Metadata").Append(p)
				.Append(",@Version").Append(p)
				.Append(",@Timestamp").Append(p)
				.Append(tenantValue)
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

		// ROWLOCK is stated rather than left to the optimizer: on a one-row table the engine may choose a
		// page or table lock, which is harmless for correctness (the row is the only row) but makes the
		// intent unreadable to anyone diagnosing contention on this table later.
		//
		// The THROW replaces the null-check the two-command form did in C#: if the UPDATE matches no row
		// the counter row is missing, which is a deployment fault. Failing here rather than inserting one
		// lazily is deliberate — a lazy insert would race other appenders and hand out duplicate positions.
#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			SET XACT_ABORT ON;

			DECLARE @First BIGINT;

			UPDATE {qualifiedCounter} WITH (ROWLOCK)
			SET @First = Value + 1, Value = Value + @AllocCount
			WHERE Id = 1;

			IF @First IS NULL
				THROW 50001, 'The global position counter row is missing. Run the event store schema script against this database; it creates the counter table and seeds the single row it requires.', 1;

			INSERT INTO {qualifiedTable} (Position, EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp{tenantColumn})
			VALUES {valuesBuilder};

			SELECT @First;
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
