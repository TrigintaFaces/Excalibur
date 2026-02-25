// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for poison message detection.
/// </summary>
public sealed class PoisonDetectionOptions
{
	/// <summary>
	/// Gets or sets the maximum failures before considering a message poison.
	/// </summary>
	/// <value>
	/// The maximum failures before considering a message poison.
	/// </value>
	public int MaxFailuresBeforePoison { get; set; } = 5;

	/// <summary>
	/// Gets or sets the number of rapid failures to trigger detection.
	/// </summary>
	/// <value>
	/// The number of rapid failures to trigger detection.
	/// </value>
	public int RapidFailureCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets the time window for rapid failure detection.
	/// </summary>
	/// <value>
	/// The time window for rapid failure detection.
	/// </value>
	public TimeSpan RapidFailureWindow { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the threshold for consistent exception detection.
	/// </summary>
	/// <value>
	/// The threshold for consistent exception detection.
	/// </value>
	public double ConsistentExceptionThreshold { get; set; } = 0.8;

	/// <summary>
	/// Gets or sets the threshold for timeout pattern detection.
	/// </summary>
	/// <value>
	/// The threshold for timeout pattern detection.
	/// </value>
	public double TimeoutThreshold { get; set; } = 0.7;

	/// <summary>
	/// Gets or sets the threshold for loop detection.
	/// </summary>
	/// <value>
	/// The threshold for loop detection.
	/// </value>
	public int LoopDetectionThreshold { get; set; } = 10;

	/// <summary>
	/// Gets or sets the retention period for message history.
	/// </summary>
	/// <value>
	/// The retention period for message history.
	/// </value>
	public TimeSpan HistoryRetentionPeriod { get; set; } = TimeSpan.FromHours(24);
}
