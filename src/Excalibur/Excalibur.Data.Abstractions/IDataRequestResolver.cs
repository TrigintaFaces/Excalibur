// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions;

/// <summary>
/// Lightweight resolver for executing <see cref="IDataRequest{TConnection, TModel}"/> instances
/// with automatic connection lifecycle management.
/// </summary>
/// <typeparam name="TConnection">The connection type (e.g., <c>SqlConnection</c>).</typeparam>
/// <remarks>
/// <para>
/// Use this interface for simple, focused data access scenarios such as CQRS read-side queries,
/// ad-hoc reporting, or serverless functions that need to run a single query without standing up
/// a full <see cref="Persistence.IPersistenceProvider"/>.
/// </para>
/// <para>
/// Method naming follows the Dapper convention:
/// <list type="bullet">
///   <item><see cref="QueryAsync{TModel}"/> — returns a result (SELECT, scalar)</item>
///   <item><see cref="ExecuteAsync"/> — performs a side effect (INSERT, UPDATE, DELETE)</item>
/// </list>
/// </para>
/// </remarks>
public interface IDataRequestResolver<TConnection>
{
	/// <summary>
	/// Executes a data request that returns a result.
	/// </summary>
	/// <typeparam name="TModel">The type of the result model.</typeparam>
	/// <param name="request">The data request to execute.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the data request execution.</returns>
	Task<TModel> QueryAsync<TModel>(
		IDataRequest<TConnection, TModel> request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes a data request that performs a side effect without returning a meaningful result.
	/// </summary>
	/// <param name="request">The data request to execute.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task ExecuteAsync(
		IDataRequest<TConnection, int> request,
		CancellationToken cancellationToken);
}
