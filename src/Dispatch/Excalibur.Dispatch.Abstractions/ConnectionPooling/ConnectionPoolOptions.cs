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
	/// Gets or sets how often to run health checks on pooled connections.
	/// </summary>
	/// <remarks>More frequent health checks improve reliability but increase overhead. Default is 1 minute.</remarks>
	/// <value>How often to run health checks on pooled connections.</value>
	public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets how often to run cleanup operations on the pool.
	/// </summary>
	/// <remarks>Cleanup operations remove expired and unhealthy connections. Default is 30 seconds.</remarks>
	/// <value>How often to run cleanup operations on the pool.</value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to pre-warm the pool with minimum connections.
	/// </summary>
	/// <remarks>Pre-warming reduces initial latency but increases startup time. Default is true.</remarks>
	/// <value>The current <see cref="PreWarmConnections"/> value.</value>
	public bool PreWarmConnections { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable connection validation on acquisition.
	/// </summary>
	/// <remarks>Validation ensures connections are healthy but adds overhead. Default is true.</remarks>
	/// <value>The current <see cref="ValidateOnAcquisition"/> value.</value>
	public bool ValidateOnAcquisition { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable connection validation on return.
	/// </summary>
	/// <remarks>This helps detect connections that became unhealthy during use. Default is false for performance.</remarks>
	/// <value>The current <see cref="ValidateOnReturn"/> value.</value>
	public bool ValidateOnReturn { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed metrics collection.
	/// </summary>
	/// <remarks>Detailed metrics provide more insight but have higher overhead. Default is true.</remarks>
	/// <value>The current <see cref="EnableMetrics"/> value.</value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable health checking.
	/// </summary>
	/// <remarks>Health checking improves reliability but adds overhead. Default is true.</remarks>
	/// <value>The current <see cref="EnableHealthChecking"/> value.</value>
	public bool EnableHealthChecking { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of times a connection can be reused before being disposed.
	/// </summary>
	/// <remarks>Limits connection reuse to prevent resource degradation. Default is 1000.</remarks>
	/// <value>The current <see cref="MaxConnectionUseCount"/> value.</value>
	public int MaxConnectionUseCount { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the strategy for handling connection acquisition failures.
	/// </summary>
	/// <value>The current <see cref="FailureHandling"/> value.</value>
	public FailureHandlingStrategy FailureHandling { get; set; } = FailureHandlingStrategy.RetryThenFail;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for connection operations.
	/// </summary>
	/// <value>The current <see cref="MaxRetryAttempts"/> value.</value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retry attempts.
	/// </summary>
	/// <remarks>Actual delay may include jitter and exponential backoff. Default is 100 milliseconds.</remarks>
	/// <value>The base delay between retry attempts.</value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets additional properties for provider-specific configuration.
	/// </summary>
	/// <value>The current <see cref="Properties"/> value.</value>
	public Dictionary<string, object> Properties { get; init; } = [];

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

		if (HealthCheckInterval <= TimeSpan.Zero)
		{
			throw new ArgumentException(ErrorMessages.HealthCheckIntervalMustBePositive, nameof(HealthCheckInterval));
		}

		if (CleanupInterval <= TimeSpan.Zero)
		{
			throw new ArgumentException(ErrorMessages.CleanupIntervalMustBePositive, nameof(CleanupInterval));
		}

		if (MaxRetryAttempts < 0)
		{
			throw new ArgumentException(ErrorMessages.MaximumRetryAttemptsCannotBeNegative, nameof(MaxRetryAttempts));
		}

		if (RetryDelay < TimeSpan.Zero)
		{
			throw new ArgumentException(ErrorMessages.RetryDelayCannotBeNegative, nameof(RetryDelay));
		}

		if (MaxConnectionUseCount <= 0)
		{
			throw new ArgumentException(ErrorMessages.MaximumConnectionUseCountMustBePositive, nameof(MaxConnectionUseCount));
		}
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
			MinConnections = MinConnections,
			MaxConnections = MaxConnections,
			AcquisitionTimeout = AcquisitionTimeout,
			IdleTimeout = IdleTimeout,
			MaxConnectionLifetime = MaxConnectionLifetime,
			HealthCheckInterval = HealthCheckInterval,
			CleanupInterval = CleanupInterval,
			PreWarmConnections = PreWarmConnections,
			ValidateOnAcquisition = ValidateOnAcquisition,
			ValidateOnReturn = ValidateOnReturn,
			EnableMetrics = EnableMetrics,
			EnableHealthChecking = EnableHealthChecking,
			MaxConnectionUseCount = MaxConnectionUseCount,
			FailureHandling = FailureHandling,
			MaxRetryAttempts = MaxRetryAttempts,
			RetryDelay = RetryDelay,
			Properties = new Dictionary<string, object>(Properties, StringComparer.Ordinal),
		};
}
