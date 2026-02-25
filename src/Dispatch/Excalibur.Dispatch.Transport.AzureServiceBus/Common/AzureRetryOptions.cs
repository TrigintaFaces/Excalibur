// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure-specific retry options.
/// </summary>
public sealed class AzureRetryOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retries.
	/// </summary>
	/// <value>
	/// The maximum number of retries.
	/// </value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retries.
	/// </summary>
	/// <value>
	/// The delay between retries.
	/// </value>
	public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum delay between retries.
	/// </summary>
	/// <value>
	/// The maximum delay between retries.
	/// </value>
	public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Gets or sets the retry mode.
	/// </summary>
	/// <value>
	/// The retry mode.
	/// </value>
	public RetryMode Mode { get; set; } = RetryMode.Exponential;
}
