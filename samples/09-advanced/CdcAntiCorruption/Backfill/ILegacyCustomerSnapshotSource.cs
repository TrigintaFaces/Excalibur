// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace CdcAntiCorruption.Backfill;

/// <summary>
/// Provides historical customer snapshots for backfill and replay.
/// </summary>
public interface ILegacyCustomerSnapshotSource
{
	/// <summary>
	/// Fetches the next batch of historical customer snapshots.
	/// </summary>
	/// <param name="skip">Number of records to skip.</param>
	/// <param name="batchSize">Maximum batch size to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A batch of snapshots for processing.</returns>
	Task<IEnumerable<LegacyCustomerSnapshot>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken);
}
