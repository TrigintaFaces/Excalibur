// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Google Pub/Sub retry policy configuration.
/// </summary>
public sealed class GooglePubSubRetryPolicy
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts.
	/// </value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the initial retry delay.
	/// </summary>
	/// <value>
	/// The initial retry delay.
	/// </value>
	public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets or sets the maximum retry delay.
	/// </summary>
	/// <value>
	/// The maximum retry delay.
	/// </value>
	public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Gets or sets the retry delay multiplier for exponential backoff.
	/// </summary>
	/// <value>
	/// The retry delay multiplier for exponential backoff.
	/// </value>
	public double DelayMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to add jitter to retry delays.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to add jitter to retry delays.
	/// </value>
	public bool UseJitter { get; set; } = true;
}
