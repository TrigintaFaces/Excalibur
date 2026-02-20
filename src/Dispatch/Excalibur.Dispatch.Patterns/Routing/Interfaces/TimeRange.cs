// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Represents a time range.
/// </summary>
public sealed class TimeRange
{
	/// <summary>
	/// Gets or sets the start time.
	/// </summary>
	/// <value>
	/// The start time.
	/// </value>
	public TimeSpan StartTime { get; set; }

	/// <summary>
	/// Gets or sets the end time.
	/// </summary>
	/// <value>
	/// The end time.
	/// </value>
	public TimeSpan EndTime { get; set; }

	/// <summary>
	/// Determines if a given time falls within this range.
	/// </summary>
	/// <param name="time"> The time to check. </param>
	/// <returns> True if the time is within the range. </returns>
	public bool Contains(TimeSpan time)
	{
		if (StartTime <= EndTime)
		{
			return time >= StartTime && time <= EndTime;
		}

		// Handle ranges that cross midnight
		return time >= StartTime || time <= EndTime;
	}
}
