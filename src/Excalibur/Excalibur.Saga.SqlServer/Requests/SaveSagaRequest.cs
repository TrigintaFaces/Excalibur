// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Saga.SqlServer.Requests;

/// <summary>
/// Represents a data request to save a saga state to the SQL Server saga store.
/// </summary>
/// <typeparam name="TSagaState">The type of the saga state.</typeparam>
public sealed class SaveSagaRequest<TSagaState> : DataRequestBase<IDbConnection, int>
	where TSagaState : SagaState
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SaveSagaRequest{TSagaState}"/> class.
	/// </summary>
	/// <param name="sagaState">The saga state to save.</param>
	/// <param name="serializer">The JSON serializer for serializing saga state.</param>
	/// <param name="qualifiedTableName">The fully qualified saga table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public SaveSagaRequest(
			TSagaState sagaState,
			IJsonSerializer serializer,
			string qualifiedTableName,
			CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);

		var sql = $"""
                        MERGE {qualifiedTableName} AS target
                        USING (SELECT @SagaId AS SagaId) AS source
                        ON (target.SagaId = source.SagaId)
                        WHEN MATCHED THEN UPDATE SET
                        StateJson = @StateJson,
                        IsCompleted = @IsCompleted,
                        UpdatedUtc = SYSUTCDATETIME()
                        WHEN NOT MATCHED THEN INSERT
                        (SagaId, SagaType, StateJson, IsCompleted)
                        VALUES (@SagaId, @SagaType, @StateJson, @IsCompleted);
                        """;

		ArgumentNullException.ThrowIfNull(sagaState);

		var stateJson = serializer.Serialize(sagaState);
		Parameters.Add("SagaId", sagaState.SagaId);
		Parameters.Add("SagaType", typeof(TSagaState).Name);
		Parameters.Add("StateJson", stateJson);
		Parameters.Add("IsCompleted", sagaState.Completed);

		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = conn => conn.ExecuteAsync(Command);
	}
}
