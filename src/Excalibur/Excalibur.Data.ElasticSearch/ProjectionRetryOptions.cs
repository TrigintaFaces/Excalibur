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
	/// Gets the fractional jitter magnitude from 0 to 1; delays vary by plus or minus this fraction and are capped by MaxDelay.
	/// </summary>
	/// <value>
	/// The jitter factor for retry delays.
	/// </value>
	public double JitterFactor { get; init; } = 0.2;
}
