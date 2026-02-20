// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Configuration options for the unified connection pool.
/// </summary>
/// <typeparam name="TConnection"> The type of connection to pool. </typeparam>
public sealed class UnifiedConnectionPoolOptions<TConnection>
	where TConnection : class
{
	/// <summary>
	/// Gets or sets the minimum number of connections to maintain in the pool.
	/// </summary>
	/// <value> The minimum connection count. </value>
	public int MinConnections { get; set; } = 1;

	/// <summary>
	/// Gets or sets the maximum number of connections allowed in the pool.
	/// </summary>
	/// <value> The maximum connection count. </value>
	public int MaxConnections { get; set; } = 10;

	/// <summary>
	/// Gets or sets the connection timeout duration.
	/// </summary>
	/// <value> The connection timeout duration. </value>
	public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the idle connection timeout duration.
	/// </summary>
	/// <value> The idle timeout duration. </value>
	public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets a value indicating whether to validate connections on checkout.
	/// </summary>
	/// <value> <see langword="true" /> when validation is performed; otherwise, <see langword="false" />. </value>
	public bool ValidateOnCheckout { get; set; } = true;

	/// <summary>
	/// Gets or sets the health check interval.
	/// </summary>
	/// <value> The interval between health checks. </value>
	public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets the connection-specific configurations.
	/// </summary>
	/// <value> The dictionary of connection configurations. </value>
	public IDictionary<string, ConnectionConfiguration<TConnection>> ConnectionConfigurations { get; } =
		new Dictionary<string, ConnectionConfiguration<TConnection>>(StringComparer.Ordinal);
}
