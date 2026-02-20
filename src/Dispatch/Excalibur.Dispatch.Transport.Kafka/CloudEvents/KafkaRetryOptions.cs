// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka retry settings.
/// </summary>
public sealed class KafkaRetryOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts.
	/// </value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base retry delay.
	/// </summary>
	/// <value>
	/// The base retry delay.
	/// </value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets or sets a value indicating whether to use exponential backoff.
	/// </summary>
	/// <value>
	/// A value indicating whether to use exponential backoff.
	/// </value>
	public bool UseExponentialBackoff { get; set; } = true;

	/// <summary>
	/// Gets or sets the backoff multiplier for exponential backoff.
	/// </summary>
	/// <value>
	/// The backoff multiplier for exponential backoff.
	/// </value>
	public double BackoffMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets the maximum retry delay.
	/// </summary>
	/// <value>
	/// The maximum retry delay.
	/// </value>
	public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to add jitter to retry delays.
	/// </summary>
	/// <value>
	/// A value indicating whether to add jitter to retry delays.
	/// </value>
	public bool UseJitter { get; set; } = true;
}
