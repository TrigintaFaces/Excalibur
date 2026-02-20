// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Retry policy options for connection operations.
/// </summary>
public sealed class RetryPolicyOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts.
	/// </value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retries.
	/// </summary>
	/// <value>
	/// The base delay between retries.
	/// </value>
	public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum delay between retries.
	/// </summary>
	/// <value>
	/// The maximum delay between retries.
	/// </value>
	public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to use exponential backoff.
	/// </summary>
	/// <value>
	/// A value indicating whether to use exponential backoff.
	/// </value>
	public bool UseExponentialBackoff { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to add jitter to retry delays.
	/// </summary>
	/// <value>
	/// A value indicating whether to add jitter to retry delays.
	/// </value>
	public bool UseJitter { get; set; } = true;
}
