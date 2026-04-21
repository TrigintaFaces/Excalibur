// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Retry settings for Google Cloud Pub/Sub.
/// </summary>
public sealed class PubSubRetryOptions
{
	/// <summary>
	/// Gets or sets the initial retry delay.
	/// </summary>
	/// <value>
	/// The initial retry delay.
	/// </value>
	public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets or sets the retry delay multiplier.
	/// </summary>
	/// <value>
	/// The retry delay multiplier.
	/// </value>
	public double RetryDelayMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets the maximum retry delay.
	/// </summary>
	/// <value>
	/// The maximum retry delay.
	/// </value>
	public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Gets or sets the total timeout.
	/// </summary>
	/// <value>
	/// The total timeout.
	/// </value>
	public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromMinutes(10);
}
