// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Configuration options for connection pool health monitoring, cleanup, and failure handling.
/// </summary>
public class ConnectionPoolHealthOptions
{
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
	/// Gets or sets a value indicating whether to enable health checking.
	/// </summary>
	/// <remarks>Health checking improves reliability but adds overhead. Default is true.</remarks>
	/// <value>The current <see cref="EnableHealthChecking"/> value.</value>
	public bool EnableHealthChecking { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed metrics collection.
	/// </summary>
	/// <remarks>Detailed metrics provide more insight but have higher overhead. Default is true.</remarks>
	/// <value>The current <see cref="EnableMetrics"/> value.</value>
	public bool EnableMetrics { get; set; } = true;

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
	/// Validates the health configuration and throws exceptions for invalid settings.
	/// </summary>
	/// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
	public virtual void Validate()
	{
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
	}

	/// <summary>
	/// Creates a copy of these health options with the same configuration.
	/// </summary>
	/// <returns>A new instance with identical configuration.</returns>
	public virtual ConnectionPoolHealthOptions Clone() =>
		new()
		{
			HealthCheckInterval = HealthCheckInterval,
			CleanupInterval = CleanupInterval,
			ValidateOnAcquisition = ValidateOnAcquisition,
			ValidateOnReturn = ValidateOnReturn,
			EnableHealthChecking = EnableHealthChecking,
			EnableMetrics = EnableMetrics,
			FailureHandling = FailureHandling,
			MaxRetryAttempts = MaxRetryAttempts,
			RetryDelay = RetryDelay,
		};
}
