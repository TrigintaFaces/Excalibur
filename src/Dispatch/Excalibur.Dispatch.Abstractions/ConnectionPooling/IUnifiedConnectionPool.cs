// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a unified connection pool for managing reusable connections across all providers. This interface consolidates the 4 incompatible
/// IConnectionPool variants found in the codebase.
/// </summary>
/// <typeparam name="TConnection"> The type of connection to pool. </typeparam>
public interface IUnifiedConnectionPool<TConnection> : IAsyncDisposable, IDisposable
	where TConnection : class
{
	/// <summary>
	/// Gets a connection from the pool asynchronously.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A connection from the pool. </returns>
	ValueTask<TConnection> GetConnectionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Returns a connection to the pool asynchronously.
	/// </summary>
	/// <param name="connection"> The connection to return. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	ValueTask ReturnConnectionAsync(TConnection connection,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current statistics of the connection pool.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The pool statistics. </returns>
	ValueTask<ConnectionPoolStatistics> GetStatisticsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Warms up the connection pool with the specified number of connections.
	/// </summary>
	/// <param name="minConnections"> The minimum number of connections to create. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	ValueTask WarmupAsync(int minConnections, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current health status of the connection pool.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The health status of the pool. </returns>
	ValueTask<PoolHealthStatus> GetHealthAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Clears all connections from the pool.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	ValueTask ClearAsync(CancellationToken cancellationToken);
}
