// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to check whether events for an aggregate have been erased (tombstoned).
/// </summary>
internal sealed class IsErasedRequest : DataRequestBase<IDbConnection, bool>
{
	public IsErasedRequest(
		string aggregateId,
		string aggregateType,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreEvents")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = SqlTableName.Format(schema, table);
		// The Events table is a KEYED (tenant-columned) store, so an unscoped IsErased must NEVER emit an
		// empty tenant predicate: doing so would let one tenant's tombstone answer another tenant's
		// erasure check, making a required GDPR erasure SKIP (logged as already-erased). Route the scope
		// through the keyed partition: default(TenantScope) (or an absent context) becomes the '__untenanted__'
		// sentinel term, so the predicate is UNCONDITIONAL and NULL-safe on the column side (a legacy NULL
		// folds to the sentinel). A bare `= @TenantId` (no COALESCE) would miss legacy NULL untenanted rows;
		// an empty predicate reads across tenants — both are the mutants the tenant-isolation arms forbid.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		// EventType tombstone marker is the centralized framework-controlled constant (closed value, no
		// injection risk) so all stores and the rehydration path agree on the same discriminator.
		var sql = $"""
			SELECT CASE WHEN EXISTS (
			    SELECT 1 FROM {qualifiedTable}
			    WHERE AggregateId = @AggregateId
			      AND AggregateType = @AggregateType
			      AND EventType = '{ErasedEventMarker.EventType}'{tenantPredicate}
			) THEN 1 ELSE 0 END
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		// The keyed partition always yields a concrete, non-null tenant term (a real tenant or the
		// '__untenanted__' sentinel), so the tenant predicate is bound unconditionally — never omitted.
		parameters.Add("@TenantId", partition.TenantId);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteScalarAsync<bool>(Command).ConfigureAwait(false);
	}
}
