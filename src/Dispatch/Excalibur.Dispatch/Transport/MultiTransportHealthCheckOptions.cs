// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Configuration options for <see cref="MultiTransportHealthCheck"/>.
/// </summary>
public sealed class MultiTransportHealthCheckOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether at least one transport must be registered.
	/// </summary>
	/// <value>True to require at least one transport; false to allow empty registry. Default is false.</value>
	/// <remarks>
	/// When set to true, the health check will return Unhealthy if no transports are registered.
	/// </remarks>
	public bool RequireAtLeastOneTransport { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the default transport must be healthy.
	/// </summary>
	/// <value>True to require default transport to be healthy; false to allow degraded default. Default is true.</value>
	/// <remarks>
	/// When set to true and a default transport is configured, the health check will return
	/// Unhealthy if the default transport is not running or reports unhealthy status.
	/// </remarks>
	public bool RequireDefaultTransportHealthy { get; set; } = true;

	/// <summary>
	/// Gets or sets the timeout for individual transport health checks.
	/// </summary>
	/// <value>The timeout duration. Default is 5 seconds.</value>
	public TimeSpan TransportCheckTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets a value indicating whether to run transport checks in parallel.
	/// </summary>
	/// <value>True to run checks in parallel; false to run sequentially. Default is true.</value>
	/// <remarks>
	/// Parallel execution is faster but may cause resource contention in some scenarios.
	/// </remarks>
	public bool ParallelChecks { get; set; } = true;
}
