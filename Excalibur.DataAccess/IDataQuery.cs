using Dapper;

namespace Excalibur.DataAccess;

/// <summary>
///     Represents a contract for database queries that define the command and parameters necessary for execution.
/// </summary>
/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
/// <typeparam name="TModel"> The type of the model to be returned by the query. </typeparam>
public interface IDataQuery<in TConnection, TModel>
{
	/// <summary>
	///     Gets the command definition used for executing the query.
	/// </summary>
	CommandDefinition Command { get; }

	/// <summary>
	///     Gets or sets the parameters associated with the query.
	/// </summary>
	DynamicParameters Parameters { get; set; }

	/// <summary>
	///     Gets the function responsible for resolving the query result using the provided connection.
	/// </summary>
	Func<TConnection, Task<TModel>> Resolve { get; }
}
