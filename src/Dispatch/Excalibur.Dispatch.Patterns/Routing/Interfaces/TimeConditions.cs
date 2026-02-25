// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Defines time conditions for routing rules.
/// </summary>
public sealed class TimeConditions
{
	/// <summary>
	/// Gets the time ranges when this rule is active.
	/// </summary>
	/// <value>
	/// The time ranges when this rule is active.
	/// </value>
	public Collection<TimeRange> ActiveTimeRanges { get; } = [];

	/// <summary>
	/// Gets the days of the week when this rule is active.
	/// </summary>
	/// <value>
	/// The days of the week when this rule is active.
	/// </value>
	public Collection<DayOfWeek> ActiveDaysOfWeek { get; } = [];

	/// <summary>
	/// Gets the dates when this rule is active.
	/// </summary>
	/// <value>
	/// The dates when this rule is active.
	/// </value>
	public Collection<DateRange> ActiveDates { get; } = [];

	/// <summary>
	/// Gets or sets the timezone for evaluating time conditions.
	/// </summary>
	/// <value>
	/// The timezone for evaluating time conditions.
	/// </value>
	public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;

	/// <summary>
	/// Gets or sets special conditions (holidays, business days, etc.).
	/// </summary>
	/// <value>
	/// Special conditions (holidays, business days, etc.).
	/// </value>
	public SpecialConditions? SpecialConditions { get; set; }
}
