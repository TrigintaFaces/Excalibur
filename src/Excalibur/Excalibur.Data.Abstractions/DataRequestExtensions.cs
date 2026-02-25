// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions;

/// <summary>
/// Extension methods for IDataRequest.
/// </summary>
public static class DataRequestExtensions
{
	/// <summary>
	/// Executes the data request with the provided connection.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
	/// <typeparam name="TModel"> The type of the model to be returned by the request. </typeparam>
	/// <param name="request"> The data request to execute. </param>
	/// <param name="connection"> The database connection. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the data request execution. </returns>
	public static async Task<TModel> ResolveAsync<TConnection, TModel>(
		this IDataRequest<TConnection, TModel> request,
		TConnection connection,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(connection);
		_ = cancellationToken; // Parameter required by interface signature but not used in current implementation

		return await request.ResolveAsync(connection).ConfigureAwait(false);
	}

	/// <summary>
	/// Executes the document data request with the provided connection.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
	/// <typeparam name="TModel"> The type of the model to be returned by the request. </typeparam>
	/// <param name="request"> The document data request to execute. </param>
	/// <param name="connection"> The database connection. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the document data request execution. </returns>
	public static async Task<TModel> ResolveAsync<TConnection, TModel>(
		this IDocumentDataRequest<TConnection, TModel> request,
		TConnection connection,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(connection);
		_ = cancellationToken; // Parameter required by interface signature but not used in current implementation

		return await request.ResolveAsync(connection).ConfigureAwait(false);
	}
}
