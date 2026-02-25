// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures retry policy behavior for transient failure handling.
/// </summary>
public sealed class RetryPolicyOptions
{
	/// <summary>
	/// Gets a value indicating whether retry policies are enabled.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether retries are active. Defaults to <c> true </c>. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the maximum number of retry attempts.
	/// </summary>
	/// <value> An <see cref="int" /> representing the maximum retry count. Defaults to 3. </value>
	public int MaxAttempts { get; init; } = 3;

	/// <summary>
	/// Gets the base delay for exponential backoff.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the initial retry delay. Defaults to 1 second. </value>
	public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the maximum delay between retry attempts.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the maximum retry delay. Defaults to 30 seconds. </value>
	public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the jitter factor for randomizing retry delays.
	/// </summary>
	/// <value> A <see cref="double" /> between 0.0 and 1.0 representing the jitter factor. Defaults to 0.1. </value>
	public double JitterFactor { get; init; } = 0.1;

	/// <summary>
	/// Gets a value indicating whether to use exponential backoff for retry delays.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to use exponential backoff. Defaults to <c> true </c>. </value>
	public bool UseExponentialBackoff { get; init; } = true;
}
