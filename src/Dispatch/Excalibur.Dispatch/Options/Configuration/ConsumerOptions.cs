// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Configuration options for message consumers.
/// </summary>
public sealed class ConsumerOptions
{
	/// <summary>
	/// Gets or sets the deduplication configuration.
	/// </summary>
	/// <value>
	/// The deduplication configuration.
	/// </value>
	public DeduplicationOptions Dedupe { get; set; } = new();

	/// <summary>
	/// Gets or sets a value indicating whether to automatically acknowledge messages after successful processing.
	/// </summary>
	/// <value> Default is true. </value>
	public bool AckAfterHandle { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of concurrent messages to process.
	/// </summary>
	/// <value>The current <see cref="MaxConcurrentMessages"/> value.</value>
	public int MaxConcurrentMessages { get; set; } = 10;

	/// <summary>
	/// Gets or sets the visibility timeout for messages.
	/// </summary>
	/// <value>
	/// The visibility timeout for messages.
	/// </value>
	public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the maximum number of retries for failed messages.
	/// </summary>
	/// <value>The current <see cref="MaxRetries"/> value.</value>
	public int MaxRetries { get; set; } = 3;
}
