using System.Data;

using Dapper;

namespace Excalibur.DataAccess;

/// <summary>
///     Serves as the base class for implementing database requests with a specific connection type and return model.
/// </summary>
/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
/// <typeparam name="TModel"> The type of the model to be returned by the request. </typeparam>
public abstract class DataRequestBase<TConnection, TModel> : IDataRequest<TConnection, TModel>
{
	/// <inheritdoc />
	public CommandDefinition Command { get; protected init; }

	/// <inheritdoc />
	public DynamicParameters Parameters { get; set; } = new DynamicParameters();

	/// <inheritdoc />
	public Func<TConnection, Task<TModel>> ResolveAsync { get; protected init; } = null!;

	/// <summary>
	///     Creates a command definition for the request.
	/// </summary>
	/// <param name="commandText"> The SQL command text. </param>
	/// <param name="parameters"> The parameters for the command (optional). </param>
	/// <param name="transaction"> The transaction for this command to participate in. </param>
	/// <param name="commandTimeout"> The timeout (in seconds) for this command. </param>
	/// <param name="commandType"> The <see cref="CommandType" /> for this command. </param>
	/// <param name="flags"> The behavior flags for this command. </param>
	/// <param name="cancellationToken"> The cancellation token for this command. </param>
	/// <returns> A <see cref="CommandDefinition" /> instance representing the request. </returns>
	protected CommandDefinition CreateCommand(string commandText, DynamicParameters? parameters, IDbTransaction? transaction = null,
		int? commandTimeout = null,
		CommandType? commandType = null, CommandFlags flags = CommandFlags.Buffered, CancellationToken cancellationToken = default)
	{
		Parameters = parameters ?? new DynamicParameters();
		return new CommandDefinition(commandText, parameters: Parameters, transaction, commandTimeout, commandType, flags,
			cancellationToken);
	}
}
