// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Resilience options for the Postgres persistence provider.
/// Controls retry behavior for transient failure recovery.
/// </summary>
public sealed class PostgresPersistenceResilienceOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures. Default is 3.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts for transient failures. Default is 3.
	/// </value>
	[Range(0, 10, ErrorMessage = "Max retry attempts must be between 0 and 10")]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts in milliseconds. Default is 1000ms.
	/// </summary>
	/// <value>
	/// The delay between retry attempts in milliseconds. Default is 1000ms.
	/// </value>
	[Range(100, 30000, ErrorMessage = "Retry delay must be between 100 and 30000 milliseconds")]
	public int RetryDelayMilliseconds { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the ceiling on a single backoff delay, in milliseconds. Default is 30000ms.
	/// </summary>
	/// <value>
	/// The ceiling on a single backoff delay, in milliseconds. Default is 30000ms.
	/// </value>
	/// <remarks>
	/// <para>
	/// Exponential backoff doubles the wait after every failed attempt, so without a ceiling the delay is
	/// bounded only by the attempt budget: a base of 30000ms across ten attempts reaches roughly 4.7 hours
	/// before the last one and sleeps about 9.4 hours in total, inside a single data request. The caller
	/// observes that as a hung request rather than a failure, which is the one outcome it cannot diagnose.
	/// </para>
	/// <para>
	/// With a ceiling the worst case is bounded by construction and is computable up front:
	/// <see cref="MaxRetryAttempts"/> multiplied by this value - at most ten attempts of thirty seconds,
	/// so five minutes. The ceiling never shortens the attempt budget; it only stops an individual wait
	/// from growing without limit.
	/// </para>
	/// <para>
	/// Must be greater than or equal to <see cref="RetryDelayMilliseconds"/>. A ceiling below the base
	/// delay describes no schedule the provider can honour, so it is rejected at startup rather than
	/// quietly reinterpreted.
	/// </para>
	/// </remarks>
	[Range(100, 30000, ErrorMessage = "Max retry delay must be between 100 and 30000 milliseconds")]
	public int MaxRetryDelayMilliseconds { get; set; } = 30000;

	/// <summary>
	/// Validates these options and throws when they describe a retry schedule the provider cannot honour.
	/// </summary>
	/// <exception cref="ValidationException"> Thrown when validation fails. </exception>
	/// <remarks>
	/// The range attributes above are checked by the parent options' generated annotation validator.
	/// This method carries only the cross-property rule that no annotation can express.
	/// </remarks>
	internal void Validate()
	{
		if (MaxRetryDelayMilliseconds < RetryDelayMilliseconds)
		{
			throw new ValidationException(
				$"{nameof(MaxRetryDelayMilliseconds)} ({MaxRetryDelayMilliseconds}ms) must be greater than or equal to "
				+ $"{nameof(RetryDelayMilliseconds)} ({RetryDelayMilliseconds}ms). A ceiling below the base delay "
				+ "describes no schedule the provider can honour.");
		}
	}
}
