// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Backoff settings for reconnection attempts.
/// </summary>
public sealed class BackoffOptions
{
	/// <summary>
	/// Gets or sets the initial delay before the first retry.
	/// </summary>
	/// <value>
	/// The initial delay before the first retry.
	/// </value>
	public TimeSpan InitialDelay { get; set; }

	/// <summary>
	/// Gets or sets the maximum delay between retries.
	/// </summary>
	/// <value>
	/// The maximum delay between retries.
	/// </value>
	public TimeSpan MaxDelay { get; set; }

	/// <summary>
	/// Gets or sets the multiplier for exponential backoff.
	/// </summary>
	/// <value>
	/// The multiplier for exponential backoff.
	/// </value>
	public double Multiplier { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts.
	/// </value>
	public int MaxAttempts { get; set; }
}
