// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Retry policy for batch processing.
/// </summary>
public sealed class RetryPolicy
{
	/// <summary>
	/// Gets or sets the maximum number of retries.
	/// </summary>
	/// <value>The current <see cref="MaxRetries"/> value.</value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the initial retry delay.
	/// </summary>
	/// <value>
	/// The initial retry delay.
	/// </value>
	public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum retry delay.
	/// </summary>
	/// <value>
	/// The maximum retry delay.
	/// </value>
	public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the backoff multiplier.
	/// </summary>
	/// <value>The current <see cref="BackoffMultiplier"/> value.</value>
	public double BackoffMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets a value indicating whether to use exponential backoff.
	/// </summary>
	/// <value>The current <see cref="UseExponentialBackoff"/> value.</value>
	public bool UseExponentialBackoff { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to add jitter to retry delays.
	/// </summary>
	/// <value>The current <see cref="UseJitter"/> value.</value>
	public bool UseJitter { get; set; } = true;
}
