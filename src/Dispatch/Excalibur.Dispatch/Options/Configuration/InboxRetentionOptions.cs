// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Configuration options for inbox message retention and cleanup policies.
/// </summary>
public sealed class InboxRetentionOptions
{
	/// <summary>
	/// Gets or sets the maximum message retention period.
	/// </summary>
	/// <value>The maximum message retention period. Default is 7 days.</value>
	public TimeSpan MaxRetention { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets the cleanup interval.
	/// </summary>
	/// <value>The cleanup interval. Default is 1 hour.</value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the cleanup interval in seconds for processed entries.
	/// </summary>
	/// <value>The cleanup interval in seconds. Default is 3600.</value>
	[Range(1, int.MaxValue)]
	public int CleanupIntervalSeconds { get; set; } = 3600;

	/// <summary>
	/// Gets or sets the retention period in days for processed entries.
	/// </summary>
	/// <value>The retention period in days. Default is 7.</value>
	[Range(1, int.MaxValue)]
	public int RetentionDays { get; set; } = 7;
}
