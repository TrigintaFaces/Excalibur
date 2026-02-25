// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures retry policies specific to projection operations.
/// </summary>
public sealed class ProjectionRetryOptions
{
	/// <summary>
	/// Gets a value indicating whether retry is enabled for projection operations.
	/// </summary>
	/// <value>
	/// A value indicating whether retry is enabled for projection operations.
	/// </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the maximum retry attempts for index operations.
	/// </summary>
	/// <value>
	/// The maximum retry attempts for index operations.
	/// </value>
	public int MaxIndexAttempts { get; init; } = 3;

	/// <summary>
	/// Gets the maximum retry attempts for bulk operations.
	/// </summary>
	/// <value>
	/// The maximum retry attempts for bulk operations.
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
	/// Gets the jitter factor for retry delays.
	/// </summary>
	/// <value>
	/// The jitter factor for retry delays.
	/// </value>
	public double JitterFactor { get; init; } = 0.2;
}
