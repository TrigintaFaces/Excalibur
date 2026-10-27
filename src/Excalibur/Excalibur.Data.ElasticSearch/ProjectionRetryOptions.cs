// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures bounded retries for create-only projection error-record persistence.
/// </summary>
public sealed class ProjectionRetryOptions
{
	/// <summary>
	/// Gets a value indicating whether retry is enabled for projection error-record persistence.
	/// </summary>
	/// <value>
	/// A value indicating whether retry is enabled for projection error-record persistence.
	/// </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the maximum total attempts, including the initial create, for a single error record.
	/// </summary>
	/// <value>
	/// The maximum total attempts, including the initial create, for a single error record.
	/// </value>
	public int MaxIndexAttempts { get; init; } = 3;

	/// <summary>
	/// Gets the maximum total attempts, including the initial request, for bulk error records.
	/// </summary>
	/// <value>
	/// The maximum total attempts, including the initial request, for bulk error records.
	/// </value>
	public int MaxBulkAttempts { get; init; } = 2;

	/// <summary>
	/// Gets the base delay for exponential backoff.
	/// </summary>
	/// <value>
	/// The base delay for exponential backoff.
	/// </value>
	public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the maximum delay between retries.
	/// </summary>
	/// <value>
	/// The maximum delay between retries.
	/// </value>
	public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets a value indicating whether to use exponential backoff.
	/// </summary>
	/// <value>
	/// A value indicating whether to use exponential backoff.
	/// </value>
	public bool UseExponentialBackoff { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether retry delays are randomized to decorrelate concurrent clients.
	/// </summary>
	/// <value>
	/// <see langword="true" /> to randomize each delay; otherwise <see langword="false" />. Defaults to
	/// <see langword="true" />.
	/// </value>
	/// <remarks>
	/// Replaces a <c>JitterFactor</c> double that scaled a symmetric band -- delays varied by plus or minus
	/// the fraction around the nominal. A band keeps its mass near the nominal, so clients that failed in
	/// the same instant stayed clustered and retried together; the randomisation added noise without
	/// decorrelating anything, which is the only reason to apply it. The strategy now uses the resilience
	/// pipeline's own jitter, which reaches the low end of the interval.
	/// </remarks>
	public bool UseJitter { get; init; } = true;
}
