// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Alert thresholds for dead letter queue monitoring.
/// </summary>
public sealed class DeadLetterAlertThresholds
{
	/// <summary>
	/// Gets or sets the message count threshold.
	/// </summary>
	/// <value>The current <see cref="MessageCountThreshold"/> value.</value>
	public int MessageCountThreshold { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the age threshold for oldest message.
	/// </summary>
	/// <value>
	/// The age threshold for oldest message.
	/// </value>
	public TimeSpan OldestMessageAgeThreshold { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets the queue size threshold in bytes.
	/// </summary>
	/// <value>The current <see cref="QueueSizeThresholdInBytes"/> value.</value>
	public long QueueSizeThresholdInBytes { get; set; } = 536_870_912; // 512 MB

	/// <summary>
	/// Gets or sets the failure rate threshold percentage.
	/// </summary>
	/// <value>The current <see cref="FailureRateThreshold"/> value.</value>
	public double FailureRateThreshold { get; set; } = 10.0;
}
