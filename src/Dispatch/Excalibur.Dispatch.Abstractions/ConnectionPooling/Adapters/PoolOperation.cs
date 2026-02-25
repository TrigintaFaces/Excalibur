// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents operations that may or may not be supported by all connection pool implementations.
/// </summary>
public enum PoolOperation
{
	/// <summary>
	/// Getting pool statistics.
	/// </summary>
	Statistics = 0,

	/// <summary>
	/// Warming up the pool with initial connections.
	/// </summary>
	Warmup = 1,

	/// <summary>
	/// Getting health status.
	/// </summary>
	HealthCheck = 2,

	/// <summary>
	/// Clearing all connections from the pool.
	/// </summary>
	Clear = 3,

	/// <summary>
	/// Asynchronous disposal of connections.
	/// </summary>
	AsyncDisposal = 4,
}
