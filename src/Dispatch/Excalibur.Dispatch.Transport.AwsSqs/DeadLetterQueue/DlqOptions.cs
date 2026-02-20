// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for DLQ processing.
/// </summary>
public sealed class DlqOptions
{
	/// <summary>
	/// Gets or sets the DLQ URL.
	/// </summary>
	/// <value>
	/// The DLQ URL.
	/// </value>
	public Uri? DeadLetterQueueUrl { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retries before permanent failure.
	/// </summary>
	/// <value>
	/// The maximum number of retries before permanent failure.
	/// </value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts.
	/// </summary>
	/// <value>
	/// The delay between retry attempts.
	/// </value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets a value indicating whether to use exponential backoff for retries.
	/// </summary>
	/// <value>
	/// A value indicating whether to use exponential backoff for retries.
	/// </value>
	public bool UseExponentialBackoff { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum age of messages to process.
	/// </summary>
	/// <value>
	/// The maximum age of messages to process.
	/// </value>
	public TimeSpan MaxMessageAge { get; set; } = TimeSpan.FromDays(14);

	/// <summary>
	/// Gets or sets a value indicating whether to archive permanently failed messages.
	/// </summary>
	/// <value>
	/// A value indicating whether to archive permanently failed messages.
	/// </value>
	public bool ArchiveFailedMessages { get; set; } = true;

	/// <summary>
	/// Gets or sets the archive storage location.
	/// </summary>
	/// <value>
	/// The archive storage location.
	/// </value>
	public string? ArchiveLocation { get; set; }

	/// <summary>
	/// Gets or sets the batch size for processing DLQ messages.
	/// </summary>
	/// <value>
	/// The batch size for processing DLQ messages.
	/// </value>
	public int BatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic redrive.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable automatic redrive.
	/// </value>
	public bool EnableAutomaticRedrive { get; set; }

	/// <summary>
	/// Gets or sets the automatic redrive interval.
	/// </summary>
	/// <value>
	/// The automatic redrive interval.
	/// </value>
	public TimeSpan AutomaticRedriveInterval { get; set; } = TimeSpan.FromHours(1);
}
