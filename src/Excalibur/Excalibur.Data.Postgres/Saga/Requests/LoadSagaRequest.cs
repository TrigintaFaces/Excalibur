// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Data.Postgres.Saga;

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
	public LoadSagaRequest(
		Guid sagaId,
		PostgresSagaOptions options,
		IJsonSerializer serializer,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(serializer);

		var sql = $"""
			SELECT state_json, is_completed
			FROM {options.QualifiedTableName}
			WHERE saga_id = @SagaId;
			""";

		Parameters.Add("SagaId", sagaId);
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

			return await serializer
				.DeserializeAsync<TSagaState>(record.state_json)
				.ConfigureAwait(false);
		};
	}

	/// <summary>
	/// Internal record for mapping Postgres snake_case columns.
	/// </summary>
	// ReSharper disable InconsistentNaming - Column names use snake_case
	private sealed record SagaRecord(string state_json, bool is_completed);
	// ReSharper restore InconsistentNaming
}
