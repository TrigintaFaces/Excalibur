// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Operation-axis seam 1/4 for <see cref="Projections.EventualConsistencyTracker"/> —
/// ingests tracking documents for write events, read events, and checkpoints.
/// 3 methods, ≤5 cap.
/// </summary>
internal interface IProjectionEventIngest
{
	Task<bool> IndexWriteEventAsync(WriteEventDocument document, string id, CancellationToken cancellationToken);

	Task<bool> IndexReadEventAsync(ReadEventDocument document, string id, CancellationToken cancellationToken);

	Task<bool> IndexCheckpointAsync(ProjectionCheckpointDocument document, string projectionType, CancellationToken cancellationToken);
}
