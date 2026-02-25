// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Statistics for a specific size bucket.
/// </summary>
public sealed class BucketStatistics
{
	/// <summary>
	/// Gets the name identifier for this bucket.
	/// </summary>
	/// <value> The friendly bucket identifier. </value>
	public string BucketName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the maximum size for buffers in this bucket.
	/// </summary>
	/// <value> The maximum buffer size represented by the bucket. </value>
	public int MaxSize { get; init; }

	/// <summary>
	/// Gets the total number of times buffers have been rented from this bucket.
	/// </summary>
	/// <value> The total rent count recorded for the bucket. </value>
	public long TotalRents { get; init; }

	/// <summary>
	/// Gets the total number of times buffers have been returned to this bucket.
	/// </summary>
	/// <value> The total return count recorded for the bucket. </value>
	public long TotalReturns { get; init; }
}
