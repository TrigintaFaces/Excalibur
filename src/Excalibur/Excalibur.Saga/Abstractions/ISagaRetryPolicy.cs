// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Defines retry policy for saga steps.
/// </summary>
public interface ISagaRetryPolicy
{
	/// <summary>
	/// Gets the maximum number of retry attempts.
	/// </summary>
	/// <value>the maximum number of retry attempts.</value>
	int MaxAttempts { get; }

	/// <summary>
	/// Gets the delay between retry attempts.
	/// </summary>
	/// <value>the delay between retry attempts.</value>
	TimeSpan RetryDelay { get; }

	/// <summary>
	/// Determines if an exception should trigger a retry.
	/// </summary>
	/// <param name="exception"> The exception to evaluate. </param>
	/// <returns> True if should retry, false otherwise. </returns>
	bool ShouldRetry(Exception exception);
}

