// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Saga.SqlServer.Requests;

/// <summary>
/// Represents a data request to load a saga state from the SQL Server saga store.
/// </summary>
/// <typeparam name="TSagaState">The type of the saga state.</typeparam>
public sealed class LoadSagaRequest<TSagaState> : DataRequestBase<IDbConnection, TSagaState?>
	where TSagaState : SagaState
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LoadSagaRequest{TSagaState}"/> class.
	/// </summary>
	/// <param name="sagaId">The unique identifier of the saga to load.</param>
	/// <param name="serializer">The JSON serializer for deserializing saga state.</param>
	/// <param name="qualifiedTableName">The fully qualified saga table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope for row-level tenant scoping. When tenant-scoped, the load is restricted to the
	/// current tenant's saga (<c>AND TenantId = @TenantId</c>) so a tenant can never load another tenant's
	/// saga by id. An untenanted scope binds the reserved sentinel, so the predicate is applied either way.
	/// A tenant-scoped scope cannot be constructed without a
	/// tenant, so a predicate-less load while tenancy is active is unrepresentable.
	/// </param>
	public LoadSagaRequest(
			Guid sagaId,
			DispatchJsonSerializer serializer,
			string qualifiedTableName,
			TenantScope scope,
			CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);

		// Read the authoritative Version column so the loaded state carries the persisted
		// optimistic-concurrency version, which the store then uses as the compare-and-swap basis on save.
		// Type-isolation: scope the load to BOTH SagaId AND SagaType. The store persists SagaType on
		// save, so loading by SagaId alone would return a saga of a DIFFERENT type that happens to share the
		// Guid, then deserialize its StateJson into the wrong TSagaState (silent data corruption). A typed
		// LoadAsync<TSagaState>(id) must return null when no saga of that type exists at the id — the contract
		// already enforced structurally by InMemory (`state is TSagaState`), Cosmos, Firestore, and DynamoDb.
		// The tenant predicate is UNCONDITIONAL. It used to disappear on the unscoped path
		// (scope.IsScoped ? " AND TenantId = @TenantId" : string.Empty), so an unscoped load matched on
		// SagaId + SagaType alone and returned WHICHEVER tenant's saga held that id — a cross-tenant read
		// through the ordinary load path. Resolving through KeyedTenantPartition gives a term on both paths
		// (unscoped resolves to the reserved untenanted sentinel), so the predicate is always present and an
		// unscoped caller reads the untenanted partition rather than everything.
		//
		// This is the same function of the same input the write side uses, which is what makes the two agree:
		// a saga saved under scope S is loadable exactly under scope S.
		var partition = KeyedTenantPartition.FromScope(scope);
		var sql = $"SELECT StateJson, IsCompleted, Version FROM {qualifiedTableName} WHERE SagaId = @SagaId AND SagaType = @SagaType AND TenantId = @TenantId;";
		Parameters.Add("SagaId", sagaId);
		Parameters.Add("SagaType", typeof(TSagaState).Name);
		Parameters.Add("TenantId", partition.TenantId);
		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			// Project into a nullable reference type so "no row" is unambiguously null. A value-tuple
			// `== default` conflates an absent row with a legitimately-persisted row whose columns all
			// equal their defaults — a brittle null-detect.
			var record = await conn.QuerySingleOrDefaultAsync<SagaLoadRow>(Command).ConfigureAwait(false);
			if (record is null)
			{
				return null;
			}

			var state = serializer.Deserialize<TSagaState>(record.StateJson);
			if (state is not null)
			{
				// The column is authoritative for concurrency, independent of any Version embedded in the JSON blob.
				state.Version = record.Version;
			}

			return state;
		};
	}

	/// <summary>Row projection for the saga load query. A null instance means no matching row exists.</summary>
	private sealed record SagaLoadRow(string StateJson, bool IsCompleted, long Version);
}
