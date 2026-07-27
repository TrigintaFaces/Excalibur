// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Options.Transport;

/// <summary>
/// Options for cron timer transport.
/// </summary>
public sealed class CronTimerOptions
{
	/// <summary>
	/// Gets or sets the time zone for the cron expression.
	/// </summary>
	/// <value> The time zone used when evaluating the cron schedule. </value>
	public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;

	/// <summary>
	/// Gets or sets a value indicating whether to run the timer immediately on startup.
	/// </summary>
	/// <value> <see langword="true" /> to trigger on startup; otherwise, <see langword="false" />. </value>
	public bool RunOnStartup { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to prevent overlapping executions.
	/// </summary>
	/// <value> <see langword="true" /> to skip runs when a previous execution is still active. </value>
	public bool PreventOverlap { get; set; } = true;

	/// <summary>
	/// Gets or sets the policy controlling how occurrences that elapse while an execution is
	/// running (or while the host is paused) are handled.
	/// </summary>
	/// <value>
	/// The catch-up policy. Default is <see cref="CronTimerCatchUpPolicy.Skip" />, which drops
	/// missed occurrences and resumes at the next future occurrence.
	/// </value>
	public CronTimerCatchUpPolicy CatchUpPolicy { get; set; } = CronTimerCatchUpPolicy.Skip;

	/// <summary>
	/// Gets or sets the maximum number of missed occurrences fired in a single catch-up pass
	/// when <see cref="CatchUpPolicy" /> is <see cref="CronTimerCatchUpPolicy.FireAll" />.
	/// </summary>
	/// <value> The upper bound on catch-up fires per pass. Default is 100. </value>
	public int MaxCatchUpOccurrences { get; set; } = 100;
}
