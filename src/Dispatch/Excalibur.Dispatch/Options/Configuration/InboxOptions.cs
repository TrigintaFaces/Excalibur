// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Configuration options for the inbox pattern.
/// </summary>
public sealed class InboxOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether inbox processing is enabled. When true, uses full inbox mode with persistent storage. When
	/// false, uses light mode with in-memory deduplication.
	/// </summary>
	/// <value>The current <see cref="Enabled"/> value.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the expiry time in hours for deduplication entries in light mode.
	/// </summary>
	/// <value> Default is 24 hours. </value>
	public int DeduplicationExpiryHours { get; set; } = 24;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically acknowledge messages after successful processing.
	/// </summary>
	/// <value> Default is true. </value>
	public bool AckAfterHandle { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for failed messages.
	/// </summary>
	/// <value> Default is 3. </value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts in minutes.
	/// </summary>
	/// <value> Default is 5 minutes. </value>
	public int RetryDelayMinutes { get; set; } = 5;

	/// <summary>
	/// Gets or sets the maximum message retention period.
	/// </summary>
	/// <value>
	/// The maximum message retention period.
	/// </value>
	public TimeSpan MaxRetention { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets the cleanup interval.
	/// </summary>
	/// <value>
	/// The cleanup interval.
	/// </value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the cleanup interval in seconds for processed entries.
	/// </summary>
	/// <value>The current <see cref="CleanupIntervalSeconds"/> value.</value>
	public int CleanupIntervalSeconds { get; set; } = 3600;

	/// <summary>
	/// Gets or sets the retention period in days for processed entries.
	/// </summary>
	/// <value>The current <see cref="RetentionDays"/> value.</value>
	public int RetentionDays { get; set; } = 7;
}
