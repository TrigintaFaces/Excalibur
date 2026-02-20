// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions.Execution;

namespace Excalibur.Data.SqlServer.Execution;

/// <summary>
/// Initializes a new instance of the <see cref="SqlServerDataExecutor" /> class.
/// </summary>
/// <param name="connectionFactory"> </param>
public sealed class SqlServerDataExecutor(Func<IDbConnection> connectionFactory) : IDataExecutor
{
	private readonly Func<IDbConnection> _connectionFactory =
		connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

	/// <inheritdoc />
	public async Task<int> ExecuteAsync(
		string commandText,
		IReadOnlyDictionary<string, object?>? parameters,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(commandText);

		using var connection = _connectionFactory();
		if (connection.State != ConnectionState.Open)
		{
			connection.Open();
		}

		var dynParams = parameters is null ? null : new DynamicParameters(parameters);

		// Dapper does not accept CancellationToken directly on ResolveAsync; wrap with Task.Run if needed.
		return await connection.ExecuteAsync(new CommandDefinition(commandText, dynParams, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}
}
