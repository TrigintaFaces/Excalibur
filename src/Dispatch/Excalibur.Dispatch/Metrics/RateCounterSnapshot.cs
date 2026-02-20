// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Snapshot of counter state.
/// </summary>
public sealed class RateCounterSnapshot
{
	/// <summary>
	/// Gets or sets the counter value.
	/// </summary>
	/// <value>The current <see cref="Value"/> value.</value>
	public long Value { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the snapshot.
	/// </summary>
	/// <value>The current <see cref="Timestamp"/> value.</value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets when the counter was last reset.
	/// </summary>
	/// <value>The current <see cref="LastReset"/> value.</value>
	public DateTimeOffset LastReset { get; set; }

	/// <summary>
	/// Gets or sets the time since last reset.
	/// </summary>
	/// <value>The current <see cref="TimeSinceReset"/> value.</value>
	public TimeSpan TimeSinceReset { get; set; }

	/// <summary>
	/// Gets or sets the current rate per second.
	/// </summary>
	/// <value>The current <see cref="Rate"/> value.</value>
	public double Rate { get; set; }

	/// <summary>
	/// Gets or sets the average rate since reset.
	/// </summary>
	/// <value>The current <see cref="AverageRate"/> value.</value>
	public double AverageRate { get; set; }
}
