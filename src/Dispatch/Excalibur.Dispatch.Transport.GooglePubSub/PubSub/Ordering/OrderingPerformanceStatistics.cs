// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Performance statistics for ordering operations.
/// </summary>
public sealed class OrderingPerformanceStatistics
{
	/// <summary>
	/// Gets total messages received in sequence.
	/// </summary>
	/// <value>
	/// Total messages received in sequence.
	/// </value>
	public long TotalInSequence { get; init; }

	/// <summary>
	/// Gets total messages received out of sequence.
	/// </summary>
	/// <value>
	/// Total messages received out of sequence.
	/// </value>
	public long TotalOutOfSequence { get; init; }

	/// <summary>
	/// Gets the percentage of messages in sequence.
	/// </summary>
	/// <value>
	/// The percentage of messages in sequence.
	/// </value>
	public double SequenceRatio { get; init; }

	/// <summary>
	/// Gets the average sequence gap size.
	/// </summary>
	/// <value>
	/// The average sequence gap size.
	/// </value>
	public double AverageGapSize { get; init; }

	/// <summary>
	/// Gets the 95th percentile gap size.
	/// </summary>
	/// <value>
	/// The 95th percentile gap size.
	/// </value>
	public double P95GapSize { get; init; }

	/// <summary>
	/// Gets the maximum gap size observed.
	/// </summary>
	/// <value>
	/// The maximum gap size observed.
	/// </value>
	public double MaxGapSize { get; init; }
}
