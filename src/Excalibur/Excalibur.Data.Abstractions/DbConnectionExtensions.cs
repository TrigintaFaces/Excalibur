// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Abstractions;

/// <summary>
/// Provides extension methods for <see cref="IDbConnection" /> to enhance connection management and request execution.
/// </summary>
public static class DbConnectionExtensions
{
	/// <summary>
	/// Executes a data request asynchronously on the specified database connection.
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
				TypeNameHelper.GetTypeDisplayName(typeof(TModel), fullName: false, includeGenericParameterNames: true),
				TypeNameHelper.GetTypeDisplayName(dataRequest.GetType(), fullName: false, includeGenericParameterNames: true),
				innerException: ex);
		}
	}

	/// <summary>
	/// Ensures the database connection is in a usable state, reopening it if necessary.
	/// </summary>
	/// <param name="connection"> The database connection to check and prepare. </param>
	/// <returns> The prepared <see cref="IDbConnection" /> instance. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown if the connection state is invalid or unrecognized. </exception>
	/// <exception cref="InvalidOperationException"></exception>
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
				throw new InvalidOperationException("The connection has been disposed and cannot be used.");
			}
		}

		return connection;
	}
}
