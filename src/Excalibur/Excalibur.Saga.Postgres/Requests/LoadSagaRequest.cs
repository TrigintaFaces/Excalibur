// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Saga.Postgres;

/// <summary>
/// Represents a data request to load a saga state from the Postgres saga store.
/// </summary>
/// <typeparam name="TSagaState">The type of the saga state.</typeparam>
/// <remarks>
/// <para>
/// This request uses Postgres's snake_case column naming convention and JSONB
/// storage for efficient saga state retrieval.
/// </para>
/// </remarks>
public sealed class LoadSagaRequest<TSagaState> : DataRequestBase<IDbConnection, TSagaState?>
	where TSagaState : SagaState
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LoadSagaRequest{TSagaState}"/> class.
	/// </summary>
	/// <param name="sagaId">The unique identifier of the saga to load.</param>
	/// <param name="options">The Postgres saga store options.</param>
	/// <param name="serializer">The JSON serializer for deserializing saga state.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope for row-level tenant scoping. When tenant-scoped, the load is restricted to the
	/// current tenant's saga (<c>AND tenant_id = @TenantId</c>) so a tenant can never load another tenant's
	/// saga by id. When <see cref="TenantScope.None"/> no tenant predicate is applied (byte-identical
	/// behavior). A tenant-scoped scope cannot be constructed without a tenant, so a predicate-less load
	/// while tenancy is active is unrepresentable.
	/// </param>
	public LoadSagaRequest(
		Guid sagaId,
		PostgresSagaOptions options,
		DispatchJsonSerializer serializer,
		TenantScope scope,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(serializer);

		// Defense-in-depth (bd-r5r7fe): validate the config-sourced qualified table name before interpolating
		// it into SQL — parity with SqlServer's LoadSagaRequest. SagaSqlValidator enforces the safe
		// "schema"."table" identifier shape.
		SagaSqlValidator.ThrowIfInvalidQualifiedName(options.QualifiedTableName);

		// Read the authoritative version column (skl8r7) so the loaded state carries the persisted
		// optimistic-concurrency version, which the store then uses as the compare-and-swap basis on save.
		// Type-isolation (1f5om2): scope the load to BOTH saga_id AND saga_type. The store persists saga_type
		// on save, so loading by saga_id alone would return a saga of a DIFFERENT type that shares the Guid,
		// then deserialize its state_json into the wrong TSagaState (silent data corruption). A typed
		// LoadAsync<TSagaState>(id) must return null when no saga of that type exists at the id — the contract
		// already enforced structurally by InMemory (`state is TSagaState`), Cosmos, Firestore, and DynamoDb.
		// Unconditional tenant term. It previously vanished on the unscoped path, so an unscoped load
		// matched saga_id + saga_type alone and returned whichever tenant's saga held that id. Resolving
		// through KeyedTenantPartition yields a term on both paths (unscoped -> reserved sentinel), and it is
		// the SAME function of the SAME input the write side uses, so a saga saved under scope S is loadable
		// exactly under scope S.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND tenant_id = @TenantId";
		var sql = $"""
			SELECT state_json, is_completed, version
			FROM {options.QualifiedTableName}
			WHERE saga_id = @SagaId AND saga_type = @SagaType{tenantPredicate};
			""";

		Parameters.Add("SagaId", sagaId);
		Parameters.Add("SagaType", typeof(TSagaState).Name);
		Parameters.Add("TenantId", partition.TenantId);
		Command = CreateCommand(sql, commandTimeout: options.CommandTimeoutSeconds, cancellationToken: cancellationToken);

		ResolveAsync = async conn =>
		{
			var record = await conn
				.QuerySingleOrDefaultAsync<SagaRecord>(Command)
				.ConfigureAwait(false);

			if (record is null || string.IsNullOrEmpty(record.state_json))
			{
				return null;
			}

			var state = serializer.Deserialize<TSagaState>(record.state_json);
			if (state is not null)
			{
				// The column is authoritative for concurrency, independent of any Version embedded in the JSON blob.
				state.Version = record.version;
			}

			return state;
		};
	}

	/// <summary>
	/// Internal record for mapping Postgres snake_case columns.
	/// </summary>
	// ReSharper disable InconsistentNaming - Column names use snake_case
	private sealed record SagaRecord(string state_json, bool is_completed, long version);
	// ReSharper restore InconsistentNaming
}
