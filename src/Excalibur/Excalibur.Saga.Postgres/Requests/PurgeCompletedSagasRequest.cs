// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Saga.Postgres;

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
	/// <param name="options">The Postgres saga store options.</param>
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
		PostgresSagaOptions options,
		CancellationToken cancellationToken,
		TenantScope scope = default,
		bool allTenants = false)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Defense-in-depth: validate the config-sourced qualified table name before interpolating it into SQL.
		SagaSqlValidator.ThrowIfInvalidQualifiedName(options.QualifiedTableName);

		// Three intents, each with its own predicate, none reachable from another by omission:
		//
		//   allTenants        no discriminator            the operator sweep -- named at the call site
		//   Scoped(t)         tenant_id = @TenantId      exactly that tenant
		//   None              tenant_id = @TenantId      the untenanted partition, via the sentinel
		//
		// The untenanted partition is a real scope, not a missing one: a store reached without a tenant owns the
		// rows that carry none, and must be able to retain them. It is now addressed by the SAME equality
		// predicate as a real tenant, because the discriminator is NOT NULL and an untenanted row carries the
		// reserved sentinel. That removes the failure mode this comment used to describe: the previous
		// `tenant_id IS NULL` form was correct only while the column was nullable, and would have gone on
		// reporting success while deleting NOTHING the moment writes began storing the sentinel -- a silent
		// retention failure no write-side test can see.
		//
		// The sentinel is a non-empty reserved string, not '', specifically so this comparison works on every
		// engine: Oracle stores the empty string AS NULL, so an `= ''` predicate could never match there.
		var partition = KeyedTenantPartition.FromScope(scope);
		var tenantPredicate = allTenants
			? string.Empty
			: " AND tenant_id = @TenantId";

		// Keys on the indexed completed_at column (SA w8aqq3 ruling), not is_completed + updated_utc: retention
		// correctness must not be coupled to the "completed sagas never re-save" invariant via a proxy column.
		var sql = $"DELETE FROM {options.QualifiedTableName} WHERE completed_at IS NOT NULL AND completed_at < @Threshold{tenantPredicate};";

		Parameters.Add("Threshold", threshold);
		if (!allTenants)
		{
			// Bound on both the scoped and untenanted paths -- partition.TenantId is never null, so the
			// predicate can no longer be emitted without its parameter.
			Parameters.Add("TenantId", partition.TenantId);
		}

		Command = CreateCommand(sql, commandTimeout: options.CommandTimeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = conn => conn.ExecuteAsync(Command);
	}
}
