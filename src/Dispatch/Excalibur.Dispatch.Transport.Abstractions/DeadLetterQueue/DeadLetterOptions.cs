// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Configuration options for dead letter queue management.
/// </summary>
public sealed class DeadLetterOptions
{
	/// <summary>
	/// Gets or sets the name of the dead letter queue.
	/// </summary>
	/// <value>The current <see cref="DeadLetterQueueName"/> value.</value>
	public string? DeadLetterQueueName { get; set; }

	/// <summary>
	/// Gets or sets the maximum delivery attempts before dead lettering.
	/// </summary>
	/// <value>The current <see cref="MaxDeliveryAttempts"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxDeliveryAttempts { get; set; } = 5;

	/// <summary>
	/// Gets or sets the retention period for messages in the DLQ.
	/// </summary>
	/// <value>
	/// The retention period for messages in the DLQ.
	/// </value>
	public TimeSpan MessageRetentionPeriod { get; set; } = TimeSpan.FromDays(14);

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic dead lettering.
	/// </summary>
	/// <value>The current <see cref="EnableAutomaticDeadLettering"/> value.</value>
	public bool EnableAutomaticDeadLettering { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include stack traces in DLQ metadata.
	/// </summary>
	/// <value>The current <see cref="IncludeStackTrace"/> value.</value>
	public bool IncludeStackTrace { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum size of the DLQ.
	/// </summary>
	/// <value>The current <see cref="MaxQueueSizeInBytes"/> value.</value>
	[Range(1, long.MaxValue)]
	public long MaxQueueSizeInBytes { get; set; } = 1_073_741_824; // 1 GB

	/// <summary>
	/// Gets or sets a value indicating whether to enable DLQ monitoring.
	/// </summary>
	/// <value>The current <see cref="EnableMonitoring"/> value.</value>
	public bool EnableMonitoring { get; set; } = true;

	/// <summary>
	/// Gets or sets the monitoring interval.
	/// </summary>
	/// <value>
	/// The monitoring interval.
	/// </value>
	public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets alert thresholds.
	/// </summary>
	/// <value>
	/// Alert thresholds.
	/// </value>
	public DeadLetterAlertThresholds AlertThresholds { get; set; } = new();
}
