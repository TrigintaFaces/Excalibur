// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Configuration options for connection pools. Consolidates options from all pool implementations.
/// </summary>
public class ConnectionPoolOptions
{
	/// <summary>
	/// Default pool name used when none is specified.
	/// </summary>
	public const string DefaultPoolName = "DefaultPool";

	/// <summary>
	/// Gets or sets the name of the connection pool.
	/// </summary>
	/// <value>The current <see cref="PoolName"/> value.</value>
	public string PoolName { get; set; } = DefaultPoolName;

	/// <summary>
	/// Gets or sets the connection string for creating connections.
	/// </summary>
	/// <value>The current <see cref="ConnectionString"/> value.</value>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets additional properties for provider-specific configuration.
	/// </summary>
	/// <value>The current <see cref="Properties"/> value.</value>
	public Dictionary<string, object> Properties { get; init; } = [];

	/// <summary>
	/// Gets or sets the sizing and capacity configuration for the connection pool.
	/// </summary>
	/// <value>The current <see cref="Sizing"/> value.</value>
	public ConnectionPoolSizingOptions Sizing { get; set; } = new();

	/// <summary>
	/// Gets or sets the health monitoring, cleanup, and failure handling configuration for the connection pool.
	/// </summary>
	/// <value>The current <see cref="Health"/> value.</value>
	public ConnectionPoolHealthOptions Health { get; set; } = new();

	/// <summary>
	/// Validates the configuration and throws exceptions for invalid settings.
	/// </summary>
	/// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
	public virtual void Validate()
	{
		if (string.IsNullOrWhiteSpace(PoolName))
		{
			throw new ArgumentException(ErrorMessages.PoolNameCannotBeNullOrEmpty, nameof(PoolName));
		}

		Sizing.Validate();
		Health.Validate();
	}

	/// <summary>
	/// Creates a copy of these options with the same configuration.
	/// </summary>
	/// <returns>A new instance with identical configuration.</returns>
	public virtual ConnectionPoolOptions Clone() =>
		new()
		{
			PoolName = PoolName,
			ConnectionString = ConnectionString,
			Properties = new Dictionary<string, object>(Properties, StringComparer.Ordinal),
			Sizing = Sizing.Clone(),
			Health = Health.Clone(),
		};
}
