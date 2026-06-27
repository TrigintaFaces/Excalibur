// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
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
	public LoadSagaRequest(
			Guid sagaId,
			DispatchJsonSerializer serializer,
			string qualifiedTableName,
			CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);

		// Read the authoritative Version column (bd-eszc06) so the loaded state carries the persisted
		// optimistic-concurrency version, which the store then uses as the compare-and-swap basis on save.
		// Type-isolation (1f5om2): scope the load to BOTH SagaId AND SagaType. The store persists SagaType on
		// save, so loading by SagaId alone would return a saga of a DIFFERENT type that happens to share the
		// Guid, then deserialize its StateJson into the wrong TSagaState (silent data corruption). A typed
		// LoadAsync<TSagaState>(id) must return null when no saga of that type exists at the id — the contract
		// already enforced structurally by InMemory (`state is TSagaState`), Cosmos, Firestore, and DynamoDb.
		var sql = $"SELECT StateJson, IsCompleted, Version FROM {qualifiedTableName} WHERE SagaId = @SagaId AND SagaType = @SagaType;";
		Parameters.Add("SagaId", sagaId);
		Parameters.Add("SagaType", typeof(TSagaState).Name);
		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var record = await conn.QuerySingleOrDefaultAsync<(string StateJson, bool IsCompleted, long Version)>(Command).ConfigureAwait(false);
			if (record == default)
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
}
