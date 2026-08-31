// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Text;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to erase (tombstone) events for GDPR Article 17 compliance.
/// Nulls event payloads and sets event type to <c>$erased</c> while preserving stream sequence.
/// </summary>
internal sealed class EraseEventsRequest : DataRequestBase<IDbConnection, int>
{
	public EraseEventsRequest(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "events")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = PgTableName.Format(schema, table);

		// The metadata column is BYTEA and the insert path binds it as DbType.Binary from a byte[], so the
		// erasure stamp must be written the same way: UTF-8 bytes, no cast. The previous form
		// (`@ErasureMetadata::jsonb`) cast the text to jsonb and assigned it to a bytea column; Postgres has
		// no implicit jsonb->bytea conversion, so the engine rejected the ENTIRE statement with
		// `42804: column "metadata" is of type bytea but expression is of type jsonb` — taking the
		// `event_data = NULL` erasure down with it. Erasure therefore threw and no payload was ever
		// destroyed. Locked by PostgresEventStoreErasureIntegrationShould against a real engine.
		var erasureMetadata = Encoding.UTF8.GetBytes(
			$"{{\"erased\":true,\"erasureRequestId\":\"{erasureRequestId}\"}}");
		// The events table is a KEYED (tenant-columned) store, so a destructive GDPR erase must NEVER emit an
		// empty tenant predicate — an unscoped erase against a tenant-columned table would tombstone every
		// tenant's rows for this aggregate (cross-tenant GDPR data destruction). Route the scope through the
		// keyed partition: default(TenantScope) (or an absent context) becomes the '__untenanted__' sentinel term,
		// so the partition always yields a concrete, non-null tenant value. The predicate is UNCONDITIONAL and
		// NULL-safe on the column side (a legacy NULL folds to the sentinel), matching the snapshot store's
		// fail-closed COALESCE form. A bare `= @TenantId` (no COALESCE) would miss legacy NULL untenanted rows;
		// an empty predicate would over-erase across tenants — both are the mutants the tenant-isolation arms forbid.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(tenant_id, @UntenantedSentinel) = @TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			UPDATE {qualifiedTable}
			SET event_data = NULL,
			    event_type = @ErasedMarker,
			    metadata = @ErasureMetadata
			WHERE aggregate_id = @AggregateId
			  AND aggregate_type = @AggregateType
			  AND event_type <> @ErasedMarker{tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		// DbType.Binary matches InsertEventsBatchRequest's binding for the same column, so an erased row's
		// metadata is byte-compatible with every row the insert path wrote.
		parameters.Add("@ErasureMetadata", erasureMetadata, DbType.Binary);
		// The keyed partition always yields a concrete, non-null tenant term (a real tenant or the
		// '__untenanted__' sentinel), so the tenant predicate is bound unconditionally — never omitted.
		parameters.Add("@TenantId", partition.TenantId);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		// Single source of truth for the erased-event sentinel (avoids a latent GDPR-erasure desync if the
		// marker value changes). Parameterized (not inlined) so the SQL stays injection-clean.
		parameters.Add("@ErasedMarker", ErasedEventMarker.EventType);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
