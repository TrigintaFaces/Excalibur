// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Operation-axis seam 2/4 for <see cref="Projections.EventualConsistencyTracker"/> —
/// point-lookup + small-cardinality reads. 3 methods, ≤5 cap.
/// </summary>
internal interface IProjectionEventLookup
{
	/// <summary>
	/// Gets a single write event by id. Returns <see langword="null"/> if not found.
	/// </summary>
	Task<WriteEventDocument?> GetWriteEventByIdAsync(string eventId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the most recent <see cref="ReadEventDocument"/> for a given projection.
	/// </summary>
	Task<ReadEventDocument?> GetLatestReadForProjectionAsync(string projectionType, CancellationToken cancellationToken);

	/// <summary>
	/// Lists distinct projection types from the checkpoint index.
	/// </summary>
	Task<IReadOnlyList<string>> GetProjectionTypesAsync(CancellationToken cancellationToken);
}
