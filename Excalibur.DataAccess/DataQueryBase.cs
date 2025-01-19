using Dapper;

namespace Excalibur.DataAccess;

/// <summary>
///     Serves as the base class for implementing database queries with a specific connection type and return model.
/// </summary>
/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
/// <typeparam name="TModel"> The type of the model to be returned by the query. </typeparam>
public abstract class DataQueryBase<TConnection, TModel> : IDataQuery<TConnection, TModel>
{
	/// <inheritdoc />
	public CommandDefinition Command { get; protected init; }

	/// <inheritdoc />
	public DynamicParameters Parameters { get; set; } = new DynamicParameters();

	/// <inheritdoc />
	public Func<TConnection, Task<TModel>> Resolve { get; protected init; } = null!;

	/// <summary>
	///     Creates a command definition for the query.
	/// </summary>
	/// <param name="commandText"> The SQL command text. </param>
	/// <param name="parameters"> The parameters for the command (optional). </param>
	/// <param name="sqlTimeOutSeconds"> The command timeout in seconds. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A <see cref="CommandDefinition" /> instance representing the query. </returns>
	protected CommandDefinition CreateCommand(string commandText, DynamicParameters? parameters, int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		Parameters = parameters ?? new DynamicParameters();
		return new CommandDefinition(commandText, parameters: Parameters, commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);
	}
}
