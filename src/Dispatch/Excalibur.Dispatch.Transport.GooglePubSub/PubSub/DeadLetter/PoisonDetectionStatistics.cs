// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Statistics about poison message detection.
/// </summary>
public sealed class PoisonDetectionStatistics
{
	/// <summary>
	/// Gets or sets the total number of tracked messages.
	/// </summary>
	/// <value>
	/// The total number of tracked messages.
	/// </value>
	public int TotalTrackedMessages { get; set; }

	/// <summary>
	/// Gets or sets the number of messages with multiple failures.
	/// </summary>
	/// <value>
	/// The number of messages with multiple failures.
	/// </value>
	public int MultipleFailuresCount { get; set; }

	/// <summary>
	/// Gets or sets the total number of failures.
	/// </summary>
	/// <value>
	/// The total number of failures.
	/// </value>
	public int TotalFailures { get; set; }

	/// <summary>
	/// Gets or sets the number of recent failures.
	/// </summary>
	/// <value>
	/// The number of recent failures.
	/// </value>
	public int RecentFailures { get; set; }

	/// <summary>
	/// Gets or sets the detected patterns.
	/// </summary>
	/// <value>
	/// The detected patterns.
	/// </value>
	public List<PatternInfo> DetectedPatterns { get; set; } = [];
}
