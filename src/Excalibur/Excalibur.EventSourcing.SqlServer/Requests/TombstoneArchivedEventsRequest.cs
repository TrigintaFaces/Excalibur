// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request that deletes hot-tier events up to and including a version, for one tenant's aggregate.
/// </summary>
/// <remarks>
/// This is a destructive statement against a keyed (tenant-columned) table, so the tenant predicate is
/// <strong>unconditional</strong>. The tenant term arrives as an explicit value rather than from ambient
/// context: the archive service that calls this enumerates every tenant in a single pass, so there is no
/// ambient tenant to inherit and inheriting one would delete under an arbitrary term. A deletion addressed
/// by aggregate identifier alone would remove every tenant's events for that identifier, destroying events
/// this run never archived.
/// </remarks>
public sealed class TombstoneArchivedEventsRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TombstoneArchivedEventsRequest"/> class.
	/// </summary>
	/// <param name="tenant">The tenant partition whose events are to be deleted.</param>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="toVersion">The version up to which events are deleted (inclusive).</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	public TombstoneArchivedEventsRequest(
		KeyedTenantPartition tenant,
		string aggregateId,
		string aggregateType,
		long toVersion,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreEvents")
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = SqlTableName.Format(schema, table);

		// The tenant predicate is emitted unconditionally and is NULL-safe on the column side: a legacy NULL
		// tenant (a pre-migration untenanted row not yet backfilled) folds to the reserved sentinel, matching
		// the erase and load siblings. A bare `= @TenantId` would skip those rows and leave their payloads
		// in the hot store after they were archived; an omitted predicate would tombstone across every tenant.
		const string tenantPredicate = " AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		// TOMBSTONE, not DELETE. The payload goes; the row, its Version and its Position stay, so the
		// global stream keeps no holes and a projection rebuild still sees every event in order.
		//
		// `EventData IS NOT NULL` is load-bearing rather than an optimisation. A row whose payload is
		// already NULL was ERASED under a data-subject request, and stamping ArchivedAt on it would
		// claim the payload is retrievable from cold storage when it is gone forever. It also makes
		// this statement idempotent: re-running it tombstones nothing twice.
		//
		// The timestamp comes from the DATABASE clock, not the application's. It records when the row
		// was tombstoned in the store's own timeline, so it cannot disagree with the row's other
		// timestamps because an app server's clock drifted.
		var sql = $"""
			UPDATE {qualifiedTable}
			SET EventData = NULL,
			    ArchivedAt = SYSDATETIMEOFFSET()
			WHERE AggregateId = @AggregateId
			  AND AggregateType = @AggregateType
			  AND Version <= @ToVersion
			  AND EventData IS NOT NULL{tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@ToVersion", toVersion);
		// KeyedTenantPartition has no empty inhabitant, so this always binds a concrete term — a real tenant
		// or the reserved sentinel. An all-tenants deletion is therefore unconstructable through this type.
		parameters.Add("@TenantId", tenant.TenantId);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
