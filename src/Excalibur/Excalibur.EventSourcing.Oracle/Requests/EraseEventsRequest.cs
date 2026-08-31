// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Oracle.Requests;

/// <summary>
/// Data request to erase (tombstone) events for GDPR Article 17 compliance. Nulls event payloads and
/// sets event type to the shared erased marker while preserving stream sequence.
/// </summary>
internal sealed class EraseEventsRequest : DataRequestBase<IDbConnection, int>
{
	public EraseEventsRequest(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "EXCALIBUR",
		string table = "EVENTSTOREEVENTS")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = OracleTableName.Format(schema, table);
		var erasureMetadata = $"{{\"erased\":true,\"erasureRequestId\":\"{erasureRequestId}\"}}";

		// The EVENTSTOREEVENTS table is a KEYED (tenant-columned) store, so a destructive GDPR erase must
		// NEVER emit an empty tenant predicate — an unscoped erase against a tenant-columned table would
		// tombstone every tenant's rows for this aggregate (cross-tenant GDPR data destruction). Route the
		// scope through the keyed partition: default(TenantScope) (or an absent context) becomes the
		// '__untenanted__' sentinel term, so the partition always yields a concrete, non-null tenant value.
		// The predicate is UNCONDITIONAL and NULL-safe on the column side (a legacy NULL folds to the
		// sentinel — Oracle also folds '' to NULL, which is why the sentinel is the non-empty '__untenanted__'
		// and never ''). A bare `= :TenantId` (no COALESCE) would miss legacy NULL untenanted rows; an empty
		// predicate would over-erase across tenants — both are the mutants the tenant-isolation arms forbid. ODP.NET binds by
		// position: :UntenantedSentinel appears first (inside COALESCE), then :TenantId.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(TENANTID, :UntenantedSentinel) = :TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		// EventType tombstone marker is the centralized framework-controlled constant (closed value, no
		// injection risk) so all stores and the rehydration path agree on the same discriminator. The
		// metadata JSON is written to the BLOB column as UTF-8 bytes, matching the append path.
		var sql = $"""
			UPDATE {qualifiedTable}
			SET EVENTDATA = NULL,
			    EVENTTYPE = '{ErasedEventMarker.EventType}',
			    METADATA = :ErasureMetadata
			WHERE AGGREGATEID = :AggregateId
			  AND AGGREGATETYPE = :AggregateType
			  AND EVENTTYPE <> '{ErasedEventMarker.EventType}'{tenantPredicate}
			""";
#pragma warning restore CA2100

		// ODP.NET binds by position by default; add parameters in the exact left-to-right order they appear
		// in the SQL text (SET :ErasureMetadata, then WHERE :AggregateId, :AggregateType, :UntenantedSentinel,
		// :TenantId). The keyed partition always yields a concrete, non-null tenant term, so both tenant
		// parameters are bound unconditionally — never omitted.
		var parameters = new DynamicParameters();
		parameters.Add(":ErasureMetadata", new OracleBlobParameter(System.Text.Encoding.UTF8.GetBytes(erasureMetadata)));
		parameters.Add(":AggregateId", aggregateId);
		parameters.Add(":AggregateType", aggregateType);
		parameters.Add(":UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);
		parameters.Add(":TenantId", partition.TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
