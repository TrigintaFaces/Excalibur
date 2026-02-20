// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

namespace Excalibur.Data.Abstractions;

/// <summary>
/// Serves as the base class for implementing database requests with a specific connection type and return model.
/// </summary>
/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
/// <typeparam name="TModel"> The type of the model to be returned by the request. </typeparam>
public abstract class DataRequestBase<TConnection, TModel> : IDataRequest<TConnection, TModel>
{
	/// <inheritdoc />
	public string RequestId { get; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public string RequestType => GetType().Name;

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public string? CorrelationId { get; set; }

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; set; }

	/// <inheritdoc />
	public CommandDefinition Command { get; protected init; }

	/// <inheritdoc />
	public DynamicParameters Parameters { get; internal set; } = new();

	/// <inheritdoc />
	public Func<TConnection, Task<TModel>> ResolveAsync { get; protected init; } = null!;

	/// <summary>
	/// Creates a command definition for the request.
	/// </summary>
	/// <param name="commandText"> The SQL command text. </param>
	/// <param name="parameters"> The parameters for the command (optional). </param>
	/// <param name="transaction"> The transaction for this command to participate in. </param>
	/// <param name="commandTimeout"> The timeout (in seconds) for this command. </param>
	/// <param name="commandType"> The <see cref="CommandType" /> for this command. </param>
	/// <param name="cancellationToken"> The cancellation token for this command. </param>
	/// <returns> A <see cref="CommandDefinition" /> instance representing the request. </returns>
	protected CommandDefinition CreateCommand(
		string commandText,
		DynamicParameters? parameters = null,
		IDbTransaction? transaction = null,
		int? commandTimeout = null,
		CommandType? commandType = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
		_ = cancellationToken; // Parameter reserved for future async implementation

		Parameters = parameters ?? Parameters;

		return new CommandDefinition(commandText, Parameters, transaction, commandTimeout, commandType);
	}
}
