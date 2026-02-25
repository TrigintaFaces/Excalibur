// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Data.Postgres.Saga;

/// <summary>
/// Represents a data request to save a saga state to the Postgres saga store.
/// </summary>
/// <typeparam name="TSagaState">The type of the saga state.</typeparam>
/// <remarks>
/// <para>
/// This request uses Postgres's INSERT ON CONFLICT (upsert) pattern for atomic
/// save operations. If the saga exists, it updates the state; otherwise, it inserts a new record.
/// </para>
/// <para>
/// The saga state is serialized to JSONB for efficient storage and querying capabilities.
/// </para>
/// </remarks>
public sealed class SaveSagaRequest<TSagaState> : DataRequestBase<IDbConnection, int>
	where TSagaState : SagaState
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SaveSagaRequest{TSagaState}"/> class.
	/// </summary>
	/// <param name="sagaState">The saga state to save.</param>
	/// <param name="options">The Postgres saga store options.</param>
	/// <param name="serializer">The JSON serializer for serializing saga state.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public SaveSagaRequest(
		TSagaState sagaState,
		PostgresSagaOptions options,
		IJsonSerializer serializer,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(sagaState);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(serializer);

		var sql = $"""
			INSERT INTO {options.QualifiedTableName}
				(saga_id, saga_type, state_json, is_completed, created_utc, updated_utc)
			VALUES
				(@SagaId, @SagaType, @StateJson::jsonb, @IsCompleted, NOW(), NOW())
			ON CONFLICT (saga_id) DO UPDATE SET
				state_json = EXCLUDED.state_json,
				is_completed = EXCLUDED.is_completed,
				updated_utc = NOW();
			""";

		var stateJson = serializer.Serialize(sagaState);

		Parameters.Add("SagaId", sagaState.SagaId);
		Parameters.Add("SagaType", typeof(TSagaState).Name);
		Parameters.Add("StateJson", stateJson);
		Parameters.Add("IsCompleted", sagaState.Completed);

		Command = CreateCommand(sql, commandTimeout: options.CommandTimeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = conn => conn.ExecuteAsync(Command);
	}
}
