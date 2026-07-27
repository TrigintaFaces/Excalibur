// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Text;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.SqlServer.Requests;

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
		string schema = "dbo",
		string table = "EventStoreEvents")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = SqlTableName.Format(schema, table);
		// The Metadata column is VARBINARY(MAX) and the insert path binds it as DbType.Binary from a byte[],
		// so the erasure stamp must be written the same way: UTF-8 bytes. Binding the JSON as a string made
		// SQL Server reject the whole statement — "Implicit conversion from data type nvarchar to
		// varbinary(max) is not allowed" — which took the `EventData = NULL` erasure down with it, so
		// erasure threw and no payload was ever destroyed. Measured against a real engine; the implicit
		// conversion that the SQL Server type-precedence chart appears to promise does NOT apply in an
		// assignment context. Locked by SqlServerEventStoreErasureIntegrationShould.
		var erasureMetadata = Encoding.UTF8.GetBytes(
			$"{{\"erased\":true,\"erasureRequestId\":\"{erasureRequestId}\"}}");
		// The Events table is a KEYED (tenant-columned) store, so a destructive GDPR erase must NEVER emit an
		// empty tenant predicate — an unscoped erase against a tenant-columned table would tombstone every
		// tenant's rows for this aggregate (cross-tenant GDPR data destruction). Route the scope through the
		// keyed partition: TenantScope.None (or an absent context) becomes the '__untenanted__' sentinel term,
		// so the partition always yields a concrete, non-null tenant value. The predicate is UNCONDITIONAL and
		// NULL-safe on the column side (a legacy NULL folds to the sentinel), matching the snapshot store's
		// fail-closed COALESCE form. A bare `= @TenantId` (no COALESCE) would miss legacy NULL untenanted rows;
		// an empty predicate would over-erase across tenants — both are the mutants AC-6 forbids.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		// EventType tombstone marker is the centralized framework-controlled constant (closed value, no
		// injection risk) so all stores and the rehydration path agree on the same discriminator.
		var sql = $"""
			UPDATE {qualifiedTable}
			SET EventData = NULL,
			    EventType = '{ErasedEventMarker.EventType}',
			    Metadata = @ErasureMetadata
			WHERE AggregateId = @AggregateId
			  AND AggregateType = @AggregateType
			  AND EventType <> '{ErasedEventMarker.EventType}'{tenantPredicate}
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

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
