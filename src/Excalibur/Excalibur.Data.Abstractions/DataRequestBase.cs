// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;
using System.Data.Common;

using Dapper;

namespace Excalibur.Data;

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

		Parameters = parameters ?? Parameters;

		// Thread the cancellation token into the command so every DataRequest observes cancellation
		// (Dapper honors CommandDefinition.CancellationToken on its async execution paths).
		return new CommandDefinition(
			commandText,
			Parameters,
			transaction,
			commandTimeout,
			commandType,
			CommandFlags.Buffered,
			cancellationToken);
	}
	/// <summary>Executes this request without runtime parameter discovery or generated command initialization.</summary>
	/// <param name="connection">The existing provider connection.</param>
	/// <param name="bind">Binds the operation's known parameters from their current request values.</param>
	/// <returns>The affected row count.</returns>
	/// <remarks>The request's command text, transaction, timeout, command type and cancellation token are preserved.</remarks>
	protected Task<int> ExecuteNonQueryAsync(IDbConnection connection, Action<DbCommand> bind)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(bind);
		return StaticDbCommand.ExecuteAsync(connection, Command, bind);
	}

	/// <summary>Reads this request's scalar result without runtime parameter discovery.</summary>
	/// <param name="connection">The existing provider connection.</param>
	/// <param name="bind">Binds the operation's known parameters from their current request values.</param>
	/// <returns>The provider's first-column scalar value.</returns>
	protected Task<object?> ExecuteScalarAsync(IDbConnection connection, Action<DbCommand> bind)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(bind);
		return StaticDbCommand.ScalarAsync(connection, Command, bind);
	}

	/// <summary>Buffers rows with an explicit ordinal projection instead of runtime row mapping.</summary>
	/// <typeparam name="TRow">The operation's statically known row type.</typeparam>
	/// <param name="connection">The existing provider connection.</param>
	/// <param name="bind">Binds the operation's known parameters from their current request values.</param>
	/// <param name="materialize">Reads one current row using the operation's fixed projection.</param>
	/// <returns>The fully buffered rows after reader cleanup.</returns>
	/// <remarks>A borrowed open connection and transaction remain caller-owned. A connection opened for this request is closed afterward.</remarks>
	protected Task<IEnumerable<TRow>> ReadRowsAsync<TRow>(IDbConnection connection, Action<DbCommand> bind,
		Func<DbDataReader, TRow> materialize)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(bind);
		ArgumentNullException.ThrowIfNull(materialize);
		return StaticDbCommand.QueryAsync(connection, Command, bind, materialize);
	}

}
