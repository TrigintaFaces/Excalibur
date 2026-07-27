// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Saga.Oracle.Requests;

/// <summary>
/// Represents a data request to load a saga state from the Oracle saga store.
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
	/// The tenant scope. When <see cref="TenantScope.None"/> (the non-multi-tenant default), no tenant
	/// predicate or parameter is emitted; when tenant-scoped, the load is restricted to the current tenant's
	/// saga (<c>AND TenantId = :TenantId</c>) so a tenant can never load another tenant's saga by id. A
	/// tenant-scoped read cannot be constructed without a tenant, so a predicate-less all-tenants load while
	/// tenancy is active is unrepresentable.
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

		// Read the authoritative Version column so the loaded state carries the persisted optimistic-concurrency
		// version, which the store then uses as the compare-and-swap basis on save. Scope the load to BOTH SagaId
		// AND SagaType so a typed LoadAsync never returns a saga of a different type sharing the id.
		// Unconditional tenant term. It previously vanished on the unscoped path, so an unscoped load matched
		// SagaId + SagaType alone and returned whichever tenant's saga held that id. It is also the same function
		// of the same input the write side uses, so a saga saved under scope S is loadable exactly under scope S.
		//
		// On this provider the unconditional form is additionally SAFER, not merely more correct: ODP.NET binds
		// by POSITION, so a predicate that is sometimes emitted and a parameter that is sometimes added must stay
		// in lockstep or every subsequent bind shifts. Emitting and binding it always removes that coupling.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND TenantId = :TenantId";
		var sql = $"SELECT StateJson, IsCompleted, Version FROM {qualifiedTableName} WHERE SagaId = :SagaId AND SagaType = :SagaType{tenantPredicate}";

		var dp = new DynamicParameters();
		// ODP.NET has no native Guid bind type; bind the 16 canonical bytes as RAW(16), symmetric with
		// SaveSagaRequest so the stored RAW(16) matches this WHERE-clause RAW(16) byte-for-byte.
		dp.Add("SagaId", sagaId.ToByteArray());
		dp.Add("SagaType", typeof(TSagaState).Name);
		dp.Add("TenantId", partition.TenantId);

		Command = new CommandDefinition(sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var row = await conn.QuerySingleOrDefaultAsync<OracleSagaRow>(Command).ConfigureAwait(false);
			if (row is null)
			{
				return null;
			}

			var state = serializer.Deserialize<TSagaState>(row.StateJson);
			if (state is not null)
			{
				// The column is authoritative for concurrency, independent of any Version embedded in the JSON blob.
				state.Version = (long)row.Version;
			}

			return state;
		};
	}

	/// <summary>
	/// Oracle-shaped projection of a saga row.
	/// </summary>
	/// <remarks>
	/// Oracle returns <c>NUMBER(19)</c> as <see cref="decimal"/> and <c>NUMBER(1)</c> as <see cref="decimal"/>
	/// too, neither of which Dapper can bind to a positional <c>long</c>/<c>bool</c> constructor parameter — a
	/// tuple or positional record materialization throws. Settable properties are converted on assignment, so
	/// the provider's native types are read here and narrowed explicitly at the call site.
	/// <para>
	/// Scoped to this provider on purpose: a global Dapper type handler would alter <c>long</c> binding for
	/// every other database in the same process.
	/// </para>
	/// </remarks>
	private sealed class OracleSagaRow
	{
		public string StateJson { get; set; } = string.Empty;

		public decimal IsCompleted { get; set; }

		public decimal Version { get; set; }
	}
}
