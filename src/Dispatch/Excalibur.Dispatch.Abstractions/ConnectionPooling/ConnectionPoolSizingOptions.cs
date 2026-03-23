// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Configuration options for connection pool sizing and capacity management.
/// </summary>
public sealed class ConnectionPoolSizingOptions
{
	/// <summary>
	/// Gets or sets the minimum number of connections to maintain in the pool.
	/// </summary>
	/// <remarks>The pool will attempt to maintain at least this many connections at all times. Default is 0 (no minimum).</remarks>
	/// <value>The current <see cref="MinConnections"/> value.</value>
	public int MinConnections { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of connections allowed in the pool.
	/// </summary>
	/// <remarks>When this limit is reached, further connection requests will wait or fail. Default is 100.</remarks>
	/// <value>The current <see cref="MaxConnections"/> value.</value>
	public int MaxConnections { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum number of times a connection can be reused before being disposed.
	/// </summary>
	/// <remarks>Limits connection reuse to prevent resource degradation. Default is 1000.</remarks>
	/// <value>The current <see cref="MaxConnectionUseCount"/> value.</value>
	public int MaxConnectionUseCount { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to pre-warm the pool with minimum connections.
	/// </summary>
	/// <remarks>Pre-warming reduces initial latency but increases startup time. Default is true.</remarks>
	/// <value>The current <see cref="PreWarmConnections"/> value.</value>
	public bool PreWarmConnections { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum time to wait for a connection to become available.
	/// </summary>
	/// <remarks>If a connection cannot be acquired within this time, a timeout exception is thrown. Default is 30 seconds.</remarks>
	/// <value>The maximum time to wait for a connection to become available.</value>
	public TimeSpan AcquisitionTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the maximum amount of time a connection can remain idle before being disposed.
	/// </summary>
	/// <remarks>Idle connections are periodically cleaned up to free resources. Default is 5 minutes.</remarks>
	/// <value>The maximum amount of time a connection can remain idle before being disposed.</value>
	public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the maximum lifetime of a connection before it's forcibly disposed.
	/// </summary>
	/// <remarks>This prevents connections from becoming stale over long periods. Default is 30 minutes.</remarks>
	/// <value>The maximum lifetime of a connection before it's forcibly disposed.</value>
	public TimeSpan MaxConnectionLifetime { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>
	/// Validates the sizing configuration and throws exceptions for invalid settings.
	/// </summary>
	/// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
	public void Validate()
	{
		if (MinConnections < 0)
		{
			throw new ArgumentException(ErrorMessages.MinimumConnectionsCannotBeNegative, nameof(MinConnections));
		}

		if (MaxConnections <= 0)
		{
			throw new ArgumentException(ErrorMessages.MaximumConnectionsMustBePositive, nameof(MaxConnections));
		}

		if (MinConnections > MaxConnections)
		{
			throw new ArgumentException(ErrorMessages.MinimumConnectionsCannotExceedMaximumConnections);
		}

		if (AcquisitionTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException(ErrorMessages.AcquisitionTimeoutMustBePositive, nameof(AcquisitionTimeout));
		}

		if (IdleTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException(ErrorMessages.IdleTimeoutMustBePositive, nameof(IdleTimeout));
		}

		if (MaxConnectionLifetime <= TimeSpan.Zero)
		{
			throw new ArgumentException(ErrorMessages.MaximumConnectionLifetimeMustBePositive, nameof(MaxConnectionLifetime));
		}

		if (MaxConnectionUseCount <= 0)
		{
			throw new ArgumentException(ErrorMessages.MaximumConnectionUseCountMustBePositive, nameof(MaxConnectionUseCount));
		}
	}

	/// <summary>
	/// Creates a copy of these sizing options with the same configuration.
	/// </summary>
	/// <returns>A new instance with identical configuration.</returns>
	public ConnectionPoolSizingOptions Clone() =>
		new()
		{
			MinConnections = MinConnections,
			MaxConnections = MaxConnections,
			MaxConnectionUseCount = MaxConnectionUseCount,
			PreWarmConnections = PreWarmConnections,
			AcquisitionTimeout = AcquisitionTimeout,
			IdleTimeout = IdleTimeout,
			MaxConnectionLifetime = MaxConnectionLifetime,
		};
}
