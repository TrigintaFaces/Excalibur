// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions.Execution;

namespace Excalibur.Data.Postgres.Execution;

/// <summary>
/// Initializes a new instance of the <see cref="PostgresQueryExecutor"/> class.
/// </summary>
public sealed class PostgresQueryExecutor(Func<IDbConnection> connectionFactory) : IQueryExecutor
{
	private readonly Func<IDbConnection> _connectionFactory =
		connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

	/// <inheritdoc/>
	public IAsyncEnumerable<T> QueryAsync<T>(string queryText, IReadOnlyDictionary<string, object?>? parameters, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(queryText);

		return QueryAsync(queryText, parameters, cancellationToken);
		async IAsyncEnumerable<T> QueryAsync(string queryText, IReadOnlyDictionary<string, object?>? parameters, [System.Runtime.CompilerServices.EnumeratorCancellation]
		CancellationToken cancellationToken)
		{
			using var connection = _connectionFactory();
			if (connection.State != ConnectionState.Open)
			{
				connection.Open();
			}

			var dynParams = parameters is null ? null : new DynamicParameters(parameters);
			var rows = await connection.QueryAsync<T>(new CommandDefinition(queryText, dynParams, cancellationToken: cancellationToken))
				.ConfigureAwait(false);
			foreach (var row in rows)
			{
				cancellationToken.ThrowIfCancellationRequested();
				yield return row;
			}
		}
	}

	/// <inheritdoc/>
	public async Task<T?> QuerySingleAsync<T>(
		string queryText,
		IReadOnlyDictionary<string, object?>? parameters,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(queryText);

		using var connection = _connectionFactory();
		if (connection.State != ConnectionState.Open)
		{
			connection.Open();
		}

		var dynParams = parameters is null ? null : new DynamicParameters(parameters);
		var result = await connection
			.QuerySingleOrDefaultAsync<T>(new CommandDefinition(queryText, dynParams, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
		return result;
	}
}
