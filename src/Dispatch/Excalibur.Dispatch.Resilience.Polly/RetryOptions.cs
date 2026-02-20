// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Configuration options for retry policy with jitter support.
/// </summary>
public sealed class RetryOptions
{
	/// <summary>
	/// Gets or sets maximum number of retry attempts.
	/// </summary>
	/// <value>The current <see cref="MaxRetries"/> value.</value>
	[Range(0, 100)]
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets base delay between retry attempts.
	/// </summary>
	/// <value>The baseline delay applied before backoff strategies.</value>
	public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets backoff strategy for retry delays.
	/// </summary>
	/// <value>The algorithm used to compute successive retry delays. Defaults to <see cref="BackoffStrategy.Exponential"/>.</value>
	public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;

	/// <summary>
	/// Gets or sets a value indicating whether to add jitter to retry delays.
	/// </summary>
	/// <value>The current <see cref="UseJitter"/> value.</value>
	public bool UseJitter { get; set; } = true;

	/// <summary>
	/// Gets or sets predicate to determine if an exception should trigger a retry.
	/// </summary>
	/// <value>The current <see cref="ShouldRetry"/> value.</value>
	public Func<Exception, bool>? ShouldRetry { get; set; }

	/// <summary>
	/// Gets or sets the jitter strategy to use.
	/// </summary>
	/// <value>The randomization model applied to delays. Defaults to <see cref="JitterStrategy.Equal"/>.</value>
	public JitterStrategy JitterStrategy { get; set; } = JitterStrategy.Equal;

	/// <summary>
	/// Gets or sets the jitter factor for controlling randomness (0.0 to 1.0).
	/// </summary>
	/// <value>A fractional multiplier that caps jitter magnitude. Defaults to 0.2.</value>
	[Range(0.0, 1.0)]
	public double JitterFactor { get; set; } = 0.2;

	/// <summary>
	/// Gets or sets maximum delay cap for any single retry.
	/// </summary>
	/// <value>The upper bound applied to any individual retry delay. Defaults to one minute.</value>
	public TimeSpan? MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the optional timeout for the entire operation including retries.
	/// </summary>
	/// <value>The maximum end-to-end duration enforced for the retry pipeline, or <see langword="null"/> to disable.</value>
	public TimeSpan? OperationTimeout { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to log detailed retry information.
	/// </summary>
	/// <value><see langword="true"/> to emit diagnostic log entries for each retry; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool EnableDetailedLogging { get; set; } = true;
}
