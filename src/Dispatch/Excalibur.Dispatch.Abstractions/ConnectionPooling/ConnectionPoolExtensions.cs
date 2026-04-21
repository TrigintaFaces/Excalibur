// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for <see cref="IConnectionPool{TConnection}"/>.
/// </summary>
public static class ConnectionPoolExtensions
{
	/// <summary>Gets current pool statistics synchronously.</summary>
	public static ConnectionPoolStatistics GetStatistics<TConnection>(this IConnectionPool<TConnection> pool) where TConnection : class
	{
		ArgumentNullException.ThrowIfNull(pool);
		if (pool is IConnectionPoolDiagnostics<TConnection> diag)
		{
			return diag.GetStatistics();
		}
		return new ConnectionPoolStatistics();
	}

	/// <summary>Gets current pool statistics asynchronously.</summary>
	public static Task<ConnectionPoolStatistics> GetStatisticsAsync<TConnection>(this IConnectionPool<TConnection> pool, CancellationToken cancellationToken) where TConnection : class
	{
		ArgumentNullException.ThrowIfNull(pool);
		if (pool is IConnectionPoolDiagnostics<TConnection> diag)
		{
			return diag.GetStatisticsAsync(cancellationToken);
		}
		return Task.FromResult(new ConnectionPoolStatistics());
	}

	/// <summary>Warms up the connection pool with the specified number of connections.</summary>
	public static Task WarmupAsync<TConnection>(this IConnectionPool<TConnection> pool, int minConnections, CancellationToken cancellationToken) where TConnection : class
	{
		ArgumentNullException.ThrowIfNull(pool);
		if (pool is IConnectionPoolDiagnostics<TConnection> diag)
		{
			return diag.WarmupAsync(minConnections, cancellationToken);
		}
		return Task.CompletedTask;
	}
}
