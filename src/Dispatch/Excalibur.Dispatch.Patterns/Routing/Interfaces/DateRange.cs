// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Represents a date range.
/// </summary>
public sealed class DateRange
{
	/// <summary>
	/// Gets or sets the start date.
	/// </summary>
	/// <value>
	/// The start date.
	/// </value>
	public DateOnly StartDate { get; set; }

	/// <summary>
	/// Gets or sets the end date.
	/// </summary>
	/// <value>
	/// The end date.
	/// </value>
	public DateOnly EndDate { get; set; }

	/// <summary>
	/// Determines if a given date falls within this range.
	/// </summary>
	/// <param name="date"> The date to check. </param>
	/// <returns> True if the date is within the range. </returns>
	public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;
}
