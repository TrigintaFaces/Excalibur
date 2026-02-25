// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Options for dead letter analytics service.
/// </summary>
public sealed class DeadLetterAnalyticsOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether the analytics service is enabled.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether the analytics service is enabled.
	/// </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the dead letter subscription to monitor.
	/// </summary>
	/// <value>
	/// The dead letter subscription to monitor.
	/// </value>
	public SubscriptionName? DeadLetterSubscription { get; set; }

	/// <summary>
	/// Gets or sets the interval between analytics collection cycles.
	/// </summary>
	/// <value>
	/// The interval between analytics collection cycles.
	/// </value>
	public TimeSpan CollectionInterval { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the interval between analytics reports.
	/// </summary>
	/// <value>
	/// The interval between analytics reports.
	/// </value>
	public TimeSpan ReportingInterval { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the batch size for pulling messages from dead letter queue.
	/// </summary>
	/// <value>
	/// The batch size for pulling messages from dead letter queue.
	/// </value>
	public int BatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable detailed logging.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable detailed logging.
	/// </value>
	public bool EnableDetailedLogging { get; set; }
}
