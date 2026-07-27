// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to load events for an aggregate from the event store.
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
	/// The tenant scope. The event store is a <strong>keyed</strong> tenant table, so the query is
	/// always partitioned by a non-null tenant term: the resolved tenant when scoped, or the reserved
	/// <c>__untenanted__</c> sentinel when unscoped — routed through <see cref="KeyedTenantPartition"/>,
	/// which has no empty inhabitant. A predicate-less all-tenants query is therefore unrepresentable.
	/// </param>
	/// <param name="schema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	public LoadEventsRequest(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreEvents")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = SqlTableName.Format(schema, table);
		// The event store is a KEYED tenant table (FR-8): the read is ALWAYS partitioned by a non-null tenant
		// term. Routing through KeyedTenantPartition binds the resolved tenant when scoped, or the reserved
		// __untenanted__ sentinel when unscoped — so an un-partitioned (all-tenants) read is unconstructable
		// (no empty predicate can be emitted). COALESCE folds a legacy NULL tenant (a pre-migration
		// untenanted row not yet backfilled) to the sentinel, matching the erase/IsErased siblings; a bare
		// `= @TenantId` would miss those rows during the migration window.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			SELECT EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp
			FROM {qualifiedTable}
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType AND Version > @FromVersion{tenantPredicate}
			ORDER BY Version ASC
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@FromVersion", fromVersion);
		parameters.Add("@TenantId", partition.TenantId);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var events = await connection.QueryAsync<StoredEvent>(Command).ConfigureAwait(false);
			return events.AsList();
		};
	}
}
