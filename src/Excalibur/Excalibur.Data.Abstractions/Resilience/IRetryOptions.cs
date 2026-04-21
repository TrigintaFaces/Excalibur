// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Configuration options for retry policies.
/// </summary>
public interface IRetryOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts for DataRequest execution.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts for DataRequest execution.
	/// </value>
	int MaxRetryAttempts { get; set; }

	/// <summary>
	/// Gets or sets the base delay between retry attempts.
	/// </summary>
	/// <value>
	/// The base delay between retry attempts.
	/// </value>
	TimeSpan BaseRetryDelay { get; set; }

	/// <summary>
	/// Gets or sets the maximum delay between retry attempts (for exponential backoff).
	/// </summary>
	/// <value>
	/// The maximum delay between retry attempts (for exponential backoff).
	/// </value>
	TimeSpan MaxRetryDelay { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use exponential backoff for retry delays.
	/// </summary>
	/// <value>
	/// A value indicating whether to use exponential backoff for retry delays.
	/// </value>
	bool UseExponentialBackoff { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to add jitter to retry delays to prevent thundering herd.
	/// </summary>
	/// <value>
	/// A value indicating whether to add jitter to retry delays to prevent thundering herd.
	/// </value>
	bool UseJitter { get; set; }
}
