// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a connection pool for managing reusable connections.
/// </summary>
/// <typeparam name="TConnection"> The type of connection to pool. </typeparam>
public interface IConnectionPool<TConnection> : IAsyncDisposable
	where TConnection : class
{
	/// <summary>
	/// Gets the name of this connection pool instance.
	/// </summary>
	/// <value>
	/// The name of this connection pool instance.
	/// </value>
	string Name { get; }

	/// <summary>
	/// Gets the type of connections managed by this pool.
	/// </summary>
	/// <value>
	/// The type of connections managed by this pool.
	/// </value>
	string ConnectionType { get; }

	/// <summary>
	/// Gets a value indicating whether this pool is available for use.
	/// </summary>
	/// <value>
	/// A value indicating whether this pool is available for use.
	/// </value>
	bool IsAvailable { get; }

	/// <summary>
	/// Acquires a connection from the pool asynchronously.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the acquisition operation. </param>
	/// <returns> A pooled connection handle that manages the connection lifecycle. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when the pool is not available. </exception>
	/// <exception cref="TimeoutException"> Thrown when connection acquisition times out. </exception>
	ValueTask<IPooledConnection<TConnection>> GetConnectionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Returns a connection to the pool for reuse.
	/// </summary>
	/// <param name="connection"> The connection to return. </param>
	/// <param name="forceDispose"> If true, forces disposal of the connection instead of pooling. </param>
	/// <returns> A task representing the asynchronous return operation. </returns>
	ValueTask ReturnConnectionAsync(TConnection connection, bool forceDispose = false);

	/// <summary>
	/// Gets current pool statistics synchronously.
	/// </summary>
	/// <returns> Current connection pool statistics. </returns>
	ConnectionPoolStatistics GetStatistics();

	/// <summary>
	/// Gets current pool statistics asynchronously.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> Current connection pool statistics. </returns>
	Task<ConnectionPoolStatistics> GetStatisticsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Warms up the connection pool with the specified number of connections.
	/// </summary>
	/// <param name="minConnections"> The minimum number of connections to create. </param>
	/// <param name="cancellationToken"> Token to cancel the warmup operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task WarmupAsync(int minConnections, CancellationToken cancellationToken);
}
