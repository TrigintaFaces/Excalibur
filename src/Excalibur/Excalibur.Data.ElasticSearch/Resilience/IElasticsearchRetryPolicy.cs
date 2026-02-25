// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Provides retry policy functionality for Elasticsearch operations with configurable backoff strategies.
/// </summary>
public interface IElasticsearchRetryPolicy
{
	/// <summary>
	/// Gets the maximum number of retry attempts configured for this policy.
	/// </summary>
	/// <value> The maximum number of retry attempts. </value>
	int MaxAttempts { get; }

	/// <summary>
	/// Calculates the delay before the next retry attempt.
	/// </summary>
	/// <param name="attemptNumber"> The zero-based attempt number (0 for first retry, 1 for second, etc.). </param>
	/// <returns> The time to wait before the next retry attempt. </returns>
	TimeSpan GetRetryDelay(int attemptNumber);

	/// <summary>
	/// Determines whether a retry should be attempted for the given exception.
	/// </summary>
	/// <param name="exception"> The exception that occurred during the operation. </param>
	/// <param name="attemptNumber"> The current attempt number (1-based). </param>
	/// <returns> True if a retry should be attempted, false otherwise. </returns>
	bool ShouldRetry(Exception exception, int attemptNumber);
}
