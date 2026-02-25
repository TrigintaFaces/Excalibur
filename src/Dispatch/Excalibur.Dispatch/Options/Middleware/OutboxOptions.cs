// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for outbox middleware behavior.
/// </summary>
public sealed class OutboxOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether outbox processing is enabled.
	/// </summary>
	/// <value> Default is false (disabled). </value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the default priority for outbound messages.
	/// </summary>
	/// <value> Default is 0 (normal priority). Higher values indicate higher priority. </value>
	public int DefaultPriority { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to continue processing if staging a message fails.
	/// </summary>
	/// <value> Default is false (fail fast). </value>
	public bool ContinueOnStagingError { get; set; }

	/// <summary>
	/// Gets or sets message types that bypass outbox staging.
	/// </summary>
	/// <value> The current <see cref="BypassOutboxForTypes" /> value. </value>
	public string[]? BypassOutboxForTypes { get; set; }

	/// <summary>
	/// Gets or sets the batch size for background publishing.
	/// </summary>
	/// <value> Default is 100 messages per batch. </value>
	public int PublishBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the polling interval for the background publisher.
	/// </summary>
	/// <value> Default is 5 seconds. </value>
	public TimeSpan PublishPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for failed messages.
	/// </summary>
	/// <value> Default is 3. </value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retry attempts.
	/// </summary>
	/// <remarks>
	/// When <see cref="EnableExponentialRetryBackoff"/> is true, this is the initial delay for the first retry.
	/// Subsequent retries use exponential backoff: delay * 2^(retryCount-1), capped at <see cref="MaxRetryDelay"/>.
	/// </remarks>
	/// <value> Default is 5 minutes. </value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets a value indicating whether exponential backoff is used for retry delays.
	/// </summary>
	/// <remarks>
	/// When enabled, the delay between retry attempts increases exponentially:
	/// attempt 1 = <see cref="RetryDelay"/>, attempt 2 = 2x, attempt 3 = 4x, etc.
	/// The delay is capped at <see cref="MaxRetryDelay"/>.
	/// </remarks>
	/// <value> Default is false (fixed delay). </value>
	public bool EnableExponentialRetryBackoff { get; set; }

	/// <summary>
	/// Gets or sets the maximum retry delay when exponential backoff is enabled.
	/// </summary>
	/// <value> Default is 30 minutes. </value>
	public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>
	/// Gets or sets the age after which sent messages are cleaned up.
	/// </summary>
	/// <value> Default is 7 days. </value>
	public TimeSpan CleanupAge { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets the interval for running cleanup operations.
	/// </summary>
	/// <value> Default is 1 hour. </value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets a value indicating whether adaptive polling is enabled.
	/// </summary>
	/// <remarks>
	/// When enabled, the polling interval dynamically adjusts between
	/// <see cref="MinPollingInterval"/> and <see cref="PublishPollingInterval"/> based on message throughput.
	/// Idle periods gradually increase the interval; active processing resets to the minimum.
	/// </remarks>
	/// <value> Default is false (fixed interval). </value>
	public bool EnableAdaptivePolling { get; set; }

	/// <summary>
	/// Gets or sets the minimum polling interval when adaptive polling is enabled.
	/// </summary>
	/// <remarks>
	/// Used as the poll interval immediately after processing messages.
	/// Must be less than or equal to <see cref="PublishPollingInterval"/>.
	/// </remarks>
	/// <value> Default is 500 milliseconds. </value>
	public TimeSpan MinPollingInterval { get; set; } = TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// Gets or sets the backoff multiplier for adaptive polling.
	/// </summary>
	/// <remarks>
	/// After each idle poll (no messages found), the current interval is multiplied by this factor
	/// until reaching <see cref="PublishPollingInterval"/>. A value of 2.0 means doubling each idle cycle.
	/// </remarks>
	/// <value> Default is 2.0. </value>
	public double AdaptivePollingBackoffMultiplier { get; set; } = 2.0;
}
