// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Represents business day status information.
/// </summary>
public sealed class BusinessDayStatus
{
	/// <summary>
	/// Gets or sets a value indicating whether it's a business day.
	/// </summary>
	/// <value>
	/// A value indicating whether it's a business day.
	/// </value>
	public bool IsBusinessDay { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether it's within business hours.
	/// </summary>
	/// <value>
	/// A value indicating whether it's within business hours.
	/// </value>
	public bool IsBusinessHours { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether it's a holiday.
	/// </summary>
	/// <value>
	/// A value indicating whether it's a holiday.
	/// </value>
	public bool IsHoliday { get; set; }

	/// <summary>
	/// Gets or sets the holiday name if applicable.
	/// </summary>
	/// <value>
	/// The holiday name if applicable.
	/// </value>
	public string? HolidayName { get; set; }

	/// <summary>
	/// Gets or sets the next business day.
	/// </summary>
	/// <value>
	/// The next business day.
	/// </value>
	public DateOnly NextBusinessDay { get; set; }
}
