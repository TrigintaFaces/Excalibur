// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Saga.Oracle.Requests;

/// <summary>
/// Represents a data request that atomically purges completed saga rows whose completion instant is older
/// than a retention threshold, returning the number of rows removed.
/// </summary>
internal sealed class PurgeCompletedSagasRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PurgeCompletedSagasRequest"/> class.
	/// </summary>
	/// <param name="threshold">The exclusive upper bound: only sagas completed strictly before this instant are purged.</param>
	/// <param name="qualifiedTableName">The fully qualified saga table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope restricting the purge. <see cref="TenantScope.Scoped(string)"/> deletes only that
	/// tenant's sagas; <see cref="TenantScope.None"/> deletes only the untenanted partition — the rows that
	/// carry no tenant at all. Neither can reach another tenant's rows.
	/// </param>
	/// <param name="allTenants">
	/// When <see langword="true"/>, no tenant discriminator is emitted and completed sagas are purged across
	/// every tenant. Reachable only from <c>PurgeAllTenantsCompletedBeforeAsync</c>, whose name is the control:
	/// this flag has no caller that did not spell out the estate-wide intent, and <paramref name="scope"/> is
	/// ignored when it is set.
	/// </param>
	public PurgeCompletedSagasRequest(
		DateTimeOffset threshold,
		string qualifiedTableName,
		CancellationToken cancellationToken,
		TenantScope scope = default,
		bool allTenants = false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);

		// Three intents, each with its own predicate, none reachable from another by omission:
		//
		//   allTenants        no discriminator            the operator sweep -- named at the call site
		//   Scoped(t)         TenantId = :TenantId      exactly that tenant
		//   None              TenantId IS NULL         the untenanted partition
		//
		// The untenanted partition is a real scope, not a missing one: a store reached without a tenant owns the
		// rows that carry none, and must be able to retain them. IS NULL is the only predicate that addresses it
		// here -- this column's NULL is genuine (saga rows carry no '' sentinel), an `= :TenantId` comparison with a
		// null parameter is never true in SQL, and on Oracle an `= ''` comparison cannot match anything at all
		// because Oracle stores the empty string AS NULL. Every alternative fails silently by deleting nothing.
		// The untenanted partition is now addressed by the SAME equality predicate as a real tenant: the
		// discriminator is NOT NULL and an untenanted row carries the non-empty reserved sentinel. On Oracle this
		// is not a preference but a requirement — Oracle stores the empty string AS NULL, so neither `= :TenantId`
		// with a null bind nor `= ''` can ever match, and the previous `TenantId IS NULL` form would have gone on
		// reporting success while deleting NOTHING the moment writes began storing the sentinel.
		var partition = KeyedTenantPartition.FromScope(scope);
		var tenantPredicate = allTenants
			? string.Empty
			: " AND TenantId = :TenantId";

		// Keys on the indexed CompletedAt column (not IsCompleted + UpdatedUtc): retention correctness must not be
		// coupled to the "completed sagas never re-save" invariant via a proxy column.
		var sql = $"DELETE FROM {qualifiedTableName} WHERE CompletedAt IS NOT NULL AND CompletedAt < :Threshold{tenantPredicate}";

		// ODP.NET binds by POSITION, not by name. The add order below must match the order the placeholders
		// appear in the SQL above -- :Threshold then :TenantId -- and a parameter added but not referenced would
		// shift every subsequent value. Hence TenantId is added under exactly the condition that emits it.
		var dp = new DynamicParameters();
		dp.Add("Threshold", threshold);
		if (!allTenants)
		{
			// Emitted and bound under the SAME condition, which is what the positional-binding note above
			// requires: partition.TenantId is never null, so there is no longer a case where the predicate
			// appears without its parameter or vice versa.
			dp.Add("TenantId", partition.TenantId);
		}

		Command = new CommandDefinition(sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken);
		ResolveAsync = conn => conn.ExecuteAsync(Command);
	}
}
