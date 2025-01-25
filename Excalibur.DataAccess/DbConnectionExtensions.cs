using System.Data;
using System.Diagnostics;

using Excalibur.Core.Exceptions;
using Excalibur.DataAccess.Exceptions;

namespace Excalibur.DataAccess;

/// <summary>
///     Provides extension methods for <see cref="IDbConnection" /> to enhance connection management and request execution.
/// </summary>
public static class DbConnectionExtensions
{
	/// <summary>
	///     Executes a data request asynchronously on the specified database connection.
	/// </summary>
	/// <typeparam name="TModel"> The type of the result model. </typeparam>
	/// <param name="connection"> The database connection to use for the request. </param>
	/// <param name="dataRequest"> The request to execute, including the command and resolver. </param>
	/// <returns> The result of the request execution as an instance of <typeparamref name="TModel" />. </returns>
	/// <exception cref="OperationFailedException"> Thrown if an error occurs while executing the request. </exception>
	public static async Task<TModel> ResolveAsync<TModel>(this IDbConnection connection, IDataRequest<IDbConnection, TModel> dataRequest)
	{
		ArgumentNullException.ThrowIfNull(dataRequest);

		try
		{
			return await dataRequest.ResolveAsync(connection).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not ApiException)
		{
			throw new OperationFailedException(
				TypeNameHelper.GetTypeDisplayName(typeof(TModel), false, true),
				TypeNameHelper.GetTypeDisplayName(dataRequest.GetType(), false, true),
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
