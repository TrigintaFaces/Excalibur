// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
}
