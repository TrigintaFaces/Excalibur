// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Retry policy configuration options.
/// </summary>
public sealed class RetryPolicyOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>The current <see cref="MaxRetryAttempts"/> value.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retries in milliseconds.
	/// </summary>
	/// <value>The current <see cref="BaseDelayMs"/> value.</value>
	[Range(1, int.MaxValue)]
	public int BaseDelayMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum delay between retries in milliseconds.
	/// </summary>
	/// <value>The current <see cref="MaxDelayMs"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxDelayMs { get; set; } = 30000;

	/// <summary>
	/// Gets or sets a value indicating whether to use exponential backoff.
	/// </summary>
	/// <value>The current <see cref="UseExponentialBackoff"/> value.</value>
	public bool UseExponentialBackoff { get; set; } = true;
}
