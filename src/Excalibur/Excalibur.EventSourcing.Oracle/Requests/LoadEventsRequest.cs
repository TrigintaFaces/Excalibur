// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Linq;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Oracle.Requests;

/// <summary>
/// Data request to load events for an aggregate from the Oracle event store.
/// </summary>
public sealed class LoadEventsRequest : DataRequestBase<IDbConnection, IReadOnlyList<StoredEvent>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LoadEventsRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="fromVersion">Load events after this version (-1 for all events).</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope. The event store is a <strong>keyed</strong> tenant table, so the query is always
	/// partitioned by a non-null tenant term: the resolved tenant when scoped, or the reserved
	/// <c>__untenanted__</c> sentinel when unscoped — routed through <see cref="KeyedTenantPartition"/>,
	/// which has no empty inhabitant. A predicate-less all-tenants query is therefore unrepresentable.
	/// </param>
	/// <param name="schema">The schema name for the event store table.</param>
	/// <param name="table">The event store table name.</param>
	public LoadEventsRequest(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "EXCALIBUR",
		string table = "EVENTSTOREEVENTS")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = OracleTableName.Format(schema, table);

		// The event store is a KEYED tenant table: the read is ALWAYS partitioned by a non-null tenant term.
		// Routing through KeyedTenantPartition binds the resolved tenant when scoped, or the reserved
		// __untenanted__ sentinel when unscoped; COALESCE folds a legacy NULL tenant (a pre-migration
		// untenanted row not yet backfilled) to the sentinel, matching the erase/IsErased siblings. ODP.NET
		// binds by position: the predicate is appended LAST in the WHERE clause and its two parameters are
		// added last, in the order they appear — :UntenantedSentinel (inside COALESCE), then :TenantId.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(TENANTID, :UntenantedSentinel) = :TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		var sql = $"""
			SELECT EVENTID AS EventId, AGGREGATEID AS AggregateId, AGGREGATETYPE AS AggregateType,
			       EVENTTYPE AS EventType, EVENTDATA AS EventData, METADATA AS Metadata,
			       VERSION AS Version, EVENTTIMESTAMP AS "Timestamp"
			FROM {qualifiedTable}
			WHERE AGGREGATEID = :AggregateId AND AGGREGATETYPE = :AggregateType AND VERSION > :FromVersion{tenantPredicate}
			ORDER BY VERSION ASC
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add(":AggregateId", aggregateId);
		parameters.Add(":AggregateType", aggregateType);
		parameters.Add(":FromVersion", fromVersion);
		parameters.Add(":UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);
		parameters.Add(":TenantId", partition.TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			// Oracle NUMBER(19) materializes as decimal and TIMESTAMP(7) WITH TIME ZONE as DateTimeOffset —
			// the decimal does not bind to StoredEvent's `long Version` constructor parameter, so a direct
			// QueryAsync<StoredEvent> fails Dapper materialization. Read into a provider-typed row and convert
			// explicitly. The EVENTTIMESTAMP column is WITH TIME ZONE, so Oracle returns the stored offset
			// (the append path persists OccurredAt as UTC); normalizing to UTC is idempotent.
			var rows = await connection.QueryAsync<OracleEventRow>(Command).ConfigureAwait(false);
			return rows.Select(static r => new StoredEvent(
				r.EventId,
				r.AggregateId,
				r.AggregateType,
				r.EventType,
				r.EventData,
				r.Metadata,
				(long)r.Version,
				r.Timestamp.ToUniversalTime())).ToList();
		};
	}

	/// <summary>
	/// Provider-typed projection of an event row as Oracle returns it (NUMBER → <see cref="decimal"/>,
	/// TIMESTAMP WITH TIME ZONE → <see cref="DateTimeOffset"/>), converted to <see cref="StoredEvent"/>
	/// after materialization.
	/// </summary>
	private sealed record OracleEventRow(
		string EventId,
		string AggregateId,
		string AggregateType,
		string EventType,
		byte[] EventData,
		byte[]? Metadata,
		decimal Version,
		DateTimeOffset Timestamp);
}
