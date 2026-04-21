// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.ParallelCatchUp;

/// <summary>
/// Partitions a global event stream position range into sub-ranges for parallel processing.
/// </summary>
public interface IGlobalStreamPartitioner
{
	/// <summary>
	/// Splits the given position range into sub-ranges for parallel workers.
	/// </summary>
	/// <param name="fromPosition">The start position (inclusive).</param>
	/// <param name="toPosition">The end position (inclusive).</param>
	/// <param name="workerCount">The number of parallel workers.</param>
	/// <returns>A list of stream ranges, one per worker.</returns>
	IReadOnlyList<StreamRange> Partition(long fromPosition, long toPosition, int workerCount);
}
