// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Defines special conditions for routing.
/// </summary>
public sealed class SpecialConditions
{
	/// <summary>
	/// Gets or sets a value indicating whether to only route on business days.
	/// </summary>
	/// <value>
	/// A value indicating whether to only route on business days.
	/// </value>
	public bool BusinessDaysOnly { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to exclude holidays.
	/// </summary>
	/// <value>
	/// A value indicating whether to exclude holidays.
	/// </value>
	public bool ExcludeHolidays { get; set; }

	/// <summary>
	/// Gets custom holiday dates.
	/// </summary>
	/// <value>
	/// Custom holiday dates.
	/// </value>
	public Collection<DateOnly> CustomHolidays { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to only route during peak hours.
	/// </summary>
	/// <value>
	/// A value indicating whether to only route during peak hours.
	/// </value>
	public bool PeakHoursOnly { get; set; }

	/// <summary>
	/// Gets custom peak hour definitions.
	/// </summary>
	/// <value>
	/// Custom peak hour definitions.
	/// </value>
	public Collection<TimeRange> PeakHours { get; } = [];
}
