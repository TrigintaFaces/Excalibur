using Dapper;

namespace Excalibur.DataAccess;

/// <summary>
///     Represents a contract for database requests that define the command and parameters necessary for execution.
/// </summary>
/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
/// <typeparam name="TModel"> The type of the model to be returned by the request. </typeparam>
public interface IDataRequest<in TConnection, TModel>
{
	/// <summary>
	///     Gets the command definition used for executing the request.
	/// </summary>
	public CommandDefinition Command { get; }

	/// <summary>
	///     Gets or sets the parameters associated with the request.
	/// </summary>
	public DynamicParameters Parameters { get; set; }

	/// <summary>
	///     Gets the function responsible for resolving the request result using the provided connection.
	/// </summary>
	public Func<TConnection, Task<TModel>> ResolveAsync { get; }
}
