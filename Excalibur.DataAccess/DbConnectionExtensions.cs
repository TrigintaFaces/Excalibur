// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using System.Data;
using System.Diagnostics;

using Excalibur.Core.Exceptions;
using Excalibur.DataAccess.Exceptions;

using Microsoft.Data.SqlClient;

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
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(dataRequest);

		try
		{
			return await dataRequest.ResolveAsync(connection.Ready()).ConfigureAwait(false);
		}
		catch (SqlException sqlEx) when (sqlEx.Number is 596 or -2)
		{
			TryClose(connection);
			throw;
		}
		catch (TimeoutException)
		{
			TryClose(connection);
			throw;
		}
		catch (Exception ex) when (ex is not ApiException)
		{
			throw new OperationFailedException(
				TypeNameHelper.GetTypeDisplayName(typeof(TModel), false, true),
				TypeNameHelper.GetTypeDisplayName(dataRequest.GetType(), false, true),
				innerException: ex);
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

		if (connection.State is ConnectionState.Closed or ConnectionState.Broken)
		{
			try
			{
				if (connection.State == ConnectionState.Broken)
				{
					connection.Close();
				}

				connection.Open();
			}
			catch (ObjectDisposedException)
			{
				throw new InvalidOperationException("The database connection has been disposed. Ensure connections are properly managed.");
			}
		}

		return connection;
	}

	private static void TryClose(IDbConnection connection)
	{
		try
		{
			connection.Close();
		}
		catch
		{
			// ignored
		}
	}
}
