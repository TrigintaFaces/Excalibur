// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to load events for an aggregate from the Postgres event store.
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
	/// <param name="schema">The schema name for the event store table. Default: "public".</param>
	/// <param name="table">The event store table name. Default: "events".</param>
	public LoadEventsRequest(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "events")
		: this(aggregateId, aggregateType, fromVersion, KeyedTenantPartition.FromScope(scope), long.MaxValue, schema, table, cancellationToken)
	{
	}

	internal LoadEventsRequest(
		string aggregateId, string aggregateType, long fromVersion, KeyedTenantPartition partition,
		long upToVersion, string schema, string table, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(partition);
		ArgumentOutOfRangeException.ThrowIfNegative(upToVersion);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = PgTableName.Format(schema, table);
		// The event store is a KEYED tenant table: the read is ALWAYS partitioned by a non-null tenant term.
		// Routing through KeyedTenantPartition binds the resolved tenant when scoped, or the reserved
		// __untenanted__ sentinel when unscoped — so an un-partitioned (all-tenants) read is unconstructable.
		// COALESCE folds a legacy NULL tenant (a pre-migration untenanted row not yet backfilled) to the
		// sentinel, matching the erase/IsErased siblings; a bare `= @TenantId` would miss those rows.
		const string tenantPredicate = " AND COALESCE(tenant_id, @UntenantedSentinel) = @TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			SELECT event_id AS EventId, aggregate_id AS AggregateId, aggregate_type AS AggregateType,
			       event_type AS EventType, event_data AS EventData, metadata AS Metadata,
			       version AS Version, timestamp AS Timestamp, archived_at AS ArchivedAt,
			       position AS GlobalPosition, COALESCE(tenant_id, @UntenantedSentinel) AS TenantId
			FROM {qualifiedTable}
			WHERE aggregate_id = @AggregateId AND aggregate_type = @AggregateType AND version > @FromVersion AND version <= @UpToVersion{tenantPredicate}
			ORDER BY version ASC
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@FromVersion", fromVersion);
		parameters.Add("@UpToVersion", upToVersion);
		parameters.Add("@TenantId", partition.TenantId);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var rows = await connection.QueryAsync<LoadedEventRow>(Command).ConfigureAwait(false);

			var events = new List<StoredEvent>();
			foreach (var row in rows)
			{
				events.Add(new StoredEvent(
					row.EventId,
					row.AggregateId,
					row.AggregateType,
					row.EventType,
					row.EventData,
					row.Metadata,
					row.Version,
					row.Timestamp)
				{
					ArchivedAt = row.ArchivedAt,
					GlobalPosition = row.GlobalPosition,
					TenantId = row.TenantId,
				});
			}

			return events;
		};
	}
}

/// <summary>
/// Row mapping for Dapper query results.
/// </summary>
/// <remarks>
/// <see cref="StoredEvent"/> cannot be materialized directly once the SELECT carries
/// <c>ArchivedAt</c>: that member is an <c>init</c> property declared OUTSIDE the positional
/// constructor, and Dapper matches a result set to a CONSTRUCTOR. Selecting the column without this row
/// type fails at runtime with "a parameterless default constructor or one matching signature ... is
/// required", which no compiler can see.
/// Npgsql exposes TIMESTAMPTZ as UTC DateTime to Dapper's constructor matching. The row uses that
/// native type; assigning it to StoredEvent converts it to DateTimeOffset while preserving the instant.
/// </remarks>
internal sealed record LoadedEventRow(
	string EventId,
	string AggregateId,
	string AggregateType,
	string EventType,
	byte[]? EventData,
	byte[]? Metadata,
	long Version,
	DateTime Timestamp,
	DateTime? ArchivedAt,
	long GlobalPosition,
	string TenantId);
