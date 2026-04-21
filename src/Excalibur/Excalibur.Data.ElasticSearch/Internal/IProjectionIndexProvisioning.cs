// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Operation-axis seam 4/4 for <see cref="Projections.EventualConsistencyTracker"/> —
/// index provisioning. 2 methods, ≤5 cap.
/// </summary>
internal interface IProjectionIndexProvisioning
{
	Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken);

	/// <summary>
	/// Creates a consistency-tracking index with the pre-registered mapping
	/// for the given <paramref name="kind"/>. Adapter owns the
	/// <c>TypeMapping</c> construction so the SDK type never crosses the seam.
	/// </summary>
	Task<bool> CreateIndexAsync(string indexName, ConsistencyIndexKind kind, CancellationToken cancellationToken);
}
