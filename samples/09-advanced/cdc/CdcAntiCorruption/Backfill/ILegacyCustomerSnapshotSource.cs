// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

namespace CdcAntiCorruption.Backfill;

/// <summary>
/// Provides historical customer snapshots for backfill and replay.
/// </summary>
public interface ILegacyCustomerSnapshotSource
{
	/// <summary>
	/// Fetches the next batch of historical customer snapshots using cursor-based pagination.
	/// </summary>
	/// <param name="cursor">Opaque cursor from previous fetch, or null to start from beginning.</param>
	/// <param name="batchSize">Maximum batch size to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A cursor-based result containing the batch and the next cursor.</returns>
	Task<CursorFetchResult<LegacyCustomerSnapshot>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken);
}
