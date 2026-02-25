// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Options for the acknowledgment batcher.
/// </summary>
public sealed class AcknowledgmentBatcherOptions
{
	/// <summary>
	/// Gets or sets the maximum batch size.
	/// </summary>
	/// <value>
	/// The maximum batch size.
	/// </value>
	public int BatchSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the flush interval.
	/// </summary>
	/// <value>
	/// The flush interval.
	/// </value>
	public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets or sets the deadline warning threshold.
	/// </summary>
	/// <value>
	/// The deadline warning threshold.
	/// </value>
	public TimeSpan DeadlineWarningThreshold { get; set; } = TimeSpan.FromSeconds(30);
}
