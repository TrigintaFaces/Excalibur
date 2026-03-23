// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Configuration for the default dispatch retry policy.
/// </summary>
public sealed class DispatchRetryOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>The current <see cref="MaxAttempts"/> value.</value>
	[Range(0, 100)]
	public int MaxAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the initial delay between retries.
	/// </summary>
	/// <value>The initial delay between retries.</value>
	public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum delay between retries.
	/// </summary>
	/// <value>The maximum delay between retries.</value>
	public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the backoff multiplier.
	/// </summary>
	/// <value>The current <see cref="BackoffMultiplier"/> value.</value>
	[Range(1.0, 100.0)]
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
