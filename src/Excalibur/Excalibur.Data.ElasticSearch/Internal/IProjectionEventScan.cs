// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Operation-axis seam 3/4 for <see cref="Projections.EventualConsistencyTracker"/> —
/// parameterized range + aggregate queries. 4 methods, ≤5 cap.
/// </summary>
internal interface IProjectionEventScan
{
	/// <summary>
	/// Returns the latest <c>writeTimestamp</c> in the write-tracking index, if any.
	/// </summary>
	Task<DateTimeOffset?> GetLatestWriteTimestampAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Searches the read-tracking index using the consolidated
	/// <see cref="ReadEventSearch"/> filter record. Adapter translates non-null
	/// fields into SDK query clauses; consolidates L232/L312/L376/L629 sites.
	/// </summary>
	Task<IReadOnlyList<ReadEventDocument>> SearchReadsAsync(
		ReadEventSearch search,
		CancellationToken cancellationToken);

	/// <summary>
	/// Searches the write-tracking index for events older than a cutoff
	/// timestamp, ordered ascending by <c>writeTimestamp</c>.
	/// </summary>
	Task<IReadOnlyList<WriteEventDocument>> SearchWritesOlderThanAsync(
		DateTime cutoff,
		int maxResults,
		CancellationToken cancellationToken);

	/// <summary>
	/// Counts documents in a consistency-tracking index, optionally filtered.
	/// Filter semantics: <see cref="ProjectionCountFilter.All"/> ignores
	/// <paramref name="filterValue"/>;
	/// <see cref="ProjectionCountFilter.ReadsByProjectionType"/> requires it as
	/// the projection type to match.
	/// </summary>
	Task<long> GetDocumentCountAsync(
		string indexName,
		ProjectionCountFilter filter,
		string? filterValue,
		CancellationToken cancellationToken);
}
