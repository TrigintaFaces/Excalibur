// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Common polling statistics for transport providers.
/// </summary>
public sealed class TransportPollingStatistics
{
	/// <summary>
	/// Gets or sets the total number of polling operations performed.
	/// </summary>
	public int TotalPolls { get; set; }

	/// <summary>
	/// Gets or sets the total number of messages received across all polls.
	/// </summary>
	public int TotalMessages { get; set; }

	/// <summary>
	/// Gets or sets the total number of errors encountered during polling.
	/// </summary>
	public int TotalErrors { get; set; }

	/// <summary>
	/// Gets or sets the total cumulative duration of all polling operations.
	/// </summary>
	public TimeSpan TotalDuration { get; set; }
}
