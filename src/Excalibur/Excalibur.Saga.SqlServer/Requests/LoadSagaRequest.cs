// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

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
			IJsonSerializer serializer,
			string qualifiedTableName,
			CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);

		var sql = $"SELECT StateJson, IsCompleted FROM {qualifiedTableName} WHERE SagaId = @SagaId;";
		Parameters.Add("SagaId", sagaId);
		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var record = await conn.QuerySingleOrDefaultAsync<(string StateJson, bool IsCompleted)>(Command).ConfigureAwait(false);
			return record == default ? null : await serializer.DeserializeAsync<TSagaState>(record.StateJson).ConfigureAwait(false);
		};
	}
}
