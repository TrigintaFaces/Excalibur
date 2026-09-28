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
/// Data request that inserts a batch of events into the Postgres event store with a <strong>single</strong>
/// multi-row <c>INSERT ... VALUES ... RETURNING</c> statement, returning each inserted event's position and
/// version. Replaces the per-event insert loop so an append is one round-trip and one atomic statement.
/// </summary>
internal sealed class InsertEventsBatchRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// The maximum number of events per statement. PostgreSQL caps a command at 65535 parameters; 256
	/// events (2048 parameters) is a conservative chunk that keeps statement text small. Larger appends
	/// are chunked by the caller within the same transaction.
	/// </summary>
	internal const int MaxEventsPerStatement = 256;

	/// <summary>
	/// Initializes a new instance of the <see cref="InsertEventsBatchRequest"/> class.
	/// </summary>
	/// <param name="rows">The events to insert (must be non-empty and within <see cref="MaxEventsPerStatement"/>).</param>
	/// <param name="transaction">The transaction to participate in.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope. The event store is a <strong>keyed</strong> tenant table, so every inserted row is
	/// stamped with a non-null tenant term: the resolved tenant when scoped, or the reserved
	/// <c>__untenanted__</c> sentinel when unscoped — routed through <see cref="KeyedTenantPartition"/>,
	/// which has no empty inhabitant. A write carrying no tenant term is therefore unrepresentable.
	/// </param>
	/// <param name="schema">The schema name for the event store table. Default: "public".</param>
	/// <param name="table">The event store table name. Default: "events".</param>
	public InsertEventsBatchRequest(
		IReadOnlyList<EventInsertRow> rows,
		IDbTransaction? transaction,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "events")
	{
		ArgumentNullException.ThrowIfNull(rows);
		if (rows.Count is 0 or > MaxEventsPerStatement)
		{
			throw new ArgumentOutOfRangeException(
				nameof(rows),
				rows.Count,
				$"Batch size must be between 1 and {MaxEventsPerStatement}.");
		}

		var qualifiedTable = PgTableName.Format(schema, table);

		var valuesBuilder = new StringBuilder();
		var parameters = new DynamicParameters();

		// The event store is a KEYED tenant table: every row carries a non-null tenant term, so the tenant
		// column and parameter are ALWAYS emitted — routing through KeyedTenantPartition makes an unscoped
		// write bind the reserved __untenanted__ sentinel rather than omit the column, so an un-partitioned
		// (all-tenants) write is unconstructable. One value for the whole batch, bound once.
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

			_ = valuesBuilder
				.Append("(@Position").Append(p)
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

			parameters.Add("@Position" + p, row.Position);
			parameters.Add("@EventId" + p, row.EventId);
			parameters.Add("@AggregateId" + p, row.AggregateId);
			parameters.Add("@AggregateType" + p, row.AggregateType);
			parameters.Add("@EventType" + p, row.EventType);
			parameters.Add("@EventData" + p, row.EventData, DbType.Binary);
			parameters.Add("@Metadata" + p, row.Metadata, DbType.Binary);
			parameters.Add("@Version" + p, row.Version);
			parameters.Add("@Timestamp" + p, row.Timestamp);
		}

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			INSERT INTO {qualifiedTable} (position, event_id, aggregate_id, aggregate_type, event_type, event_data, metadata, version, timestamp{tenantColumn})
			VALUES {valuesBuilder}
			""";
#pragma warning restore CA2100

		Command = CreateCommand(sql, parameters, transaction, cancellationToken: cancellationToken);

		// Positions are assigned by the caller from the store's position counter before this request is
		// built, so there is nothing to read back: no RETURNING clause, and no need to match returned rows
		// to events by version. The previous implementation recovered sequence values here, which is
		// precisely the allocation strategy the position counter replaces.
		ResolveAsync = connection => connection.ExecuteAsync(Command);
	}
}

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
	/// Set by the caller from the block reserved by <see cref="AllocateAndInsertEventsRequest"/>, never by
	/// the database. Declared outside the positional constructor so that building a row and assigning its
	/// position stay separate steps: rows are built while the append does its serialization work, and
	/// positions are stamped immediately before the insert, which keeps the counter's lock window short.
	/// </remarks>
	public long Position { get; init; }
}
