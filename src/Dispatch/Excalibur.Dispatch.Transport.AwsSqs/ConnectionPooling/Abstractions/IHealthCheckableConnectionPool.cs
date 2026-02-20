// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Extended connection pool interface with health checking capabilities.
/// </summary>
/// <typeparam name="TConnection"> The type of connection to pool. </typeparam>
public interface IHealthCheckableConnectionPool<TConnection> : IConnectionPool<TConnection>
	where TConnection : class
{
	/// <summary>
	/// Performs a health check on all connections in the pool.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> The health check result. </returns>
	Task<ConnectionPoolHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken);
}
