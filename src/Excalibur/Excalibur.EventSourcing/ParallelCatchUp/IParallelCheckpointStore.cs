// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.ParallelCatchUp;

/// <summary>
/// Stores per-worker checkpoint positions for parallel catch-up processing.
/// </summary>
/// <remarks>
/// <para>
/// Internal interface -- providers implement this behind the existing checkpoint store
/// abstraction. Consumers don't interact with it directly (per P2 decision).
/// </para>
/// </remarks>
internal interface IParallelCheckpointStore
{
	/// <summary>
	/// Saves a worker's current position checkpoint.
	/// </summary>
	Task SaveWorkerCheckpointAsync(
		string projectionName,
		int workerId,
		long position,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the low watermark (minimum checkpoint across all workers) for a projection.
	/// </summary>
	Task<long> GetLowWatermarkAsync(
		string projectionName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets all worker checkpoints for a projection.
	/// </summary>
	Task<IReadOnlyDictionary<int, long>> GetAllWorkerCheckpointsAsync(
		string projectionName,
		CancellationToken cancellationToken);
}
