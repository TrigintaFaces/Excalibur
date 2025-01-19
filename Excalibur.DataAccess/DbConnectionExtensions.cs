using System.Data;
using System.Diagnostics;

using Excalibur.DataAccess.Exceptions;
using Excalibur.Exceptions;

namespace Excalibur.DataAccess;

/// <summary>
///     Provides extension methods for <see cref="IDbConnection" /> to enhance connection management and query execution.
/// </summary>
public static class DbConnectionExtensions
{
	/// <summary>
	///     Executes a data query asynchronously on the specified database connection.
	/// </summary>
	/// <typeparam name="TModel"> The type of the result model. </typeparam>
	/// <param name="connection"> The database connection to use for the query. </param>
	/// <param name="dataQuery"> The query to execute, including the command and resolver. </param>
	/// <returns> The result of the query execution as an instance of <typeparamref name="TModel" />. </returns>
	/// <exception cref="OperationFailedException"> Thrown if an error occurs while executing the query. </exception>
	public static async Task<TModel> QueryAsync<TModel>(this IDbConnection connection, IDataQuery<IDbConnection, TModel> dataQuery)
	{
		ArgumentNullException.ThrowIfNull(dataQuery);

		try
		{
			return await dataQuery.Resolve(connection).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not ApiException)
		{
			throw new OperationFailedException(
				TypeNameHelper.GetTypeDisplayName(typeof(TModel), false, true),
				TypeNameHelper.GetTypeDisplayName(dataQuery.GetType(), false, true),
				innerException: ex
			);
		}
	}

	/// <summary>
	///     Ensures the database connection is in a usable state, reopening it if necessary.
	/// </summary>
	/// <param name="connection"> The database connection to check and prepare. </param>
	/// <returns> The prepared <see cref="IDbConnection" /> instance. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown if the connection state is invalid or unrecognized. </exception>
	public static IDbConnection Ready(this IDbConnection connection)
	{
		ArgumentNullException.ThrowIfNull(connection);

		switch (connection.State)
		{
			case ConnectionState.Broken:
				connection.Close();
				connection.Open();
				break;

			case ConnectionState.Closed:
				connection.Open();
				break;

			case ConnectionState.Open:
			case ConnectionState.Connecting:
			case ConnectionState.Executing:
			case ConnectionState.Fetching:
				break;

			default:
				throw new ArgumentOutOfRangeException(
					nameof(connection),
					$"The connection state '{connection.State}' for the provided connection is not valid or recognized."
				);
		}

		return connection;
	}
}
