// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides diagnostic and lifecycle operations for a connection pool.
/// Implementations that support these operations should implement this interface
/// alongside <see cref="IConnectionPool{TConnection}"/>.
/// </summary>
/// <typeparam name="TConnection">The type of connection managed by the pool.</typeparam>
public interface IConnectionPoolDiagnostics<TConnection>
	where TConnection : class
{
	/// <summary>
	/// Gets current pool statistics synchronously.
	/// </summary>
	/// <returns>A <see cref="ConnectionPoolStatistics"/> snapshot of the pool state.</returns>
	ConnectionPoolStatistics GetStatistics();

	/// <summary>
	/// Gets current pool statistics asynchronously.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A <see cref="ConnectionPoolStatistics"/> snapshot of the pool state.</returns>
	Task<ConnectionPoolStatistics> GetStatisticsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Warms up the connection pool with the specified number of connections.
	/// </summary>
	/// <param name="minConnections">The minimum number of connections to establish.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that completes when the warmup is finished.</returns>
	Task WarmupAsync(int minConnections, CancellationToken cancellationToken);
}
